using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SomeEmotesREPO
{
    public static class EmoteCatalog
    {
        private static readonly List<string> AllNames = new List<string>();
        private static readonly Dictionary<string, int> Indices = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly List<AnimationClip> Clips = new List<AnimationClip>();

        public static bool Loaded { get; private set; }

        public static int Count => AllNames.Count;

        public static IList<string> Names => AllNames;

        public static string Signature { get; private set; } = string.Empty;

        public static void Load()
        {
            if (Loaded) return;

            var paths = EmoteBundleLoader.GetAllAnimNames();
            if (paths == null || paths.Count == 0)
            {
                SomeEmotesREPO.Logger.LogError("[Catalog] No emote clip found in the bundle.");
                return;
            }

            var byName = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (string path in paths)
            {
                string name = ExtractName(path);
                if (name.Length == 0) continue;
                byName[name] = path;
            }

            foreach (var entry in byName)
            {
                var clip = EmoteBundleLoader.LoadAsset<AnimationClip>(entry.Value);
                if (clip == null)
                {
                    SomeEmotesREPO.Logger.LogWarning($"[Catalog] '{entry.Key}' could not be loaded, skipped.");
                    continue;
                }

                // Loop Time left off in the import settings makes a dance play once and
                // then hold its last frame, which on screen is a player standing oddly
                // still. Nothing here can fix it, since looping is baked at import, but
                // naming the clip turns a puzzling emote into a one-line answer.
                // Generic clips drive nothing on a humanoid avatar, silently.
                if (!clip.isHumanMotion)
                {
                    SomeEmotesREPO.Logger.LogWarning(
                        $"[Catalog] '{entry.Key}' is not a humanoid clip, so it cannot drive the dancer. "
                        + "Set the FBX rig to Humanoid and rebuild the bundle.");
                }

                if (!clip.isLooping)
                {
                    SomeEmotesREPO.Logger.LogWarning(
                        $"[Catalog] '{entry.Key}' is not set to loop, so it will play once and freeze. " +
                        "Tick Loop Time on the clip and rebuild the bundle.");
                }

                Indices[entry.Key] = AllNames.Count;
                AllNames.Add(entry.Key);
                Clips.Add(clip);
            }

            Signature = ComputeSignature();
            Loaded = AllNames.Count > 0;
            SomeEmotesREPO.Logger.LogInfo($"[Catalog] {AllNames.Count} emotes loaded, signature {Signature}.");
        }

        public static bool IsValid(int index) => index >= 0 && index < AllNames.Count;

        public static string NameAt(int index) => IsValid(index) ? AllNames[index] : string.Empty;

        public static AnimationClip? ClipAt(int index) => IsValid(index) ? Clips[index] : null;

        public static int IndexOf(string name) => Indices.TryGetValue(name, out int index) ? index : -1;
        private static string ComputeSignature()
        {
            const uint Offset = 2166136261;
            const uint Prime = 16777619;

            uint hash = Offset;
            foreach (string name in AllNames)
            {
                foreach (char c in name)
                {
                    hash = (hash ^ c) * Prime;
                }
                hash = (hash ^ '\n') * Prime;
            }
            return AllNames.Count.ToString() + ":" + hash.ToString("x8");
        }

        public static string ExtractName(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;

            string name = path.Trim();
            int slash = name.LastIndexOf('/');
            if (slash >= 0) name = name.Substring(slash + 1);

            if (name.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(0, name.Length - ".anim".Length);
            }
            return name;
        }

        public static List<string> DisplayOrder(IList<string>? favourites)
        {
            var ordered = new List<string>(AllNames.Count);
            if (favourites != null)
            {
                foreach (string favourite in favourites)
                {
                    if (Indices.ContainsKey(favourite) && !ordered.Contains(favourite)) ordered.Add(favourite);
                }
            }
            foreach (string name in AllNames)
            {
                if (!ordered.Contains(name)) ordered.Add(name);
            }
            return ordered;
        }
    }
}
