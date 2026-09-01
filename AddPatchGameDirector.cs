using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;

namespace SomeEmotesREPO
{
    [HarmonyPatch(typeof(GameDirector))]
    class AddPatchGameDirector
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(GameDirector.DeathStart))]
        private static void DeathStart_Prefix()
        {
            var emotes = EmoteSystem.Instance;
            if (emotes != null && emotes.IsEmoting) emotes.StopEmote();
        }

    }
}
