using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace DevMenu
{
    /// <summary>
    /// Finds which scene components point at an object through their fields: scene logic in this game is often wired
    /// in prefabs (relays, outputs, animator triggers), not in code. Reflection over every behaviour in the scene, so it takes a moment.
    /// </summary>
    internal static class References
    {
        private const int MaxDepth = 3;
        private const int MaxLines = 60;

        private static readonly Dictionary<Type, FieldInfo[]> fieldCache = new Dictionary<Type, FieldInfo[]>();

        public static void Describe(StringBuilder s, GameObject go)
        {
            var targets = new HashSet<GameObject> { go };

            var hits = new List<string>();
            foreach (MonoBehaviour b in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (b == null || hits.Count >= MaxLines)
                    continue;
                // The object's own components are listed above; what matters is who reaches in from outside.
                if (b.gameObject == go)
                    continue;
                try
                {
                    Scan(b, b.GetType().Name, 0, targets, hits, new HashSet<object>(), b);
                }
                catch
                {
                    // a getter-backed or odd field: skip this behaviour
                }
            }

            s.AppendLine("Referenced by:" + (hits.Count == 0 ? " nothing found" : ""));
            foreach (string h in hits.Distinct())
                s.AppendLine("  " + h);
        }

        private static void Scan(object obj, string path, int depth, HashSet<GameObject> targets, List<string> hits, HashSet<object> seen, MonoBehaviour owner)
        {
            if (obj == null || depth > MaxDepth || hits.Count >= MaxLines || !seen.Add(obj))
                return;
            foreach (FieldInfo f in Fields(obj.GetType()))
            {
                object value;
                try
                {
                    value = f.GetValue(obj);
                }
                catch
                {
                    continue;
                }
                Check(value, $"{path}.{f.Name}", depth, targets, hits, seen, owner);
            }
        }

        private static void Check(object value, string path, int depth, HashSet<GameObject> targets, List<string> hits, HashSet<object> seen, MonoBehaviour owner)
        {
            switch (value)
            {
                case null:
                    return;
                case GameObject g:
                    if (g != null && targets.Contains(g))
                        hits.Add($"{Where(owner)}  {path} → {g.name}");
                    return;
                case Component c:
                    if (c != null && targets.Contains(c.gameObject))
                        hits.Add($"{Where(owner)}  {path} → {c.GetType().Name} on {c.gameObject.name}");
                    return;
                case UnityEngine.Object _:
                    return; // assets and other engine objects: not followed
                case string _:
                    return;
                case IEnumerable list when !(value is IDictionary):
                    int i = 0;
                    foreach (object item in list)
                    {
                        if (i++ > 200)
                            break;
                        Check(item, $"{path}[{i - 1}]", depth + 1, targets, hits, seen, owner);
                    }
                    return;
            }
            Type type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || !type.IsClass)
                return;
            Scan(value, path, depth + 1, targets, hits, seen, owner);
        }

        private static FieldInfo[] Fields(Type type)
        {
            if (fieldCache.TryGetValue(type, out FieldInfo[] fields))
                return fields;
            var list = new List<FieldInfo>();
            for (Type t = type; t != null && t != typeof(MonoBehaviour) && t != typeof(object); t = t.BaseType)
                list.AddRange(t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Where(f => !f.FieldType.IsPrimitive && f.FieldType != typeof(string) && !f.FieldType.IsEnum));
            fields = list.ToArray();
            fieldCache[type] = fields;
            return fields;
        }

        private static string Where(MonoBehaviour b)
        {
            var parts = new List<string>();
            for (Transform t = b.transform; t != null && parts.Count < 4; t = t.parent)
                parts.Add(t.name);
            parts.Reverse();
            return $"{b.GetType().Name} on …/{string.Join("/", parts)}" + (b.gameObject.activeInHierarchy ? "" : " (inactive)");
        }
    }
}
