using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SomeEmotesREPO.Rig
{
    /// <summary>
    /// The bones of a REPO player avatar that the game drives through its Animator.
    /// Order matters: it is the order the debug overlay and rigpose.json use.
    /// </summary>
    public enum RigBone
    {
        Root = 0,
        LegLeftBottom,
        LegLeftTop,
        LegRightBottom,
        LegRightTop,
        BodyBottom,
        BodyBottomScale,
        BodyTop,
        BodyTopScale,
        ArmLeft,
        ArmRight,
        ArmRightScale,
        HeadBottom,
        HeadTop,
    }

    /// <summary>
    /// Resolves the animator-driven bones of a live PlayerAvatarVisuals.
    ///
    /// The avatar rig interleaves two families of transforms:
    ///   "ANIM *"  written by the game Animator, from generic clips bound by path.
    ///   "code_*"  written by the game's C# every Update: springs, lean, look-at,
    ///             and code_head_top, which PlayerAvatarTalkAnimation rotates from
    ///             the voice chat loudness.
    /// Because they alternate as parent and child, writing the ANIM nodes in
    /// LateUpdate composes with the code_ nodes instead of fighting them: the voice
    /// head motion, the eye look-at and every cosmetic spring keep running on top
    /// of whatever pose we push.
    ///
    /// Verified against REPO on Unity 2022.3.45f1 (resources.assets, GameObject
    /// "Player Visuals"): each "ANIM *" name occurs exactly once inside the avatar,
    /// and each one sits at identity in the prefab. The 37-underscore spacer parents
    /// carry all of the rest geometry. So lookup by name is unambiguous, and writing
    /// identity to a bone means "rest pose", not "collapse to the origin".
    /// </summary>
    public sealed class RepoRigBinder
    {
        public const int BoneCount = 14;

        private static readonly string[] BoneNames =
        {
            "ANIM BOT",
            "ANIM LEG L BOT",
            "ANIM LEG L TOP",
            "ANIM LEG R BOT",
            "ANIM LEG R TOP",
            "ANIM BODY BOT",
            "ANIM BODY BOT SCALE",
            "ANIM BODY TOP",
            "ANIM BODY TOP SCALE",
            "ANIM ARM L",
            "ANIM ARM R",
            "ANIM ARM R SCALE",
            "ANIM HEAD BOT",
            "ANIM HEAD TOP",
        };

        /// <summary>
        /// Animator-driven too, but owned by PlayerEyes and PlayerAvatarEyelids.
        /// We never write these: blinking and gaze must stay the game's job.
        /// </summary>
        private static readonly string[] ReservedNames =
        {
            "ANIM EYE LEFT",
            "ANIM EYE RIGHT",
            "ANIM PUPIL LEFT",
            "ANIM PUPIL LEFT SCALE",
            "ANIM PUPIL RIGHT",
            "ANIM PUPIL RIGHT SCALE",
        };

        /// <summary>
        /// Not a bone we drive. It is the transform PlayerAvatarTalkAnimation rotates
        /// from the microphone, and it hangs under ANIM HEAD TOP. Reading it while we
        /// force a pose is the direct proof that the voice motion survives.
        /// </summary>
        private const string TalkWitnessName = "code_head_top";

        private readonly Transform[] _bones = new Transform[BoneCount];

        private RepoRigBinder(PlayerAvatarVisuals visuals)
        {
            Visuals = visuals;
        }

        public PlayerAvatarVisuals Visuals { get; }

        public Transform? TalkWitness { get; private set; }

        public Transform this[RigBone bone] => _bones[(int)bone];

        public static string NameOf(RigBone bone) => BoneNames[(int)bone];

        /// <summary>
        /// Walks the avatar once and binds every ANIM bone by name.
        /// Fails loudly rather than half-binding: a partial rig would deform the player.
        /// </summary>
        public static bool TryBind(PlayerAvatarVisuals visuals, out RepoRigBinder? binder, out string error)
        {
            binder = null;
            error = string.Empty;

            if (visuals == null)
            {
                error = "PlayerAvatarVisuals is null.";
                return false;
            }

            var found = new Dictionary<string, Transform>(32, StringComparer.Ordinal);
            var duplicates = new List<string>();
            Collect(visuals.transform, found, duplicates);

            var result = new RepoRigBinder(visuals);
            var missing = new List<string>();

            for (int i = 0; i < BoneCount; i++)
            {
                if (found.TryGetValue(BoneNames[i], out var t)) result._bones[i] = t;
                else missing.Add(BoneNames[i]);
            }

            if (missing.Count > 0)
            {
                error = "missing bones: " + string.Join(", ", missing.ToArray());
                return false;
            }

            if (duplicates.Count > 0)
            {
                error = "ambiguous bone names: " + string.Join(", ", duplicates.ToArray());
                return false;
            }

            found.TryGetValue(TalkWitnessName, out var witness);
            result.TalkWitness = witness;

            binder = result;
            return true;
        }

        private static void Collect(Transform t, Dictionary<string, Transform> found, List<string> duplicates)
        {
            string name = t.name;

            if (IsTracked(name))
            {
                if (found.ContainsKey(name)) duplicates.Add(name);
                else found[name] = t;
            }

            for (int i = 0; i < t.childCount; i++)
            {
                Collect(t.GetChild(i), found, duplicates);
            }
        }

        private static bool IsTracked(string name)
        {
            if (name.Length == 0) return false;
            if (string.Equals(name, TalkWitnessName, StringComparison.Ordinal)) return true;
            // Cheap prefix reject before the exact-name comparisons below.
            if (name[0] != 'A') return false;

            for (int i = 0; i < BoneNames.Length; i++)
            {
                if (string.Equals(name, BoneNames[i], StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>
        /// Returns every ANIM node to its prefab rest value. Used when releasing the rig:
        /// bones the current game clip does not animate would otherwise stay where we left them.
        /// </summary>
        public void ResetToRest()
        {
            for (int i = 0; i < BoneCount; i++)
            {
                var t = _bones[i];
                if (t == null) continue;
                t.localRotation = Quaternion.identity;
                t.localScale = Vector3.one;
                // Every bound bone rests at the origin of its offset parent, measured on
                // the live rig; only the pupils carry an offset, and those are reserved.
                // Without this the avatar would stay crouched after an emote that lowered
                // ANIM BOT, since not every game clip writes that channel back.
                t.localPosition = Vector3.zero;
            }
        }

        /// <summary>
        /// Full diagnostic dump. This is the thing to paste in an issue when a REPO
        /// update renames a node: it shows the path each bone resolved to, and which
        /// reserved eye nodes are present.
        /// </summary>
        public string Describe()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Rig bound on '{Visuals.name}' ({BoneCount} bones)");

            for (int i = 0; i < BoneCount; i++)
            {
                sb.AppendLine($"  {BoneNames[i],-22} {PathFrom(Visuals.transform, _bones[i])}");
            }

            sb.AppendLine($"  {TalkWitnessName,-22} {(TalkWitness != null ? PathFrom(Visuals.transform, TalkWitness) : "NOT FOUND (voice head motion cannot be verified)")}");

            var reserved = new List<string>();
            foreach (string name in ReservedNames)
            {
                if (FindByName(Visuals.transform, name) == null) reserved.Add(name + " MISSING");
            }
            sb.Append("  reserved (never written): ");
            sb.Append(reserved.Count == 0 ? "all present" : string.Join(", ", reserved.ToArray()));

            return sb.ToString();
        }

        private static Transform? FindByName(Transform root, string name)
        {
            if (string.Equals(root.name, name, StringComparison.Ordinal)) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var hit = FindByName(root.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }

        private static string PathFrom(Transform root, Transform? t)
        {
            if (t == null) return "<null>";

            var parts = new List<string>();
            var current = t;
            while (current != null && current != root)
            {
                parts.Add(current.name);
                current = current.parent;
            }
            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }
    }
}
