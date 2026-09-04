using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SomeEmotesREPO;

[BepInPlugin("ImGogole.SomeEmotesREPO", "SomeEmotesREPO", "2.0.1")]
public class SomeEmotesREPO : BaseUnityPlugin
{
    internal static SomeEmotesREPO Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger => Instance._logger;
    private ManualLogSource _logger => base.Logger;
    internal Harmony? Harmony { get; set; }

    internal static ConfigEntry<string> EmoteKey { get; private set; } = null!;
    internal static ConfigEntry<float> WheelSensitivity { get; private set; } = null!;

    internal const string DefaultEmoteKey = "F";

    internal static KeyCode EmoteKeyCode { get; private set; } = KeyCode.F;

    internal static readonly string[] BindableKeys =
    {
        "A", "B", "C", "D","E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X","Y", "Z",
        "Alpha0", "Alpha1", "Alpha2", "Alpha3", "Alpha4",
        "Alpha5", "Alpha6", "Alpha7", "Alpha8", "Alpha9",
        "F1", "F2", "F3", "F4","F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
        "Space", "Return", "Backspace", "Tab", "CapsLock", "Escape",
        "LeftShift", "RightShift", "LeftControl", "RightControl", "LeftAlt", "RightAlt",
        "Insert", "Delete", "Home", "End","PageUp", "PageDown",
        "UpArrow", "DownArrow", "LeftArrow", "RightArrow",
        "BackQuote", "Minus", "Equals", "LeftBracket", "RightBracket", "Backslash",
        "Semicolon", "Quote","Comma", "Period", "Slash",
        "Keypad0", "Keypad1", "Keypad2", "Keypad3", "Keypad4",
        "Keypad5", "Keypad6", "Keypad7", "Keypad8", "Keypad9",
        "KeypadPeriod", "KeypadEnter", "KeypadPlus","KeypadMinus",
        "KeypadMultiply", "KeypadDivide",
        "Mouse2", "Mouse3", "Mouse4",
    };

    private void Awake()
    {
        Instance = this;

        this.gameObject.transform.parent = null;
        this.gameObject.hideFlags = HideFlags.HideAndDontSave;

        EmoteKey = Config.Bind(
            "Emote wheel", "Key", DefaultEmoteKey,
            new ConfigDescription(
                "Held down to open the emote wheel. F by default, within reach of the movement keys and clear of interact. Mouse2 is the middle button, Mouse3 and Mouse4 the side ones.",
                new AcceptableValueList<string>(BindableKeys)));

        ReadEmoteKey();
        EmoteKey.SettingChanged += (_, _) => ReadEmoteKey();

        WheelSensitivity = Config.Bind(
            "Emote wheel", "Sensitivity", 0.006f,
            "How far the wheel swings for a given mouse movement. Raise it if reaching the outer emotes takes too much desk.");

        if (!GetComponent<EmoteWheel>()) gameObject.AddComponent<EmoteWheel>();
        if (!GetComponent<EmoteLoader>()) gameObject.AddComponent<EmoteLoader>();
        if (!GetComponent<EmoteNetwork>()) gameObject.AddComponent<EmoteNetwork>();

        Patch();

        Logger.LogInfo($"{Info.Metadata.GUID} v{Info.Metadata.Version} has loaded!");
    }

    private static void ReadEmoteKey()
    {
        if (System.Enum.TryParse(EmoteKey.Value, ignoreCase: true, out KeyCode key) && key != KeyCode.None)
        {
            EmoteKeyCode = key;
            return;
        }

        Logger.LogWarning($"'{EmoteKey.Value}' is not a key this can bind. Falling back to [{DefaultEmoteKey}].");
        EmoteKeyCode = KeyCode.F;
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