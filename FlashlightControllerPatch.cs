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
            var emoteSystem = __instance.PlayerAvatar.GetComponent<EmoteSystem>();

            if (emoteSystem == null) return true;

            if (emoteSystem.IsEmoting)
            {
                __instance.mesh.enabled = false;
                __instance.spotlight.enabled = false;
                __instance.halo.enabled = false;
                __instance.LightActive = false;

                return false;
            }
            return true;
        }
    }
}
