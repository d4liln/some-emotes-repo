using System.Linq;
using System.Text;
using UnityEngine;

namespace SomeEmotesREPO
{
    public static class HierarchyLogger
    {
        public static void LogFullHierarchy(GameObject go)
        {
            Transform root = go.transform;
            while (root.parent != null)
                root = root.parent;

            var sb = new StringBuilder();
            BuildHierarchyString(root, 0, sb, go.transform);
            SomeEmotesREPO.Logger.LogInfo(sb.ToString());
        }

        private static void BuildHierarchyString(Transform t, int indent, StringBuilder sb, Transform initial)
        {
            sb.Append(' ', indent * 2);
            string is_initial = (t == initial) ? " [INITIAL GAME OBJECT]" : "";

            Component[] components = t.GetComponents<Component>();
            string componentList = string.Join(", ",
                components
                    .Where(c => c is not Transform)
                    .Select(c => c.GetType().Name));

            if (!string.IsNullOrEmpty(componentList))
                componentList = $" [{componentList}]";

            sb.AppendLine($"- {t.name}{is_initial}{componentList}");

            foreach (Transform child in t)
                BuildHierarchyString(child, indent + 1, sb, initial);
        }
    }
}

