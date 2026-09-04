using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using UnityEngine;

namespace SomeEmotesREPO
{
    public class EmoteLoader : MonoBehaviour
    {
        private const string PreferencesFileName = "preferences.json";

        private static EmoteLoader? instance;
        public static EmoteLoader? Instance => instance;

        private AssetBundle? assetBundle;
        private Preferences preferences = new Preferences();
        private string preferencesPath = string.Empty;

        public static List<string> DisplayOrder()
        {
            return EmoteCatalog.DisplayOrder(instance?.preferences.farovites);
        }

        public static IList<string> Favourites()
        {
            return instance != null ? instance.preferences.farovites : new List<string>();
        }

        private void Awake()
        {
            instance = this;
        }

        private void Start()
        {
            string pluginFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Paths.PluginPath;

            assetBundle = LoadBundle(pluginFolder);

            preferencesPath = Path.Combine(pluginFolder, PreferencesFileName);
            LoadPreferences();
        }

        private static AssetBundle? LoadBundle(string pluginFolder)
        {
            string[] candidates =
            {
                Path.Combine(pluginFolder, "emotes.bundle"),
                Path.Combine(pluginFolder, "emotes"),
                Path.Combine(pluginFolder, "Emotes", "emotes.bundle"),
                Path.Combine(pluginFolder, "Emotes", "emotes"),
            };

            foreach (string path in candidates)
            {
                if (!File.Exists(path)) continue;

                var bundle = EmoteBundleLoader.Load(path);
                if (bundle != null)
                {
                    SomeEmotesREPO.Logger.LogInfo($"Emote bundle loaded from {path}.");
                    return bundle;
                }
            }

            SomeEmotesREPO.Logger.LogError(
                $"No emote bundle found in {pluginFolder}. Looked for 'emotes' and 'emotes.bundle', "
                + "at the top level and under Emotes/. No emote will be available.");
            return null;
        }

        public static Font? GetFont()
        {
            return instance?.assetBundle?.LoadAllAssets<Font>().FirstOrDefault(f => f.name.ToLower().Contains("teko-regular"));
        }

        private void LoadPreferences()
        {
            string json;
            try
            {
                json = File.ReadAllText(preferencesPath);
            }
            catch (System.Exception)
            {
                SomeEmotesREPO.Logger.LogInfo("No usable preferences file, creating one.");
                preferences = new Preferences();
                SavePreferences();
                return;
            }

            try
            {
                preferences = JsonUtility.FromJson<Preferences>(json) ?? new Preferences();
            }
            catch (System.Exception)
            {
                preferences = new Preferences();
            }

            Migrate(json);
        }

        private void Migrate(string json)
        {
            if (preferences.version >= Preferences.CurrentVersion) return;

            if (preferences.version == 1)
            {
                KeyCode chosen = KeyCode.None;
                try
                {
                    var legacy = JsonUtility.FromJson<LegacyPreferences>(json);
                    if (legacy != null) chosen = legacy.panelKey;
                }
                catch (System.Exception) { }

                string name = chosen.ToString();
                if (chosen != KeyCode.None && chosen != KeyCode.E
                    && SomeEmotesREPO.EmoteKey.Value == SomeEmotesREPO.DefaultEmoteKey
                    && System.Array.IndexOf(SomeEmotesREPO.BindableKeys, name) >= 0)
                {
                    SomeEmotesREPO.EmoteKey.Value = name;
                    SomeEmotesREPO.Logger.LogInfo(
                        $"Your [{name}] emote key moved out of {PreferencesFileName} and into the config, under 'Emote wheel / Key'.");
                }
            }

            preferences.version = Preferences.CurrentVersion;
            SavePreferences();
        }

        public void SavePreferences()
        {
            try
            {
                File.WriteAllText(preferencesPath, JsonUtility.ToJson(preferences));
            }
            catch (IOException e)
            {
                SomeEmotesREPO.Logger.LogWarning($"Could not save {PreferencesFileName}: {e.Message}");
            }
        }
        public void AddFavorites(List<string>? favorites)
        {
            if (favorites == null || favorites.Count == 0) return;

            var updated = new List<string>(preferences.farovites);
            bool changed = false;

            foreach (string name in favorites)
            {
                if (EmoteCatalog.IndexOf(name) < 0) continue;
                if (updated.Remove(name)) changed = true;
                else
                {
                    updated.Insert(0, name);
                    changed = true;
                }
            }
            if (!changed) return;

            int max = Mathf.Min(EmoteWheel.Slots, updated.Count);
            preferences.farovites = updated.GetRange(0, max);
            SavePreferences();
        }
    }
}

[System.Serializable]
public class Preferences
{
    public const int CurrentVersion = 2;
    public List<string> farovites = new List<string>();
    public int version;
}

[System.Serializable]
public class LegacyPreferences
{
    public KeyCode panelKey = KeyCode.None;
}
