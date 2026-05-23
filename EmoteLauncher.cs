using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Linq;


namespace SomeEmotesREPO
{
    public class EmoteLauncher : MonoBehaviour
    {
        public Animator animator;
        public EmoteSystem emoteSystem;
        public List<string> emoteNames = new List<string>();

        private Dictionary<string, AnimationClip> animationDict = new Dictionary<string, AnimationClip>();
        private AnimatorOverrideController overrideController;
        private string overrideKeyName = "Placeholder";

        private float initialRot;
        private Transform targetPlayer;

        private GameObject visuals;
        private GameObject spotlight;

        public List<string> EmoteNames => emoteNames;

        public void AddEntry(string name, AnimationClip clip)
        {
            animationDict.Add(name, clip);
        }

        public void Init(Transform target)
        {
            targetPlayer = target;
            visuals = transform.GetChild(0).gameObject;
            spotlight = transform.GetChild(1).gameObject;
            animator = GetComponentInChildren<Animator>();
            overrideController = new AnimatorOverrideController(animator.runtimeAnimatorController);
            animator.runtimeAnimatorController = overrideController;

            foreach (var entry in animationDict)
            {
                emoteNames.Add(entry.Key);
            }

            InitBones();
            InitLight();
            visuals.SetActive(false);
            spotlight.SetActive(false);
        }

        void Update()
        {
            if (targetPlayer)
            {
                transform.position = targetPlayer.position;
            }
        }

        void LateUpdate()
        {
            if (emoteSystem && emoteSystem.IsEmoting)
            {
                transform.eulerAngles = new Vector3(transform.eulerAngles.x, initialRot, transform.eulerAngles.z);
            }
        }

        public void SetFavorites(List<string> favs)
        {
            if (favs == null || favs.Count == 0)
                return;

            var validFavs = favs.Where(emoteNames.Contains).Distinct().ToList();
            if (validFavs.Count == 0)
                return;

            emoteNames.RemoveAll(validFavs.Contains);
            emoteNames.InsertRange(0, validFavs);
            EmoteLoader.Instance.emotesName = new List<string>(emoteNames);
            int max = System.Math.Min(EmoteSelectionManager.emotePerPages, emoteNames.Count);
            var prefs = emoteNames.Take(max).ToList();
            EmoteLoader.Instance.SetFavorites(prefs);
        }


        public void SetRotation(float _initialRot)
        {
            initialRot = _initialRot;
        }

        public bool Animate(string emoteName)
        {
            if (!animationDict.TryGetValue(emoteName, out var clip))
            {
                return false;
            }

            animator.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            visuals.SetActive(true);
            spotlight.SetActive(true);

            overrideController[overrideKeyName] = clip;
            animator.ResetTrigger("StopEmote");
            animator.SetTrigger("TriggerEmote");
            return true;
        }

        public void StopEmote()
        {
            visuals.SetActive(false);
            spotlight.SetActive(false);
            animator.SetTrigger("StopEmote");
        }

        void InitLight()
        {
            Light light = spotlight.AddComponent<Light>();
            light.transform.localPosition = Vector3.up * 2.5f;
            light.transform.localEulerAngles = -Vector3.left * 90f;
            light.type = LightType.Spot;
            light.innerSpotAngle = 35f;
            light.spotAngle = 40f;
            light.color = new Color(1f, 0.97f, 0.84f);
            light.intensity = 1.75f;
            light.bounceIntensity = 1f;
            light.range = 5f;
            light.shadows = LightShadows.None;
            light.cullingMask = ~0;
        }

