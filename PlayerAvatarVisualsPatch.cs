using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SomeEmotesREPO
{
    [HarmonyPatch(typeof(PlayerAvatarVisuals), nameof(PlayerAvatarVisuals.Update))]
    static class PlayerAvatarVisualsUpdatePatch
    {
        static bool Prefix(PlayerAvatarVisuals __instance)
        {
            bool value;

            if (__instance.playerAvatar != null
                && __instance.playerAvatar.photonView.IsMine
                && EmoteSystem.Instance != null
                && EmoteSystem.Instance.IsEmoting)
            {
                value = false;
            }
            else value = true;
            return value;
        }
    }

    [HarmonyPatch(typeof(PlayerAvatarVisuals), nameof(PlayerAvatarVisuals.Revive))]
    static class PlayerAvatarVisualsRevivePatch
    {
        static void Prefix(PlayerAvatarVisuals __instance)
        {
            if (!EmoteSystem.Ready || EmoteSystem.Instance == null) return;
            if (!EmoteSystem.Instance.IsEmoting) return;

            EmoteSystem.Instance.StopEmote();
        }
    }
}



