using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SomeEmotesREPO.Rig
{
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

        private static readonly string[] ReservedNames =
        {
            "ANIM EYE LEFT",
            "ANIM EYE RIGHT",
            "ANIM PUPIL LEFT",
            "ANIM PUPIL LEFT SCALE",
            "ANIM PUPIL RIGHT",
            "ANIM PUPIL RIGHT SCALE",
        };

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
            if (name[0] != 'A') return false;

            for (int i = 0; i < BoneNames.Length; i++)
            {
                if (string.Equals(name, BoneNames[i], StringComparison.Ordinal)) return true;
            }
            return false;
        }
        public void ResetToRest()
        {
            for (int i = 0; i < BoneCount; i++)
            {
                var t = _bones[i];
                if (t == null) continue;
                t.localRotation = Quaternion.identity;
                t.localScale = Vector3.one;
                t.localPosition = Vector3.zero;
            }
        }

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
