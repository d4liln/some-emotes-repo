using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;

namespace SomeEmotesREPO
{
    [HarmonyPatch(typeof(PlayerExpression))]
    internal class AddPatchPlayerExpression
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(PlayerExpression.DoExpression))]
        private static bool DoExpression_Prefix()
        {
            return !BlockBecauseEmoteIsPlaying();
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(PlayerExpression.ToggleExpression))]
        private static bool ToggleExpression_Prefix()
        {
            return !BlockBecauseEmoteIsPlaying();
        }
        private static bool BlockBecauseEmoteIsPlaying()
        {
            var emotes = EmoteSystem.Instance;
            return emotes != null && emotes.IsEmoting;
        }
    }
}
    