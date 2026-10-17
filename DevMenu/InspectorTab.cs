using System;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using UnityEngine;

namespace DevMenu
{
    /// <summary>
    /// What's under the crosshair: the object, its components and collider, and which scene components reference it.
    /// The Inspect key (menu closed; none by default) captures the target, shows it and writes it (only the latest) to BepInEx\DevMenu\inspect-latest.txt.
    /// </summary>
    internal sealed class InspectorTab : Tab
    {
        public override string Title => "Inspector";

        private static string FilePath => Path.Combine(Paths.BepInExRootPath, "DevMenu", "inspect-latest.txt");

        private GameObject target;
        private string report = "Nothing captured yet. Close the menu, look at an object and press the Inspect key.";
        private Vector2 overlayScroll;
        private float overlayUntil;

        // The interaction raycast stops while the menu is open, so take the target as the menu opens.
        // No reference scan here: it takes a moment, and opening the menu should be instant.
        public override void OnOpen() => Capture(saveToFile: false, deep: false);

        public void CaptureAndSave()
        {
            Capture(saveToFile: true, deep: true);
            overlayUntil = Time.unscaledTime + 8f;
        }

        private void Capture(bool saveToFile, bool deep)
        {
            GameObject now = InteractObjects.instance != null ? InteractObjects.instance.currentGameObject : null;
            if (now != null)
                target = now;
            report = target == null ? "Nothing under the crosshair." : Describe(target, deep);
            if (saveToFile)
                Save();
        }

