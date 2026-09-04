using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SomeEmotesREPO.Rig
{
    public sealed class EmoteSuppression
    {
        private static readonly Dictionary<PlayerAvatarVisuals, EmoteSuppression> Suppressed =
            new Dictionary<PlayerAvatarVisuals, EmoteSuppression>();

        public static bool HoldCodeTransforms { get; set; } = true;

        private readonly PlayerAvatarVisuals _visuals;
        private readonly PlayerAvatarLeftArm? _leftArm;
        private readonly Quaternion _frozenRotation;

        /// <summary>0 leaves the avatar entirely to the game, 1 suppresses fully.</summary>
        public float Weight { get; set; } = 1f;

        private EmoteSuppression(PlayerAvatarVisuals visuals, Quaternion? heading)
        {
            _visuals = visuals;

            _leftArm = visuals.GetComponentInChildren<PlayerAvatarLeftArm>();
            _frozenRotation = heading ?? visuals.transform.rotation;
        }

        public static bool IsActive(PlayerAvatarVisuals visuals) => visuals != null && Suppressed.ContainsKey(visuals);

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

            _visuals.animator.speed = 1f;
        }


        private static void Prune()
        {
            List<PlayerAvatarVisuals>? dead = null;
            foreach (var pair in Suppressed)
            {
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

            if (_visuals.animator != null) _visuals.animator.speed = 1f - weight;

            if (_visuals.playerAvatarRightArm != null) _visuals.playerAvatarRightArm.forceBasePoseTimer = 0.5f;

            Quaternion held = Quaternion.Slerp(_visuals.transform.rotation, _frozenRotation, weight);
            _visuals.transform.rotation = held;
            _visuals.bodySpringTarget = held;
            _visuals.bodySpring.lastRotation = held;

            if (!HoldCodeTransforms) return;

            Hold(_visuals.leanTransform, weight);
            Hold(_visuals.tiltTransform, weight);
            Hold(_visuals.legTwistTransform, weight);
            Hold(_visuals.bodyTopUpTransform, weight);
            Hold(_visuals.bodyTopSideTransform, weight);
            Hold(_visuals.headUpTransform, weight);
            Hold(_visuals.headSideTransform, weight);

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
