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
            if (!emoteSystem.IsEmoting) return true;

            __instance.mesh.enabled = false;
            __instance.meshShadows.enabled = false;
            __instance.spotlight.enabled = false;
            __instance.halo.enabled = false;
            __instance.LightActive = false;

            // Rewound to Hidden, not just switched off. The renderers above are only ever
            // turned back on by Intro and LightOn, and those are reachable from Hidden
            // alone: leaving the state at Idle means the machine resumes after the dance
            // with everything off and no transition left that would undo it, so the
            // flashlight stays dark until something else drags it through Outro. The game
            // does exactly this itself when a player is disabled.
            __instance.currentState = FlashlightController.State.Hidden;
            __instance.hiddenScale = 0f;

            // The lerps belong to whichever animation was interrupted. Intro reads them
            // from the start, so an emote begun mid-intro would otherwise resume the
            // flashlight halfway out of the player's hand.
            __instance.introRotLerp = 0f;
            __instance.introYLerp = 0f;
            __instance.outroRotLerp = 0f;
            __instance.outroYLerp = 0f;
            __instance.lightOnLerp = 0f;

            return false;
        }
    }
}
