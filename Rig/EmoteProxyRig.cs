using System;
using UnityEngine;

namespace SomeEmotesREPO.Rig
{
    public enum ProxyBone
    {
        Hips = 0,
        Spine1,
        Spine2,
        Neck,
        Head,
        HeadTop,
        ArmLeft,
        HandLeft,
        ArmRight,
        HandRight,
        UpLegLeft,
        KneeLeft,
        FootLeft,
        UpLegRight,
        KneeRight,
        FootRight,
    }

    public sealed class EmoteProxyRig : MonoBehaviour
    {
        /// <summary>
        /// The clip in the bundle's controller that gets swapped for the emote.
        ///
        /// Taken from the controller rather than named in code. It used to be the literal
        /// string "Placeholder", and that is a trap: the string indexer of an
        /// AnimatorOverrideController looks the clip up by name and, finding nothing,
        /// does nothing at all. No exception, no warning. Every emote then "played"
        /// successfully while the proxy stood still, and the avatar froze with it. A
        /// rebuilt controller whose clip is called anything else is enough to cause it.
        /// </summary>
        private AnimationClip? _slot;

        private static readonly string[] BoneNames =
        {
            "mixamorig:Hips",
            "mixamorig:Spine1",
            "mixamorig:Spine2",
            "mixamorig:Neck",
            "mixamorig:Head",
            "mixamorig:HeadTop_End",
            "mixamorig:LeftArm",
            "mixamorig:LeftHand",
            "mixamorig:RightArm",
            "mixamorig:RightHand",
            "mixamorig:LeftUpLeg",
            "mixamorig:LeftLeg",
            "mixamorig:LeftFoot",
            "mixamorig:RightUpLeg",
            "mixamorig:RightLeg",
            "mixamorig:RightFoot",
        };

        private readonly Transform?[] _bones = new Transform?[BoneNames.Length];

        private Animator _animator = null!;
        private AnimatorOverrideController _override = null!;

        public Transform Root => _animator.transform;

        public string CurrentClip { get; private set; } = string.Empty;

        public Transform? this[ProxyBone bone] => _bones[(int)bone];

        private bool _visible;

        public static EmoteProxyRig? Create(bool visible = false)
        {
            GameObject? prefab = EmoteBundleLoader.LoadAsset<GameObject>("emote.prefab");
            if (prefab == null)
            {
                SomeEmotesREPO.Logger.LogError("[Solver] emote.prefab not found in the bundle.");
                return null;
            }

            GameObject instance = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            instance.name = visible ? "SomeEmotesREPO Reference Dancer" : "SomeEmotesREPO Proxy Rig";

            var proxy = instance.AddComponent<EmoteProxyRig>();
            proxy._visible = visible;
            if (!proxy.Setup())
            {
                Destroy(instance);
                return null;
            }
            return proxy;
        }

        public void PlaceAt(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
        }

