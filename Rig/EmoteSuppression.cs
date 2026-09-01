using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SomeEmotesREPO.Rig
{
    /// <summary>
    /// Everything the game keeps writing to the avatar that would fight, or spoil, a
    /// pose we are driving ourselves. Phase 2 established what has to be silenced;
    /// phase 4 makes the silence fade in and out instead of switching.
    ///
    /// Three distinct problems, all solved in the same place:
    ///
    ///   Footstep ghosts. The game clips carry animation events (footsteps, landing
    ///   spring impulses). We overwrite the bones, so the motion is invisible, but the
    ///   events still fire and the player is heard walking on the spot. Slowing the
    ///   animator to a stop kills them. It has to happen after PlayerAvatarVisuals.Update
    ///   and before the Animator evaluates: a postfix on that Update is exactly that
    ///   window. LateUpdate would be one frame too late, every frame.
    ///
    ///   The grabber arm. PlayerAvatarRightArm drives code_arm_r into its grabber pose
    ///   from Update. forceBasePoseTimer is the game's own way of telling it to let go,
    ///   and it is already used when tumbling.
    ///
    ///   Body spin. The avatar visuals rotate to follow where the player aims. During an
    ///   emote the body should hold its heading while the camera keeps orbiting, so the
    ///   player can look at themselves dance.
    ///
    /// Every one of those is applied through <see cref="Weight"/>, against the value the
    /// game wrote this very frame. That is what makes a crossfade possible at all: at
    /// weight 0.3 the body is 30 percent held and 70 percent the game's own live pose,
    /// not "held, but only sometimes".
    ///
    /// One instance per avatar, because in multiplayer several players dance at once.
    /// </summary>
    public sealed class EmoteSuppression
    {
        private static readonly Dictionary<PlayerAvatarVisuals, EmoteSuppression> Suppressed =
            new Dictionary<PlayerAvatarVisuals, EmoteSuppression>();

        /// <summary>
        /// Whether the code-driven transforms that follow the camera are held at rest.
        /// A debug switch, shared by every avatar: it exists to show what they cost.
        ///
        /// These sit *between* the bone the solver aims and the mesh it moves, so no
        /// amount of aiming can compensate for them: they lean the torso and tip the
        /// head on top of the dance, following the mouse. code_head_top is deliberately
        /// left out, because that is the jaw the microphone drives.
        /// </summary>
        public static bool HoldCodeTransforms { get; set; } = true;

        private readonly PlayerAvatarVisuals _visuals;
        private readonly PlayerAvatarLeftArm? _leftArm;
        private readonly Quaternion _frozenRotation;

        /// <summary>0 leaves the avatar entirely to the game, 1 suppresses fully.</summary>
        public float Weight { get; set; } = 1f;

        private EmoteSuppression(PlayerAvatarVisuals visuals, Quaternion? heading)
        {
            _visuals = visuals;
            // PlayerAvatarVisuals tracks the right arm but not the left; both components
            // sit on the same object.
            _leftArm = visuals.GetComponentInChildren<PlayerAvatarLeftArm>();
            // A networked emote passes the heading the dancer chose, so every client
            // freezes the same one. Without it each client would freeze whatever heading
            // its own interpolated copy of the avatar happened to be at, and the same
            // dance would face a slightly different way on every screen.
            _frozenRotation = heading ?? visuals.transform.rotation;
        }

        public static bool IsActive(PlayerAvatarVisuals visuals) => visuals != null && Suppressed.ContainsKey(visuals);

        /// <summary>
        /// Starts suppressing, and locks the body to the heading it has right now.
        /// Returns the existing handle if this avatar is already suppressed, so two
        /// callers can never end up fighting over the same body.
        /// </summary>
        public static EmoteSuppression? Begin(PlayerAvatarVisuals visuals, Quaternion? heading = null)
        {
            if (visuals == null) return null;
            if (Suppressed.TryGetValue(visuals, out var existing)) return existing;

            Prune();
            var suppression = new EmoteSuppression(visuals, heading);
            Suppressed[visuals] = suppression;
            return suppression;
        }

        public void End()
        {
            Suppressed.Remove(_visuals);

            if (_visuals == null || _visuals.animator == null) return;
            // The game only ever writes animator.speed to freeze a hurt player, and never
            // writes it back, so releasing it is our job. If it is hurt-freezing right now
            // its own Update re-zeroes this next frame, which is why this needs no guard.
            _visuals.animator.speed = 1f;
        }

        /// <summary>
        /// Drops avatars destroyed by a level change or a disconnect. Cheap, and it runs
        /// once per emote rather than once per frame.
        /// </summary>
        private static void Prune()
        {
            List<PlayerAvatarVisuals>? dead = null;
            foreach (var pair in Suppressed)
            {
                // Unity's == reports a destroyed object as null while the reference is
                // still a perfectly good dictionary key, which is exactly what is needed
                // to remove the entry.
                if (pair.Key == null)
                {
                    dead ??= new List<PlayerAvatarVisuals>();
                    dead.Add(pair.Key!);
                }
            }
            if (dead == null) return;
            foreach (var key in dead) Suppressed.Remove(key);
        }

        internal static void Tick(PlayerAvatarVisuals visuals)
        {
            if (Suppressed.Count == 0) return;
            if (Suppressed.TryGetValue(visuals, out var suppression)) suppression.Apply();
        }

        private void Apply()
        {
            float weight = Mathf.Clamp01(Weight);
            if (weight <= 0f) return;

            // Slowed rather than stopped: the animation events thin out with the motion
            // during a fade instead of cutting dead on the first frame of the emote.
            if (_visuals.animator != null) _visuals.animator.speed = 1f - weight;

            if (_visuals.playerAvatarRightArm != null) _visuals.playerAvatarRightArm.forceBasePoseTimer = 0.5f;

            Quaternion held = Quaternion.Slerp(_visuals.transform.rotation, _frozenRotation, weight);
            _visuals.transform.rotation = held;
            _visuals.bodySpringTarget = held;
            _visuals.bodySpring.lastRotation = held;

            if (!HoldCodeTransforms) return;

            // Body-level secondary motion: lean from velocity, tilt from sprinting,
            // leg twist from the direction of travel.
            Hold(_visuals.leanTransform, weight);
            Hold(_visuals.tiltTransform, weight);
            Hold(_visuals.legTwistTransform, weight);

            // Torso and head following where the player aims.
            Hold(_visuals.bodyTopUpTransform, weight);
            Hold(_visuals.bodyTopSideTransform, weight);
            Hold(_visuals.headUpTransform, weight);
            Hold(_visuals.headSideTransform, weight);

            // The arms carry code transforms of their own, below the bone the solver
            // aims, so they rotate the limb away from wherever it was pointed.
            // forceBasePoseTimer is not enough: it settles the arm on basePose, which is
            // a pose, not identity. The right arm is the worse of the two because it has
            // two of them plus a stretch on its parent, which is why it looked wrong
            // while the left one passed.
            if (_leftArm != null)
            {
                Hold(_leftArm.leftArmTransform, weight);
            }
            if (_visuals.playerAvatarRightArm != null)
            {
                Hold(_visuals.playerAvatarRightArm.rightArmTransform, weight);
                Transform? parent = _visuals.playerAvatarRightArm.rightArmParentTransform;
                Hold(parent, weight);
                if (parent != null) parent.localScale = Vector3.Lerp(parent.localScale, Vector3.one, weight);
            }
        }

        private static void Hold(Transform? t, float weight)
        {
            if (t == null) return;
            t.localRotation = weight >= 1f
                ? Quaternion.identity
                : Quaternion.Slerp(t.localRotation, Quaternion.identity, weight);
        }
    }

    [HarmonyPatch(typeof(PlayerAvatarVisuals), nameof(PlayerAvatarVisuals.Update))]
    internal static class EmoteSuppressionUpdatePatch
    {
        private static void Postfix(PlayerAvatarVisuals __instance)
        {
            EmoteSuppression.Tick(__instance);
        }
    }
}