        /// <summary>
        /// A click on the game view (outside the menu) while this tab is open: selects the first solid collider under the mouse,
        /// or a trigger if there's nothing solid. <paramref name="guiPoint"/> is in GUI coordinates (y down).
        /// </summary>
        public void PickAt(Vector2 guiPoint)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                Menu.Notify("No camera to pick with");
                return;
            }
            Ray ray = camera.ScreenPointToRay(new Vector3(guiPoint.x, Screen.height - guiPoint.y, 0f));
            // The camera is behind the player: skip the player's own colliders.
            // Not .root: the player may sit under a scene root that holds everything.
            Transform player = PlayerStats.instance != null ? PlayerStats.instance.transform : null;
            RaycastHit[] hits = Physics.RaycastAll(ray, 500f, ~0, QueryTriggerInteraction.Collide)
                .Where(h => player == null || !h.collider.transform.IsChildOf(player))
                .ToArray();
            if (hits.Length == 0)
            {
                Menu.Notify("Nothing there");
                return;
            }
            RaycastHit hit = hits.OrderBy(h => h.collider.isTrigger ? 1 : 0).ThenBy(h => h.distance).First();
            Select(hit.collider.gameObject);
            Menu.Notify($"Selected {hit.collider.name} ({hit.distance:0.#} m)");
        }

        private void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(FilePath, $"===== {DateTime.Now:yyyy-MM-dd HH:mm:ss} =====\n{report}\n\n");
            Menu.Notify($"Inspected {(target != null ? target.name : "nothing")} → inspect-latest.txt");
        }

        public override void Draw()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh", GUILayout.ExpandWidth(false)))
                Menu.Run("Inspect", () => Capture(saveToFile: false, deep: true));
            if (GUILayout.Button("Save to file", GUILayout.ExpandWidth(false)))
                Menu.Run("Inspect", Save);
            GUILayout.EndHorizontal();
            GUILayout.Label("Click an object on the screen (outside this window) to select it.");
            GUILayout.Label(Plugin.InspectKey.Value == KeyCode.None
                ? "Set Keys.Inspect in the DevMenu settings (Mods tab) to capture what you look at without opening the menu."
                : $"Close the menu, look at something, press {Plugin.InspectKey.Value}: it's captured and saved without opening the menu.");

            if (target != null)
                DrawHierarchy();
            showReport = GUILayout.Toggle(showReport, showReport ? "▼ Details (components, materials, references)" : "► Details (components, materials, references)", GUI.skin.button);
            if (showReport)
                GUILayout.TextArea(report);
        }

        private bool showReport = true;

        // ---- hierarchy browser: parents and children, each a button that selects it ----

        private const int MaxChildren = 200;

        private GameObject listedFor;
        private Transform[] parents = new Transform[0];
        private Transform[] children = new Transform[0];
        private void Select(GameObject go)
        {
            target = go;
            report = Describe(go, deep: false);
        }

        private void BuildLists()
        {
            listedFor = target;
            var up = new System.Collections.Generic.List<Transform>();
            for (Transform t = target.transform.parent; t != null; t = t.parent)
                up.Add(t);
            up.Reverse();
            parents = up.ToArray();
            children = target.transform.Cast<Transform>().Take(MaxChildren).ToArray();
        }

        private void DrawHierarchy()
        {
            if (listedFor != target)
                BuildLists();

            GUILayout.Label("Parents (top of the scene first; click to select):");
            for (int i = 0; i < parents.Length; i++)
                if (parents[i] != null && Row(parents[i], i * 12f))
                {
                    Select(parents[i].gameObject);
                    return;
                }

            GUILayout.Label($"► Selected: {target.name}   {Tags(target.transform)}");

            GUILayout.Label(children.Length == 0 ? "Children: none" : $"Children ({target.transform.childCount}):");
            foreach (Transform child in children)
                if (child != null && Row(child, 12f))
                {
                    Select(child.gameObject);
                    return;
                }
        }

        private static GUIStyle leftButton;

        private static GUIStyle LeftButton => leftButton ?? (leftButton = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, wordWrap = true });

        private static bool Row(Transform t, float indent)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(indent);
            bool clicked = GUILayout.Button($"{t.name}   {Tags(t)}", LeftButton);
            GUILayout.EndHorizontal();
            return clicked;
        }

        /// <summary>Short marks for what an object is: light, furniture, item, mesh, inactive, child count.</summary>
        private static string Tags(Transform t)
        {
            var tags = new System.Collections.Generic.List<string>();
            Light light = t.GetComponent<Light>();
            if (light != null)
                tags.Add(light.enabled && t.gameObject.activeInHierarchy ? "[LIGHT on]" : "[LIGHT off]");
            if (t.GetComponent<LightController>() != null)
                tags.Add("[LightController]");
            if (t.GetComponent<LightTurnOffOn>() != null)
                tags.Add("[switch]");
            if (t.GetComponent<FurniturePlaceable>() != null)
                tags.Add("[furniture]");
            if (t.GetComponent<CollectibleItem>() != null)
                tags.Add("[item]");
            Renderer r = t.GetComponent<Renderer>();
            if (r != null)
                tags.Add("[mesh]");
            if (!t.gameObject.activeSelf)
                tags.Add("[inactive]");
            if (t.childCount > 0)
                tags.Add($"+{t.childCount} children");
            return string.Join(" ", tags);
        }

        public override void DrawOverlay()
        {
            if (Time.unscaledTime >= overlayUntil)
                return;
            GUILayout.BeginArea(new Rect(20, 80, 520, Screen.height - 160), GUI.skin.box);
            overlayScroll = GUILayout.BeginScrollView(overlayScroll);
            GUILayout.Label(report);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static string Describe(GameObject go, bool deep)
        {
            var s = new StringBuilder();
            s.AppendLine($"Object: {go.name}  layer={LayerMask.LayerToName(go.layer)}  pos={go.transform.position:F2}");
            s.AppendLine($"Path: {PathOf(go.transform)}");
            s.AppendLine($"Components: {string.Join(", ", go.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name))}");
            Collider col = go.GetComponent<Collider>();
            if (col != null)
                s.AppendLine($"Collider: {col.GetType().Name} trigger={col.isTrigger} size={col.bounds.size:F2}");
            Interactable interactable = go.GetComponentInParent<Interactable>();
            if (interactable != null)
                s.AppendLine($"Interactable (self or parent): {interactable.GetType().Name} on '{interactable.name}'");

            Section(s, "Materials", () => Materials(s, go));
            if (deep)
                Section(s, "References", () => References.Describe(s, go));
            return s.ToString();
        }

        private static readonly string[] GlowProperties = { "_EmissionColor", "_EmissiveColor", "_EmissiveIntensity", "_Emission", "_Glow", "_Color", "_BaseColor" };

        /// <summary>
        /// Renderers of the object's furniture (or of the object itself if it isn't furniture): each material with its shader,
        /// keywords and colour/emission values, to see what makes a mesh glow.
        /// </summary>
        private static void Materials(StringBuilder s, GameObject go)
        {
            FurniturePlaceable furniture = go.GetComponentInParent<FurniturePlaceable>();
            Transform root = furniture != null ? furniture.transform : go.transform;
            s.AppendLine($"Renderers under '{root.name}':");
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                s.AppendLine($"  {renderer.name} ({renderer.GetType().Name}, enabled={renderer.enabled && renderer.gameObject.activeInHierarchy})");
                foreach (Material m in renderer.sharedMaterials)
                {
                    if (m == null)
                        continue;
                    var values = GlowProperties.Where(m.HasProperty).Select(p => $"{p}={Value(m, p)}");
                    s.AppendLine($"    {m.name} shader={m.shader?.name} keywords=[{string.Join(",", m.shaderKeywords)}] {string.Join(" ", values)}");
                }
            }
        }

        private static string Value(Material m, string property)
        {
            int index = m.shader.FindPropertyIndex(property);
            UnityEngine.Rendering.ShaderPropertyType type = m.shader.GetPropertyType(index);
            if (type == UnityEngine.Rendering.ShaderPropertyType.Color || type == UnityEngine.Rendering.ShaderPropertyType.Vector)
            {
                Color c = m.GetColor(property);
                return $"({c.r:0.##},{c.g:0.##},{c.b:0.##},{c.a:0.##})";
            }
            if (type == UnityEngine.Rendering.ShaderPropertyType.Texture)
                return m.GetTexture(property)?.name ?? "none";
            return m.GetFloat(property).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static void Section(StringBuilder s, string name, Action body)
        {
            try
            {
                body();
            }
            catch (Exception e)
            {
                s.AppendLine($"{name}: failed: {e.Message}");
            }
        }

        private static string PathOf(Transform t)
        {
            var parts = new System.Collections.Generic.List<string>();
            for (; t != null; t = t.parent)
                parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
