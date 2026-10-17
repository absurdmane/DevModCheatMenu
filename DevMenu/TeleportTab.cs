using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using BepInEx;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DevMenu
{
    /// <summary>
    /// Named teleport points per scene, kept in BepInEx\config\obenseuer.absurdmane.devmenu.teleports.txt, and travel to other scenes.
    /// Travel goes through the game's own level change (<c>SaveController.ChangeLevel</c>: autosave, loading screen) to an entrypoint.
    /// Entrypoint names are only known for a loaded scene, so they are learned as scenes are visited (the scene's own entrypoints and
    /// the ones its doors lead to) and kept in ...devmenu.locations.txt. An unknown entrypoint name makes the game log
    /// "Entrypoint ... not found!" and spawn the player at the scene's default entrypoint, which is how unvisited scenes are reached.
    /// A point in another scene: travel there, then move to the point once loading is done.
    /// </summary>
    internal sealed class TeleportTab : Tab
    {
        public override string Title => "Teleport";

        private sealed class Point
        {
            public string Scene;
            public string Name;
            public Vector3 Position;
            public float Yaw;
        }

        private static string FilePath => Path.Combine(Paths.ConfigPath, Plugin.Guid + ".teleports.txt");

        private static string LocationsPath => Path.Combine(Paths.ConfigPath, Plugin.Guid + ".locations.txt");

        // Not an entrypoint of any scene: the game falls back to the scene's default spawn.
        private const string DefaultEntrypoint = "DevMenuDefaultSpawn";
        // After LoadingDone the game moves the player to the entrypoint 0.1 s later (realtime); go to the point after that.
        private const float PointDelay = 0.5f;

        private List<Point> points;
        private string newName = "";

        // Scene -> entrypoint names.
        private Dictionary<string, SortedSet<string>> locations;
        private string learnedScene;
        private readonly HashSet<string> expanded = new HashSet<string>();
        private bool showUnvisited;

        private Point pending;
        private float pendingAt = -1f;

        public TeleportTab()
        {
            SaveController.LoadingDone += () =>
            {
                learnedScene = null;
                if (pending != null)
                    pendingAt = Time.realtimeSinceStartup + PointDelay;
            };
        }

        public override void Tick()
        {
            if (SaveController.Loading)
                return;
            string scene = SceneManager.GetActiveScene().name;
            if (scene != learnedScene && PlayerLevelEntrypoints.instance != null)
            {
                learnedScene = scene;
                Learn(scene);
            }
            if (pending == null || pendingAt < 0f || Time.realtimeSinceStartup < pendingAt)
                return;
            Point p = pending;
            pending = null;
            pendingAt = -1f;
            if (p.Scene != scene)
                Menu.Notify($"Travel to {p.Scene} ended in {scene}; not moving to {p.Name}");
            else if (PlayerLocator.instance != null)
                Teleport(PlayerLocator.instance, p);
        }

        public override void Draw()
        {
            if (points == null)
                points = Load();

            PlayerLocator player = PlayerLocator.instance;
            if (player == null)
            {
                GUILayout.Label("No player in this scene.");
                return;
            }
            string scene = SceneManager.GetActiveScene().name;
            GUILayout.Label($"Scene: {scene}   Position: {player.transform.position:F1}");

            GUILayout.BeginHorizontal();
            Gui.InlineLabel("Name");
            newName = GUILayout.TextField(newName, Gui.Fit("A point name", 200, GUI.skin.textField));
            if (GUILayout.Button("Save point here", GUILayout.ExpandWidth(false)))
                Menu.Run("Save point", () =>
                {
                    string name = string.IsNullOrWhiteSpace(newName) ? $"Point {points.Count(p => p.Scene == scene) + 1}" : newName.Trim();
                    points.RemoveAll(p => p.Scene == scene && p.Name == name);
                    points.Add(new Point { Scene = scene, Name = name, Position = player.transform.position, Yaw = player.transform.eulerAngles.y });
                    Save();
                    newName = "";
                    Menu.Notify($"Saved point {name}");
                });
            GUILayout.EndHorizontal();

            Gui.Heading("Points in this scene");
            DrawPoints(points.Where(p => p.Scene == scene).ToList(), p => Teleport(player, p));
            List<Point> elsewhere = points.Where(p => p.Scene != scene).OrderBy(p => p.Scene).ToList();
            if (elsewhere.Count > 0)
            {
                Gui.Heading("Points in other scenes (loads the scene, autosaves)");
                DrawPoints(elsewhere, TravelToPoint);
            }

            DrawLocations(scene);
        }

        private void DrawPoints(List<Point> list, Action<Point> go)
        {
            string scene = SceneManager.GetActiveScene().name;
            foreach (Point p in list)
            {
                GUILayout.BeginHorizontal();
                string where = p.Scene == scene ? "" : $"{p.Scene}: ";
                if (GUILayout.Button($"{where}{p.Name}   {p.Position:F1}", Gui.Row))
                    Menu.Run("Teleport", () => go(p));
                if (GUILayout.Button("Delete", Gui.Fit("Delete", 70)))
                {
                    points.Remove(p);
                    Save();
                }
                GUILayout.EndHorizontal();
            }
        }

        private void DrawLocations(string scene)
        {
            if (locations == null)
                locations = LoadLocations();
            Gui.Heading("Locations (learned entrypoints; loads the scene, autosaves)");
            foreach (KeyValuePair<string, SortedSet<string>> kv in locations.OrderBy(kv => kv.Key).ToList())
            {
                bool open = expanded.Contains(kv.Key);
                string here = kv.Key == scene ? "  (here)" : "";
                if (GUILayout.Button($"{(open ? "▼" : "►")}  <b>{kv.Key}</b>  ({kv.Value.Count}){here}", Gui.Header))
                {
                    if (open)
                        expanded.Remove(kv.Key);
                    else
                        expanded.Add(kv.Key);
                }
                if (!open)
                    continue;
                foreach (string entry in kv.Value)
                    if (GUILayout.Button("      " + entry, Gui.Row))
                        Menu.Run("Travel", () => Travel(kv.Key, entry));
            }

            List<string> unvisited = BuildScenes().Where(s => !locations.ContainsKey(s)).ToList();
            if (unvisited.Count == 0)
                return;
            if (GUILayout.Button($"{(showUnvisited ? "▼" : "►")}  <b>Other scenes in the build</b>  ({unvisited.Count}; default spawn, may be menus or test scenes)", Gui.Header))
                showUnvisited = !showUnvisited;
            if (showUnvisited)
                foreach (string s in unvisited)
                    if (GUILayout.Button("      " + s, Gui.Row))
                        Menu.Run("Travel", () => Travel(s, DefaultEntrypoint));
        }

        private static IEnumerable<string> BuildScenes()
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
                yield return Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
        }

        private void TravelToPoint(Point p)
        {
            if (locations == null)
                locations = LoadLocations();
            string entry = locations.TryGetValue(p.Scene, out SortedSet<string> known) && known.Count > 0 ? known.First() : DefaultEntrypoint;
            if (Travel(p.Scene, entry))
                pending = p;
        }

        private bool Travel(string scene, string entrypoint)
        {
            if (SaveController.Loading)
            {
                Menu.Notify("Already loading");
                return false;
            }
            if (!SaveController.CheckIfLevelExists(scene))
            {
                Menu.Notify($"No scene {scene} in the build");
                return false;
            }
            pending = null;
            pendingAt = -1f;
            PlayerLevelEntrypoints.Entrypoint here = scene == SceneManager.GetActiveScene().name && PlayerLevelEntrypoints.instance != null
                ? PlayerLevelEntrypoints.instance.GetEntrypoint(entrypoint)
                : null;
            if (here != null)
            {
                here.TeleportPlayer();
                Menu.Notify($"Teleported to {entrypoint}");
                return false;
            }
            Menu.Instance?.Close();
            Menu.Notify($"Travelling to {scene} / {entrypoint}");
            SaveController.ChangeLevel(scene, entrypoint);
            return true;
        }

        /// <summary>The loaded scene's entrypoints, and where its doors lead.</summary>
        private void Learn(string scene)
        {
            if (locations == null)
                locations = LoadLocations();
            bool changed = false;
            foreach (PlayerLevelEntrypoints.Entrypoint e in PlayerLevelEntrypoints.instance.Entrypoints)
                if (e != null && !string.IsNullOrEmpty(e.Name) && e.Name != "UnderMapDrop" && e.Location != null)
                    changed |= Add(scene, e.Name);
            foreach (Changelevel door in UnityEngine.Object.FindObjectsByType<Changelevel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!string.IsNullOrEmpty(door.OtherLevel) && !string.IsNullOrEmpty(door.OtherEntrypoint) && door.OtherEntrypoint != "NONE")
                    changed |= Add(door.OtherLevel, door.OtherEntrypoint);
            if (changed)
                File.WriteAllLines(LocationsPath, locations.OrderBy(kv => kv.Key).SelectMany(kv => kv.Value.Select(e => kv.Key + "|" + e)));
        }

        private bool Add(string scene, string entry)
        {
            if (!locations.TryGetValue(scene, out SortedSet<string> set))
                locations[scene] = set = new SortedSet<string>(StringComparer.Ordinal);
            return set.Add(entry);
        }

        private static Dictionary<string, SortedSet<string>> LoadLocations()
        {
            var result = new Dictionary<string, SortedSet<string>>();
            if (!File.Exists(LocationsPath))
                return result;
            foreach (string line in File.ReadAllLines(LocationsPath))
            {
                string[] f = line.Split('|');
                if (f.Length != 2)
                    continue;
                if (!result.TryGetValue(f[0], out SortedSet<string> set))
                    result[f[0]] = set = new SortedSet<string>(StringComparer.Ordinal);
                set.Add(f[1]);
            }
            return result;
        }

        private static void Teleport(PlayerLocator player, Point p)
        {
            Transform t = player.transform;
            t.position = p.Position;
            t.eulerAngles = new Vector3(t.eulerAngles.x, p.Yaw, t.eulerAngles.z);
            Rigidbody body = player.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.position = p.Position;
                body.rotation = t.rotation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            Menu.Notify($"Teleported to {p.Name}");
        }

        private static List<Point> Load()
        {
            var result = new List<Point>();
            if (!File.Exists(FilePath))
                return result;
            foreach (string line in File.ReadAllLines(FilePath))
            {
                string[] f = line.Split('|');
                if (f.Length != 6)
                    continue;
                try
                {
                    result.Add(new Point
                    {
                        Scene = f[0],
                        Name = f[1],
                        Position = new Vector3(Parse(f[2]), Parse(f[3]), Parse(f[4])),
                        Yaw = Parse(f[5])
                    });
                }
                catch (FormatException)
                {
                    Plugin.Log.LogWarning($"Bad teleport line: {line}");
                }
            }
            return result;
        }

        private void Save()
        {
            File.WriteAllLines(FilePath, points.Select(p => string.Join("|",
                p.Scene, p.Name.Replace("|", "/"), Format(p.Position.x), Format(p.Position.y), Format(p.Position.z), Format(p.Yaw))));
        }

        private static float Parse(string s) => float.Parse(s, CultureInfo.InvariantCulture);
        private static string Format(float f) => f.ToString("R", CultureInfo.InvariantCulture);
    }
}
