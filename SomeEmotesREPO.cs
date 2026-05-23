using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace SomeEmotesREPO;

[BepInPlugin("ImGogole.SomeEmotesREPO", "SomeEmotesREPO", "1.0.7")]
public class SomeEmotesREPO : BaseUnityPlugin
{
    internal static SomeEmotesREPO Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger => Instance._logger;
    private ManualLogSource _logger => base.Logger;
    internal Harmony? Harmony { get; set; }

    private void Awake()
    {
        Instance = this;

        this.gameObject.transform.parent = null;
        this.gameObject.hideFlags = HideFlags.HideAndDontSave;

        if (!GetComponent<EmoteSelectionManager>()) gameObject.AddComponent<EmoteSelectionManager>();
        if (!GetComponent<EmoteLoader>()) gameObject.AddComponent<EmoteLoader>();

        Patch();

        Logger.LogInfo($"{Info.Metadata.GUID} v{Info.Metadata.Version} has loaded!");
    }

    internal void Patch()
    {
        Harmony ??= new Harmony(Info.Metadata.GUID);
        Harmony.PatchAll();
    }

    internal void Unpatch()
    {
        Harmony?.UnpatchSelf();
    }
}