using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SomeEmotesREPO;

[BepInPlugin("ImGogole.SomeEmotesREPO", "SomeEmotesREPO", "2.0.0")]
public class SomeEmotesREPO : BaseUnityPlugin
{
    internal static SomeEmotesREPO Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger => Instance._logger;
    private ManualLogSource _logger => base.Logger;
    internal Harmony? Harmony { get; set; }

    internal static ConfigEntry<bool> RigProbeEnabled { get; private set; } = null!;
    internal static ConfigEntry<KeyCode> RigProbeKey { get; private set; } = null!;
    internal static ConfigEntry<float> WheelSensitivity { get; private set; } = null!;

    private void Awake()
    {
        Instance = this;

        this.gameObject.transform.parent = null;
        this.gameObject.hideFlags = HideFlags.HideAndDontSave;

        RigProbeEnabled = Config.Bind(
            "Debug", "RigProbe", false,
            "Enables the rig probe: an in-game tool that binds the real player avatar bones and forces a pose onto them. Used to develop the clone-free emote system.");
        RigProbeKey = Config.Bind(
            "Debug", "RigProbeKey", KeyCode.F8,
            "Opens and closes the rig probe overlay.");

        WheelSensitivity = Config.Bind(
            "Wheel", "Sensitivity", 0.006f,
            "How far the emote wheel swings for a given mouse movement. Raise it if reaching the outer emotes takes too much desk.");

        if (!GetComponent<EmoteWheel>()) gameObject.AddComponent<EmoteWheel>();
        if (!GetComponent<EmoteLoader>()) gameObject.AddComponent<EmoteLoader>();
        if (!GetComponent<EmoteNetwork>()) gameObject.AddComponent<EmoteNetwork>();
        if (!GetComponent<Rig.RepoRigProbe>()) gameObject.AddComponent<Rig.RepoRigProbe>();

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