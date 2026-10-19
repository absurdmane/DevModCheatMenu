using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DevMenu
{
    /// <summary>
    /// The game's uGUI under the mouse: the Inspect key with the game menu open (inventory, a container, a station) captures the element
    /// under the mouse (EventSystem.RaycastAll) and writes it, its parents up to the canvas and its children (layout, rects, images)
    /// to BepInEx\DevMenu\ui-inspect-latest.txt (only the latest). A click on the game's UI selects it too, while this page is open.
    /// The Inspector tab's UI page shows the capture and walks its parents and children.
    /// </summary>
    internal sealed class UiInspector
    {
        private static string FilePath => Path.Combine(Paths.BepInExRootPath, "DevMenu", "ui-inspect-latest.txt");

        private const int MaxHits = 20;
        private const int MaxChildren = 100;

        private GameObject selected;
        private string hits = "";
        private Vector2 mouse;
        private string summary = "";
        private float overlayUntil;
        private float frameUntil; // the key with the menu closed shows the frame for a few seconds; with the menu open it's always shown

        /// <summary>The game's own menu is open (or something else freed the cursor): the Inspect key captures UI, not the world.</summary>
        public static bool GameMenuOpen
        {
            get
            {
                GameUIController ui = GameUIController.instance;
                // IsAnyMenuOpen reads ShopController.instance without a null check.
                if (ui != null && ShopController.instance != null && ui.IsAnyMenuOpen())
                    return true;
                return Cursor.lockState != CursorLockMode.Locked && Cursor.visible;
            }
        }

        /// <summary>
        /// Captures the UI under the mouse and saves it. Returns false if there's none; <paramref name="quiet"/> then says
        /// nothing and keeps the previous capture (the menu's key falls back to the world).
        /// </summary>
        public bool CaptureAndSave(bool quiet = false) => Capture(Input.mousePosition, save: true, quiet);

        /// <summary>A click on the game view while this page is open: selects the UI element there. <paramref name="guiPoint"/> is in GUI coordinates (y down).</summary>
        public void PickAt(Vector2 guiPoint)
        {
            if (Capture(new Vector2(guiPoint.x, Screen.height - guiPoint.y), save: false, quiet: false))
                Menu.Notify($"Selected {selected.name}");
        }

        private bool Capture(Vector2 screenPoint, bool save, bool quiet)
        {
            EventSystem events = EventSystem.current;
            if (events == null)
            {
                if (!quiet)
                    Menu.Notify("UI inspect: no EventSystem");
                return false;
            }
            mouse = screenPoint;
            var results = new List<RaycastResult>();
            events.RaycastAll(new PointerEventData(events) { position = mouse }, results);
            results.RemoveAll(r => r.gameObject == null);
            if (results.Count == 0 && quiet)
                return false;

            var s = new StringBuilder();
            s.AppendLine($"Hits under the mouse (top first, {results.Count}):");
            foreach (RaycastResult r in results.Take(MaxHits))
                s.AppendLine($"  {InspectorTab.PathOf(r.gameObject.transform)}  depth={r.depth} sortingOrder={r.sortingOrder} raycaster={r.module?.GetType().Name}");
            if (results.Count > MaxHits)
                s.AppendLine($"  ... {results.Count - MaxHits} more");
            hits = s.ToString();

            if (results.Count == 0)
            {
                selected = null;
                Menu.Notify("UI inspect: nothing under the mouse");
                return false;
            }
            selected = results[0].gameObject;
            summary = $"{selected.name}\n{InspectorTab.PathOf(selected.transform)}\n{OneLine(selected.transform)}\n\n{hits}";
            if (save)
            {
                overlayUntil = Time.unscaledTime + 6f;
                frameUntil = Time.unscaledTime + 3f;
                Save();
            }
            return true;
        }

        private void Save()
        {
            if (selected == null)
            {
                Menu.Notify("Nothing captured");
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(FilePath, $"===== {DateTime.Now:yyyy-MM-dd HH:mm:ss} =====\n{Describe(selected)}\n");
            Menu.Notify($"UI inspected {selected.name} → ui-inspect-latest.txt");
        }

        /// <summary>The whole report: screen, hits, canvas, the element and each parent up to the root canvas, the element's children.</summary>
        private string Describe(GameObject go)
        {
            var s = new StringBuilder();
            s.AppendLine($"Screen: {Screen.width}x{Screen.height}  mouse={V(mouse)} (pixels, origin bottom-left)");
            s.AppendLine(hits);
            InspectorTab.Section(s, "Canvas", () => DescribeCanvas(s, go));
            s.AppendLine();
            s.AppendLine("Element and its parents (element first, up to the root canvas):");
            Canvas root = RootCanvas(go);
            for (Transform t = go.transform; t != null; t = t.parent)
            {
                Transform at = t;
                InspectorTab.Section(s, at.name, () => Block(s, at));
                if (root != null && t == root.transform)
                    break;
            }
            s.AppendLine();
            Children(s, go.transform);
            return s.ToString();
        }

        private static Canvas RootCanvas(GameObject go)
        {
            Canvas canvas = go.GetComponentInParent<Canvas>(true);
            return canvas != null ? canvas.rootCanvas : null;
        }

        private static void DescribeCanvas(StringBuilder s, GameObject go)
        {
            Canvas canvas = RootCanvas(go);
            if (canvas == null)
            {
                s.AppendLine("Canvas: none");
                return;
            }
            s.AppendLine($"Root canvas: {canvas.name} renderMode={canvas.renderMode} scaleFactor={F(canvas.scaleFactor)} sortingOrder={canvas.sortingOrder} pixelRect={R(canvas.pixelRect)}");
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
                s.AppendLine($"CanvasScaler: mode={scaler.uiScaleMode} referenceResolution={V(scaler.referenceResolution)} match={scaler.screenMatchMode} widthOrHeight={F(scaler.matchWidthOrHeight)} scaleFactor={F(scaler.scaleFactor)}");
        }

        /// <summary>One object in full: path, active state, components, RectTransform, computed layout sizes, layout and graphic components.</summary>
        private static void Block(StringBuilder s, Transform t)
        {
            GameObject go = t.gameObject;
            s.AppendLine($"--- {t.name}  activeSelf={go.activeSelf} activeInHierarchy={go.activeInHierarchy}  siblingIndex={t.GetSiblingIndex()} children={t.childCount}");
            s.AppendLine($"  Path: {InspectorTab.PathOf(t)}");
            s.AppendLine($"  Components: {string.Join(", ", go.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name))}");
            if (t is RectTransform rt)
            {
                s.AppendLine($"  Rect: anchorMin={V(rt.anchorMin)} anchorMax={V(rt.anchorMax)} pivot={V(rt.pivot)} sizeDelta={V(rt.sizeDelta)} anchoredPosition={V(rt.anchoredPosition)}");
                s.AppendLine($"  rect={R(rt.rect)} offsetMin={V(rt.offsetMin)} offsetMax={V(rt.offsetMax)}{(rt.localScale != Vector3.one ? $" localScale={rt.localScale.x.ToString("0.###", CultureInfo.InvariantCulture)},{rt.localScale.y.ToString("0.###", CultureInfo.InvariantCulture)}" : "")}");
                s.AppendLine($"  Screen: {ScreenRect(rt)}");
                s.AppendLine($"  Layout sizes: min={F(LayoutUtility.GetMinWidth(rt))}x{F(LayoutUtility.GetMinHeight(rt))} preferred={F(LayoutUtility.GetPreferredWidth(rt))}x{F(LayoutUtility.GetPreferredHeight(rt))} flexible={F(LayoutUtility.GetFlexibleWidth(rt))}x{F(LayoutUtility.GetFlexibleHeight(rt))}");
            }
            foreach (Component c in go.GetComponents<Component>())
            {
                string line = Details(c);
                if (line != null)
                    s.AppendLine($"  {line}");
            }
        }

        /// <summary>The settings of a layout, graphic, scroll or mask component, or null for anything else.</summary>
        private static string Details(Component c)
        {
            switch (c)
            {
                case GridLayoutGroup g:
                    return $"GridLayoutGroup: cellSize={V(g.cellSize)} spacing={V(g.spacing)} constraint={g.constraint} count={g.constraintCount} startCorner={g.startCorner} startAxis={g.startAxis} childAlignment={g.childAlignment} padding={P(g.padding)} enabled={g.enabled}";
                case HorizontalOrVerticalLayoutGroup h:
                    return $"{h.GetType().Name}: spacing={F(h.spacing)} childAlignment={h.childAlignment} padding={P(h.padding)} controlSize={h.childControlWidth}/{h.childControlHeight} forceExpand={h.childForceExpandWidth}/{h.childForceExpandHeight} useScale={h.childScaleWidth}/{h.childScaleHeight} reverse={h.reverseArrangement} enabled={h.enabled}";
                case LayoutGroup l:
                    return $"{l.GetType().Name}: childAlignment={l.childAlignment} padding={P(l.padding)} enabled={l.enabled}";
                case LayoutElement e:
                    return $"LayoutElement: ignoreLayout={e.ignoreLayout} min={F(e.minWidth)}x{F(e.minHeight)} preferred={F(e.preferredWidth)}x{F(e.preferredHeight)} flexible={F(e.flexibleWidth)}x{F(e.flexibleHeight)} priority={e.layoutPriority} enabled={e.enabled}";
                case ContentSizeFitter f:
                    return $"ContentSizeFitter: horizontal={f.horizontalFit} vertical={f.verticalFit} enabled={f.enabled}";
                case AspectRatioFitter a:
                    return $"AspectRatioFitter: mode={a.aspectMode} ratio={F(a.aspectRatio)} enabled={a.enabled}";
                case Image i:
                    return $"Image: sprite={(i.sprite != null ? i.sprite.name : "none")} color={C(i.color)} type={i.type}{(i.type == Image.Type.Filled ? $" fill={F(i.fillAmount)}" : "")} preserveAspect={i.preserveAspect} raycastTarget={i.raycastTarget} enabled={i.enabled}"
                        + (i.sprite != null ? $" spriteSize={V(i.sprite.rect.size)} border={i.sprite.border}" : "");
                case RawImage r:
                    return $"RawImage: texture={(r.texture != null ? r.texture.name : "none")} color={C(r.color)} uvRect={R(r.uvRect)} raycastTarget={r.raycastTarget} enabled={r.enabled}";
                case TMP_Text tmp:
                    return $"{tmp.GetType().Name}: \"{Short(tmp.text)}\" fontSize={F(tmp.fontSize)} autoSize={tmp.enableAutoSizing} ({F(tmp.fontSizeMin)}–{F(tmp.fontSizeMax)}) alignment={tmp.alignment} color={C(tmp.color)} raycastTarget={tmp.raycastTarget}";
                case Text text:
                    return $"Text: \"{Short(text.text)}\" fontSize={text.fontSize} bestFit={text.resizeTextForBestFit} alignment={text.alignment} color={C(text.color)} raycastTarget={text.raycastTarget}";
                case ScrollRect sr:
                    return $"ScrollRect: content={sr.content?.name} viewport={sr.viewport?.name} horizontal={sr.horizontal} vertical={sr.vertical} movement={sr.movementType} normalizedPosition={V(sr.normalizedPosition)} sensitivity={F(sr.scrollSensitivity)}";
                case Mask m:
                    return $"Mask: showMaskGraphic={m.showMaskGraphic} enabled={m.enabled}";
                case RectMask2D rm:
                    return $"RectMask2D: padding={rm.padding} softness={rm.softness} enabled={rm.enabled}";
                case CanvasGroup cg:
                    return $"CanvasGroup: alpha={F(cg.alpha)} interactable={cg.interactable} blocksRaycasts={cg.blocksRaycasts} ignoreParentGroups={cg.ignoreParentGroups}";
                case Canvas canvas:
                    return $"Canvas: renderMode={canvas.renderMode} overrideSorting={canvas.overrideSorting} sortingOrder={canvas.sortingOrder} isRoot={canvas.isRootCanvas}";
                case Selectable sel:
                    return $"{sel.GetType().Name}: interactable={sel.interactable} transition={sel.transition}";
            }
            return null;
        }

        private static void Children(StringBuilder s, Transform t)
        {
            s.AppendLine(t.childCount == 0 ? $"Children of {t.name}: none" : $"Children of {t.name} ({t.childCount}):");
            foreach (Transform child in t.Cast<Transform>().Take(MaxChildren))
                s.AppendLine($"  {child.name}  {OneLine(child)}");
            if (t.childCount > MaxChildren)
                s.AppendLine($"  ... {t.childCount - MaxChildren} more");
        }

        /// <summary>A short summary: active state, size, position on screen, the components that matter for layout.</summary>
        private static string OneLine(Transform t)
        {
            var parts = new List<string>();
            if (!t.gameObject.activeSelf)
                parts.Add("[inactive]");
            else if (!t.gameObject.activeInHierarchy)
                parts.Add("[inactive parent]");
            if (t is RectTransform rt)
            {
                parts.Add($"size={F(rt.rect.width)}x{F(rt.rect.height)}");
                parts.Add($"screen={ScreenRect(rt)}");
            }
            foreach (Component c in t.GetComponents<Component>())
                switch (c)
                {
                    case GridLayoutGroup g: parts.Add($"[Grid {V(g.cellSize)} gap {V(g.spacing)} {g.constraint} {g.constraintCount}]"); break;
                    case LayoutGroup l: parts.Add($"[{l.GetType().Name}]"); break;
                    case LayoutElement e: parts.Add(e.ignoreLayout ? "[LayoutElement ignore]" : $"[LayoutElement pref {F(e.preferredWidth)}x{F(e.preferredHeight)} flex {F(e.flexibleWidth)}x{F(e.flexibleHeight)}]"); break;
                    case ContentSizeFitter f: parts.Add($"[Fitter {f.horizontalFit}/{f.verticalFit}]"); break;
                    case Image i: parts.Add($"[Image {(i.sprite != null ? i.sprite.name : "no sprite")} {C(i.color)}]"); break;
                    case TMP_Text tmp: parts.Add($"[Text \"{Short(tmp.text)}\"]"); break;
                    case Text text: parts.Add($"[Text \"{Short(text.text)}\"]"); break;
                    case ScrollRect _: parts.Add("[ScrollRect]"); break;
                    case Mask _: parts.Add("[Mask]"); break;
                    case RectMask2D _: parts.Add("[RectMask2D]"); break;
                }
            if (t.childCount > 0)
                parts.Add($"+{t.childCount} children");
            return string.Join(" ", parts);
        }

        /// <summary>Screen pixels (origin bottom-left) of the rect's corners: (left,bottom)-(right,top) and size.</summary>
        private static string ScreenRect(RectTransform rt)
        {
            ScreenCorners(rt, out Vector2 min, out Vector2 max);
            return $"({F(min.x)},{F(min.y)})-({F(max.x)},{F(max.y)}) {F(max.x - min.x)}x{F(max.y - min.y)}";
        }

        private static void ScreenCorners(RectTransform rt, out Vector2 min, out Vector2 max)
        {
            Canvas canvas = rt.GetComponentInParent<Canvas>(true);
            Camera camera = canvas == null || canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
        }

        private static string F(float f) => f.ToString("0.#", CultureInfo.InvariantCulture);
        private static string V(Vector2 v) => $"({F(v.x)},{F(v.y)})";
        private static string R(Rect r) => $"(x={F(r.x)} y={F(r.y)} w={F(r.width)} h={F(r.height)})";
        private static string P(RectOffset p) => $"(l={p.left} r={p.right} t={p.top} b={p.bottom})";
        private static string C(Color c) => $"({c.r.ToString("0.##", CultureInfo.InvariantCulture)},{c.g.ToString("0.##", CultureInfo.InvariantCulture)},{c.b.ToString("0.##", CultureInfo.InvariantCulture)},{c.a.ToString("0.##", CultureInfo.InvariantCulture)})";

        private static string Short(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            text = text.Replace("\n", " ").Replace("\r", "");
            return text.Length > 40 ? text.Substring(0, 40) + "…" : text;
        }

        // ---- the tab: the selected element in full, its parents and children as buttons that select them ----

        public void Draw()
        {
            GUILayout.Label("Click the game's UI (outside this window) to select an element; the game doesn't get the click while this page is open.");
            GUILayout.Label(Plugin.InspectKey.Value == KeyCode.None
                ? "Set Keys.Inspect in the DevMenu settings (Mods tab) to capture the UI under the mouse with this menu closed."
                : $"Or close this menu, open the game's inventory (or a container, a station), point at an element and press {Plugin.InspectKey.Value}: it's captured and saved to ui-inspect-latest.txt.");
            if (selected == null)
            {
                GUILayout.Label("Nothing captured, or the captured UI is gone.");
                return;
            }
            if (GUILayout.Button("Save to file (this element, its parents and children)", GUILayout.ExpandWidth(false)))
                Menu.Run("UI inspect", Save);

            // Rebuilt only at a Layout event (so Layout and Repaint match): on a new selection, and twice a second as the UI moves.
            if (Event.current.type == EventType.Layout && (listedFor != selected || Time.unscaledTime - listedAt > 0.5f))
                BuildLists();

            GUILayout.Label("Parents (canvas first; click to select):");
            for (int i = 0; i < parents.Length; i++)
                if (parents[i] != null && InspectorTab.Row(parentLabels[i], i * 12f))
                {
                    selected = parents[i].gameObject;
                    return;
                }

            GUILayout.Label("► Selected:");
            GUILayout.TextArea(blockText);

            GUILayout.Label(children.Length == 0 ? "Children: none" : $"Children ({selected.transform.childCount}):");
            int shown = Mathf.Min(shownChildren, children.Length);
            for (int i = 0; i < shown; i++)
                if (children[i] != null && InspectorTab.Row(childLabels[i], 12f))
                {
                    selected = children[i].gameObject;
                    return;
                }
            if (shown < children.Length && GUILayout.Button($"Show more ({children.Length - shown} left)", GUILayout.ExpandWidth(false)))
                shownChildren += InspectorTab.RowsPerPage;

            GUILayout.Label(hitsText);
        }

        // IMGUI wraps only at spaces: a long path would widen the page past the window.
        private static string Wrappable(string text) => text.Replace("/", " / ");

        // The rows' labels and the element's text: made in BuildLists, not per event (each takes GetComponent calls).
        private GameObject listedFor;
        private float listedAt;
        private Transform[] parents = new Transform[0];
        private string[] parentLabels = new string[0];
        private Transform[] children = new Transform[0];
        private string[] childLabels = new string[0];
        private string blockText = "";
        private string hitsText = "";
        private int shownChildren;

        private void BuildLists()
        {
            if (listedFor != selected)
                shownChildren = InspectorTab.RowsPerPage;
            listedFor = selected;
            listedAt = Time.unscaledTime;
            var up = new List<Transform>();
            Canvas root = RootCanvas(selected);
            for (Transform t = selected.transform.parent; t != null; t = t.parent)
            {
                up.Add(t);
                if (root != null && t == root.transform)
                    break;
            }
            up.Reverse();
            parents = up.ToArray();
            parentLabels = parents.Select(Label).ToArray();
            children = selected.transform.Cast<Transform>().Take(MaxChildren).ToArray();
            childLabels = children.Select(Label).ToArray();
            var s = new StringBuilder();
            InspectorTab.Section(s, "Element", () => Block(s, selected.transform));
            blockText = Wrappable(s.ToString().TrimEnd());
            hitsText = Wrappable(hits.TrimEnd());
        }

        private static string Label(Transform t) => $"{t.name}   {OneLine(t)}";

        // ---- the game's UI doesn't get clicks while this page is open: its input module is off ----

        private BaseInputModule held;

        /// <summary>Called every frame: true turns the game's UI input off (once), false gives it back.</summary>
        public void HoldGameUi(bool hold)
        {
            if (hold && held == null)
            {
                BaseInputModule module = EventSystem.current != null ? EventSystem.current.currentInputModule : null;
                if (module != null && module.enabled)
                {
                    module.enabled = false;
                    held = module;
                }
            }
            else if (!hold && held != null)
            {
                held.enabled = true;
                held = null;
            }
        }

        /// <summary>The green rectangle around the selected element while the menu is open, if the element is on screen.</summary>
        public void DrawFrame()
        {
            if (selected == null || !selected.activeInHierarchy || !(selected.transform is RectTransform rt))
                return;
            ScreenCorners(rt, out Vector2 min, out Vector2 max);
            Frame.Rect(min, max);
        }

        // A few seconds after a capture: what was hit, so a wrong pick is seen without opening the file.
        // Top right; an IMGUI box doesn't block the game's raycasts, so the next capture still sees what's under it.
        public void DrawOverlay()
        {
            if (Time.unscaledTime < frameUntil)
                DrawFrame();
            if (Time.unscaledTime >= overlayUntil)
                return;
            const float width = 520f;
            var style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, wordWrap = true };
            float height = Mathf.Min(style.CalcHeight(new GUIContent(summary), width), Screen.height - 160f);
            GUI.Box(new Rect(Screen.width - width - 20f, 80f, width, height), summary, style);
        }
    }
}
