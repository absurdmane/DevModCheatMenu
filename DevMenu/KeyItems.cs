using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace DevMenu
{
    /// <summary>
    /// Every key/quest item (passport, ...). In the release build they are Addressables, not Resources (the console's
    /// <c>Resources.LoadAll&lt;TaskItem&gt;("TaskItems")</c> finds nothing), so they are found through the Addressables catalog.
    /// Loaded once per list reload; that takes a moment.
    /// </summary>
    internal static class KeyItems
    {
        public static IEnumerable<TaskItem> All()
        {
            var result = new HashSet<TaskItem>();
            result.UnionWith(Resources.FindObjectsOfTypeAll<TaskItem>());
            result.UnionWith(Resources.LoadAll<TaskItem>("TaskItems"));

            var keys = new HashSet<string>();
            foreach (var locator in Addressables.ResourceLocators)
                foreach (object key in locator.Keys)
                    if (key is string k && locator.Locate(k, typeof(TaskItem), out IList<IResourceLocation> found) && found.Count > 0)
                        keys.Add(found[0].PrimaryKey);
            foreach (string key in keys)
            {
                try
                {
                    TaskItem item = Addressables.LoadAssetAsync<TaskItem>(key).WaitForCompletion();
                    if (item != null)
                        result.Add(item);
                }
                catch (System.Exception e)
                {
                    Plugin.Log.LogWarning($"Key item {key}: {e.Message}");
                }
            }
            Plugin.Log.LogInfo($"Key items found: {result.Count} ({keys.Count} in the Addressables catalog)");
            // Several assets can share a name (copies loaded from saves); keep one per name.
            return result.Where(t => t != null).GroupBy(t => t.name).Select(g => g.First());
        }
    }
}
