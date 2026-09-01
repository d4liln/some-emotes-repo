using System;
using System.Collections.Generic;
using UnityEngine;

namespace SomeEmotesREPO
{
    public static class EmoteBundleLoader
    {
        private static AssetBundle? emoteBundle;

        public static bool Loaded => emoteBundle != null;

        public static AssetBundle? Load(string path)
        {
            if (emoteBundle != null) return emoteBundle;

            emoteBundle = AssetBundle.LoadFromFile(path);
            if (emoteBundle == null)
            {
                SomeEmotesREPO.Logger.LogWarning($"No emote bundle at {path}.");
            }
            return emoteBundle;
        }

        public static void Unload()
        {
            if (emoteBundle == null) return;
            emoteBundle.Unload(true);
            emoteBundle = null;
        }

        public static T? LoadAsset<T>(string name) where T : UnityEngine.Object
        {
            if (emoteBundle == null) return null;

            foreach (string path in emoteBundle.GetAllAssetNames())
            {
                if (path.EndsWith(name, StringComparison.OrdinalIgnoreCase))
                {
                    return emoteBundle.LoadAsset<T>(path);
                }
            }
            return null;
        }

        public static List<string> GetAllAnimNames()
        {
            var result = new List<string>();
            if (emoteBundle == null) return result;

            foreach (string name in emoteBundle.GetAllAssetNames())
            {
                if (name.EndsWith(".anim", StringComparison.OrdinalIgnoreCase)) result.Add(name);
            }
            return result;
        }
    }
}
