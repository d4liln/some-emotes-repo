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

        public static KeyCode PanelKey => instance != null ? instance.preferences.panelKey : KeyCode.P;

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

        /// <summary>
        /// Finds the emote bundle wherever the Unity build happened to put it.
        ///
        /// Unity names the built file after the bundle, with no extension, so a bundle
        /// called "emotes" ships as "emotes" and one called "emotes.bundle" ships as
        /// "emotes.bundle". Both spellings have shipped with this mod. Trying the four
        /// combinations costs nothing and turns a rebuild that silently produces a mod
        /// with no emotes at all into a non-event.
        /// </summary>
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
            try
            {
                preferences = JsonUtility.FromJson<Preferences>(File.ReadAllText(preferencesPath)) ?? new Preferences();
                Migrate();
            }
            catch (System.Exception)
            {
                SomeEmotesREPO.Logger.LogInfo("No usable preferences file, creating one.");
                preferences = new Preferences();
                SavePreferences();
            }
        }
        private void Migrate()
        {
            if (preferences.version >= Preferences.CurrentVersion) return;

            KeyCode previous = preferences.panelKey;
            preferences.panelKey = new Preferences().panelKey;
            preferences.version = Preferences.CurrentVersion;
            SavePreferences();

            SomeEmotesREPO.Logger.LogInfo(
                $"Emote key moved from [{previous}] to [{preferences.panelKey}]. Change it in {PreferencesFileName} if you prefer the old one.");
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
    public const int CurrentVersion = 1;
    public List<string> farovites = new List<string>();
    public KeyCode panelKey = KeyCode.E;
    public int version;
}