        void InitBones()
        {
            leg_R = FindDeepChild(transform, "leg_R_0").GetComponent<SkinnedMeshRenderer>();
            leg_L = FindDeepChild(transform, "leg_L_0").GetComponent<SkinnedMeshRenderer>();
            body_bot = FindDeepChild(transform, "body_bot_0").GetComponent<SkinnedMeshRenderer>();
            body_top_sphere = FindDeepChild(transform, "body_top_sphere_0").GetComponent<SkinnedMeshRenderer>();
            arm_L = FindDeepChild(transform, "arm_L_0").GetComponent<SkinnedMeshRenderer>();
            arm_R = FindDeepChild(transform, "arm_R_0").GetComponent<SkinnedMeshRenderer>();
            health_LED = FindDeepChild(transform, "health_LED_0").GetComponent<SkinnedMeshRenderer>();
            health_frame = FindDeepChild(transform, "health_frame_0").GetComponent<SkinnedMeshRenderer>();
            health_shadow = FindDeepChild(transform, "health_shadow_0").GetComponent<SkinnedMeshRenderer>();
            head_bot_sphere = FindDeepChild(transform, "head_bot_sphere_0").GetComponent<SkinnedMeshRenderer>();
            head_bot_flat = FindDeepChild(transform, "head_bot_flat_0").GetComponent<SkinnedMeshRenderer>();
            head_top = FindDeepChild(transform, "head_top_0").GetComponent<SkinnedMeshRenderer>();
            eye_L = FindDeepChild(transform, "eye_L_0").GetComponent<SkinnedMeshRenderer>();
            iris_L = FindDeepChild(transform, "iris_L_0").GetComponent<SkinnedMeshRenderer>();
            eye_R = FindDeepChild(transform, "eye_R_0").GetComponent<SkinnedMeshRenderer>();
            iris_R = FindDeepChild(transform, "iris_R_0").GetComponent<SkinnedMeshRenderer>();
        }

        public void InitTexturesFrom(GameObject initial)
        {
            TryCopyMaterials(leg_R, initial, "mesh_leg_r");
            TryCopyMaterials(leg_L, initial, "mesh_leg_l");
            TryCopyMaterials(body_bot, initial, "mesh_body_bot");
            TryCopyMaterials(body_top_sphere, initial, "mesh_body_top_sphere");
            TryCopyMaterials(arm_L, initial, "mesh_arm_l");
            TryCopyMaterials(arm_R, initial, "mesh_arm_r");
            TryCopyMaterials(health_LED, initial, "mesh_health");
            TryCopyMaterials(health_frame, initial, "mesh_health frame");
            TryCopyMaterials(health_shadow, initial, "mesh_health shadow");
            TryCopyMaterials(head_bot_sphere, initial, "mesh_head_bot_sphere");
            TryCopyMaterials(head_bot_flat, initial, "mesh_head_bot_flat");
            TryCopyMaterials(head_top, initial, "mesh_head_top");
            TryCopyMaterials(eye_L, initial, "mesh_eye_l");
            TryCopyMaterials(iris_L, initial, "mesh_pupil_l");
            TryCopyMaterials(eye_R, initial, "mesh_eye_r");
            TryCopyMaterials(iris_R, initial, "mesh_pupil_r");
        }

        void TryCopyMaterials(SkinnedMeshRenderer target, GameObject source, string boneName)
        {
            if (target == null) return;
            var materials = GetMaterialsFromBone(source, boneName);
            if (materials != null) target.materials = materials;
        }

        Material[] GetMaterialsFromBone(GameObject initial, string name)
        {
            Transform mesh = FindDeepChild(initial.transform, name);
            if (mesh != null)
            {
                var materials = mesh.GetComponent<MeshRenderer>().materials;
                return materials;
            }
            return default;
        }

        Transform FindDeepChild(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name.ToLower() == childName.ToLower())
                    return child;

                Transform result = FindDeepChild(child, childName);
                if (result != null)
                    return result;
            }
            return null;
        }


        public SkinnedMeshRenderer
            leg_R,
            leg_L,
            body_bot,
            body_top_sphere,
            arm_L,
            arm_R,
            health_LED,
            health_frame,
            health_shadow,
            head_bot_sphere,
            head_bot_flat,
            head_top,
            eye_L,
            iris_L,
            eye_R,
            iris_R;
    }
}