        public void ApplyMaterial(Material material)
        {
            if (material == null) return;

            int painted = 0;
            foreach (var renderer in GetComponentsInChildren<Renderer>(includeInactive: true))
            {
                var materials = new Material[renderer.sharedMaterials.Length == 0 ? 1 : renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
                painted++;
            }
            SomeEmotesREPO.Logger.LogInfo($"[Solver] Reference dancer painted with '{material.name}' on {painted} renderers.");
        }

        private bool Setup()
        {
            var animator = GetComponentInChildren<Animator>();
            if (animator == null)
            {
                SomeEmotesREPO.Logger.LogError("[Solver] The proxy prefab has no Animator.");
                return false;
            }
            _animator = animator;

            if (!_visible)
            {
                foreach (var renderer in GetComponentsInChildren<Renderer>(includeInactive: true)) renderer.enabled = false;
                foreach (var light in GetComponentsInChildren<Light>(includeInactive: true)) light.enabled = false;
            }
            else
            {
                foreach (var t in GetComponentsInChildren<Transform>(includeInactive: true)) t.gameObject.SetActive(true);

                int shown = 0;
                foreach (var renderer in GetComponentsInChildren<Renderer>(includeInactive: true))
                {
                    renderer.enabled = true;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    renderer.gameObject.layer = 0;
                    shown++;
                }
                SomeEmotesREPO.Logger.LogInfo($"[Solver] Reference dancer built with {shown} renderers.");
            }

            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _animator.applyRootMotion = false;

            _override = new AnimatorOverrideController(_animator.runtimeAnimatorController);
            _animator.runtimeAnimatorController = _override;

            var slots = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>>(_override.overridesCount);
            _override.GetOverrides(slots);
            _slot = slots.Count > 0 ? slots[0].Key : null;

            if (_slot == null)
            {
                SomeEmotesREPO.Logger.LogError(
                    "[Solver] The bundle's animator controller holds no clip to swap out, so no emote can play. "
                    + "Its state needs a clip assigned to it.");
                return false;
            }
            SomeEmotesREPO.Logger.LogInfo($"[Solver] Emote clips will replace '{_slot.name}' in the controller.");

            // A humanoid muscle clip can only be evaluated through a humanoid Avatar. If
            // the prefab lost its Avatar in a rebuild, or the clips came back from the
            // importer as generic, the animator runs and writes nothing: no error, no
            // warning, and a proxy frozen in its bind pose. These two flags are the only
            // way to tell that apart from a controller that never transitions.
            var parameters = new System.Collections.Generic.List<string>();
            foreach (var parameter in _animator.parameters) parameters.Add(parameter.name);

            SomeEmotesREPO.Logger.LogInfo(
                $"[Solver] Proxy animator: avatar '{(_animator.avatar != null ? _animator.avatar.name : "<none>")}', "
                + $"human {_animator.isHuman}, layers {_animator.layerCount}, "
                + $"parameters [{string.Join(", ", parameters.ToArray())}]");

            if (!_animator.isHuman)
            {
                // Refusing beats carrying on. Without an Avatar the clips write nothing,
                // the proxy holds its bind pose, and the solver copies that onto the
                // player every frame: the avatar freezes instead of simply not dancing,
                // which reads as the mod being broken rather than the emote being absent.
                SomeEmotesREPO.Logger.LogError(
                    "[Solver] The dancer prefab has no humanoid Avatar, so no emote can play. "
                    + "In Unity, select Emote.prefab, and set the Avatar field on its Animator to the "
                    + "humanoid avatar generated by the model's FBX, then rebuild the bundle.");
                return false;
            }

            var missing = new System.Collections.Generic.List<string>();
            for (int i = 0; i < BoneNames.Length; i++)
            {
                _bones[i] = FindDeep(transform, BoneNames[i]);
                if (_bones[i] == null) missing.Add(BoneNames[i]);
            }

            if (missing.Count > 0)
            {
                SomeEmotesREPO.Logger.LogError("[Solver] Proxy rig is missing joints: " + string.Join(", ", missing.ToArray()));
                return false;
            }

            return true;
        }

        public bool Play(AnimationClip clip)
        {
            if (clip == null) return false;

            if (_slot == null) return false;

            _override[_slot] = clip;
            CurrentClip = clip.name;
            _animator.ResetTrigger("StopEmote");
            _animator.SetTrigger("TriggerEmote");
            return true;
        }

        public void Stop()
        {
            // Reached from OnDestroy on a level change, by which point Unity may already
            // have taken the animator away.
            if (_animator != null) _animator.SetTrigger("StopEmote");
            CurrentClip = string.Empty;
        }

        public Vector3 Position(ProxyBone bone)
        {
            Transform? t = _bones[(int)bone];
            return t == null ? Vector3.zero : Quaternion.Inverse(Root.rotation) * (t.position - Root.position);
        }

        public Vector3 Direction(ProxyBone from, ProxyBone to)
        {
            Transform? a = _bones[(int)from];
            Transform? b = _bones[(int)to];
            if (a == null || b == null) return Vector3.zero;

            Vector3 delta = b.position - a.position;
            if (delta.sqrMagnitude < 1e-8f) return Vector3.zero;

            return Quaternion.Inverse(Root.rotation) * delta.normalized;
        }

        public float Length(ProxyBone from, ProxyBone to)
        {
            Transform? a = _bones[(int)from];
            Transform? b = _bones[(int)to];
            return a == null || b == null ? 0f : Vector3.Distance(a.position, b.position);
        }

        private static Transform? FindDeep(Transform root, string name)
        {
            if (string.Equals(root.name, name, StringComparison.Ordinal)) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var hit = FindDeep(root.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}
