using HarmonyLib;

namespace SomeEmotesREPO
{
    [HarmonyPatch(typeof(FlashlightController))]
    class FlashlightControllerPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch("Update")]
        private static bool Update_Prefix(FlashlightController __instance)
        {
            var emoteSystem = __instance.PlayerAvatar != null
                ? __instance.PlayerAvatar.GetComponent<EmoteSystem>()
                : null;

            if (emoteSystem == null) return true;

            if (emoteSystem.IsEmoting)
            {
                __instance.mesh.enabled = false;
                __instance.meshShadows.enabled = false;
                __instance.spotlight.enabled = false;
                __instance.halo.enabled = false;
                __instance.LightActive = false;

                emoteSystem.flashlightHeld = true;
                return false;
            }

            if (emoteSystem.flashlightHeld)
            {
                emoteSystem.flashlightHeld = false;

                __instance.currentState = FlashlightController.State.Hidden;
                __instance.hiddenScale = 0f;
                __instance.introRotLerp = 0f;
                __instance.introYLerp = 0f;
                __instance.outroRotLerp = 0f;
                __instance.outroYLerp = 0f;
                __instance.lightOnLerp = 0f;
            }

            return true;
        }
    }
}
