using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Unity.Mono;
using BepInEx.Unity.Mono.Bootstrap;
using UnityEngine;

namespace DevMenu
{
    /// <summary>
    /// Hot reload for mods in development. Plugins in <c>BepInEx\scripts</c> are not seen by BepInEx's loader; we load them
    /// ourselves, each on its own GameObject, from the dll's bytes (so the file isn't locked and a rebuilt dll is a new assembly).
    /// Reload destroys the old plugin at once (its <c>OnDestroy</c> must undo its patches and UI) and loads the new build.
    /// Mono can't unload assemblies, so old copies stay in memory until the game quits: fine for development.
    /// </summary>
    internal sealed class ScriptLoader : MonoBehaviour
    {
        private sealed class Loaded
        {
            public string File;
            public string Guid;
            public string Name;
            public GameObject Holder;
            public DateTime Built;
        }

        private static readonly List<Loaded> loaded = new List<Loaded>();

        /// <summary>Bumped on every load, so the Mods tab knows to scan the plugins again.</summary>
        public static int Version { get; private set; }

        public static string Folder => Path.Combine(Paths.BepInExRootPath, "scripts");

        public static IEnumerable<(string Name, string File, DateTime Built)> Plugins =>
            loaded.Select(l => (l.Name, Path.GetFileName(l.File), l.Built));

        // Start, not Awake: by then the chainloader has created every normal plugin.
        private void Start() => Menu.Run("Load scripts", LoadAll);

        private void Update()
        {
            if (Input.GetKeyDown(Plugin.ReloadKey.Value))
                ReloadAll();
        }

        public static void ReloadAll() => Menu.Run("Reload scripts", () =>
        {
            int count = UnloadAll();
            LoadAll();
            Menu.Notify($"Scripts reloaded: {loaded.Count} plugin(s)" + (count != loaded.Count ? $" (was {count})" : ""));
        });

        private static int UnloadAll()
        {
            int count = loaded.Count;
            // Last loaded first, in case one builds on another.
            for (int i = loaded.Count - 1; i >= 0; i--)
            {
                Loaded l = loaded[i];
                try
                {
                    // At once, not at the end of the frame: the new copy patches the same methods right after.
                    if (l.Holder != null)
                        DestroyImmediate(l.Holder);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Unloading {l.Name} failed (its OnDestroy): {e}");
                }
                UnityChainloader.Instance.Plugins.Remove(l.Guid);
                RemoveLogSource(l.Name);
                Plugin.Log.LogInfo($"Unloaded {l.Name}");
            }
            loaded.Clear();
            return count;
        }

        private static void LoadAll()
        {
            Directory.CreateDirectory(Folder);
            foreach (string file in Directory.GetFiles(Folder, "*.dll", SearchOption.AllDirectories).OrderBy(f => f))
            {
                try
                {
                    Load(file);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Loading {file} failed: {e}");
                    Menu.Notify($"Loading {Path.GetFileName(file)} failed: {e.Message}");
                }
            }
            Version++;
        }

        private static void Load(string file)
        {
            string pdb = Path.ChangeExtension(file, ".pdb");
            Assembly assembly = File.Exists(pdb)
                ? Assembly.Load(File.ReadAllBytes(file), File.ReadAllBytes(pdb))
                : Assembly.Load(File.ReadAllBytes(file));

            foreach (Type type in SafeTypes(assembly).Where(t => !t.IsAbstract && typeof(BaseUnityPlugin).IsAssignableFrom(t)))
            {
                BepInPlugin meta = type.GetCustomAttribute<BepInPlugin>();
                if (meta == null)
                    continue;
                if (UnityChainloader.Instance.Plugins.ContainsKey(meta.GUID))
                {
                    Plugin.Log.LogWarning($"{meta.Name} is already loaded from plugins; remove it from there to reload it from scripts");
                    continue;
                }

                var holder = new GameObject($"Script {meta.Name}");
                DontDestroyOnLoad(holder);
                var plugin = (BaseUnityPlugin)holder.AddComponent(type); // Awake runs here
                UnityChainloader.Instance.Plugins[meta.GUID] = plugin.Info;
                loaded.Add(new Loaded { File = file, Guid = meta.GUID, Name = meta.Name, Holder = holder, Built = File.GetLastWriteTime(file) });
                Plugin.Log.LogInfo($"Loaded {meta.Name} from scripts\\{Path.GetFileName(file)}");
            }
        }

        private static IEnumerable<Type> SafeTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null);
            }
        }

        // Every plugin instance makes a log source with its name; drop the old one so lines aren't printed twice.
        private static void RemoveLogSource(string name)
        {
            foreach (BepInEx.Logging.ILogSource source in BepInEx.Logging.Logger.Sources.Where(s => s.SourceName == name).ToList())
            {
                BepInEx.Logging.Logger.Sources.Remove(source);
                source.Dispose();
            }
        }
    }
}
