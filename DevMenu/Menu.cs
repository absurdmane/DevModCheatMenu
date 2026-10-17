using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DevMenu
{
    internal abstract class Tab
    {
        public abstract string Title { get; }
        public abstract void Draw();
        public virtual void OnOpen() { }
        /// <summary>Runs every frame, menu open or not.</summary>
        public virtual void Tick() { }
        /// <summary>Draws on screen while the menu is closed (overlays).</summary>
        public virtual void DrawOverlay() { }
    }

    /// <summary>The window: F9 toggles it, tabs on top. While open the game's controls are off and the cursor is free.</summary>
    internal sealed class Menu : MonoBehaviour
    {
        private const int WindowId = 0x0BE5;

        private static Menu instance;

        private readonly List<Tab> tabs = new List<Tab>();
        private InspectorTab inspector;
        private int current;
        private bool open;
        private Rect window = new Rect(60, 60, 760, 640);
        private Vector2 scroll;

        private string notice;
        private float noticeUntil;

        public static Menu Instance => instance;

        public void Close()
        {
            if (open)
                SetOpen(false);
        }

        public static void Notify(string text)
        {
            Plugin.Log.LogInfo(text);
            if (instance == null)
                return;
            instance.notice = text;
            instance.noticeUntil = Time.unscaledTime + 4f;
        }

        /// <summary>Runs a menu action; an exception is reported, never thrown into IMGUI.</summary>
        public static void Run(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"{what} failed: {e}");
                Notify($"{what} failed: {e.Message}");
            }
        }

        private void Awake()
        {
            instance = this;
            inspector = new InspectorTab();
            tabs.Add(new ModsTab());
            tabs.Add(new TenementTab());
            tabs.Add(new PlayerTab());
            tabs.Add(new ItemsTab());
            tabs.Add(new FurnitureTab());
            tabs.Add(new TeleportTab());
            tabs.Add(inspector);
            tabs.Add(new TimeTab());
            tabs.Add(new ToolsTab());
        }

        private void Update()
        {
            // While a key is being set in the Mods tab, the press only sets the key.
            bool hotkeys = !SettingsEditor.IsCapturing;
            if (hotkeys && Input.GetKeyDown(Plugin.MenuKey.Value))
                SetOpen(!open);
            if (hotkeys && !open && Input.GetKeyDown(Plugin.InspectKey.Value))
                Run("Inspect", () => inspector.CaptureAndSave());
            foreach (Tab tab in tabs)
                Run(tab.Title, tab.Tick);
            if (open)
                HoldControls();
        }

        // The camera reads the mouse in its LateUpdate unless these flags are set. Something in the game can clear them
        // while the menu is open, so set them again every frame, before the camera's LateUpdate.
        private static void HoldControls()
        {
            ThirdPersonCameraController camera = ThirdPersonCameraController.instance;
            if (camera != null && !camera.controlsDisabled)
            {
                if (!warnedCameraReset)
                {
                    Plugin.Log.LogWarning("Camera controls were re-enabled while the menu is open; holding them off");
                    warnedCameraReset = true;
                }
                camera.controlsDisabled = true;
            }
            if (ThirdPersonCameraCollision.instance != null)
                ThirdPersonCameraCollision.instance.controlsDisabled = true;
        }

        private static bool warnedCameraReset;

        private void SetOpen(bool value)
        {
            open = value;
            Dropdown.Close();
            GameController gc = GameController.instance;
            if (open)
            {
                Run("Open", tabs[current].OnOpen);
                if (gc != null)
                    Run("Disable controls", () => gc.ControlsDisabled(gameObject, ShowCursor: true));
                else
                    Plugin.Log.LogWarning("No GameController: game controls stay on");
                HoldControls();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (gc != null)
            {
                // Clears the camera flags too, unless another game window still holds the controls.
                Run("Enable controls", () => gc.ControlsEnabled(gameObject));
            }
            else
            {
                if (ThirdPersonCameraController.instance != null)
                    ThirdPersonCameraController.instance.controlsDisabled = false;
                if (ThirdPersonCameraCollision.instance != null)
                    ThirdPersonCameraCollision.instance.controlsDisabled = false;
            }
        }

        private void OnGUI()
        {
            GUISkin previous = Theme.Use();
            try
            {
                DrawGui();
            }
            finally
            {
                GUI.skin = previous;
            }
        }

        private void DrawGui()
        {
            if (open)
            {
                // Keep the cursor free: game UI code may lock it again.
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                if (Event.current.type == EventType.MouseDown && !window.Contains(Event.current.mousePosition))
                {
                    Dropdown.Close();
                    // Inspector open: a left click on the game picks the object under the mouse.
                    if (Event.current.button == 0 && tabs[current] == inspector)
                    {
                        Vector2 at = Event.current.mousePosition;
                        Run("Pick", () => inspector.PickAt(at));
                        Event.current.Use();
                    }
                }
                window = GUI.Window(WindowId, window, DrawWindow, GUIContent.none); // the title is drawn by DrawWindow
                if (resized.HasValue)
                {
                    window = resized.Value;
                    resized = null;
                }
            }
            else
            {
                foreach (Tab tab in tabs)
                    Run(tab.Title, tab.DrawOverlay);
            }

            if (notice != null && Time.unscaledTime < noticeUntil)
            {
                var style = new GUIStyle(GUI.skin.box) { fontSize = 16, wordWrap = true };
                GUI.Box(new Rect(Screen.width / 2f - 300, 20, 600, 50), notice, style);
            }
        }

        private void DrawWindow(int id)
        {
            Resize(); // first: a press at the window's edge must not reach the page
            Dropdown.HandleInput(window.size);
            float close = DrawCloseButton();
            Theme.DrawTitle(window.width, $"{Plugin.Name} {Plugin.Version}  ·  {Plugin.MenuKey.Value}", close);
            DrawTabs();

            GUIStyle bar = GUI.skin.verticalScrollbar;
            ContentWidth = window.width - GUI.skin.window.padding.horizontal - bar.fixedWidth - bar.margin.horizontal - 4f;
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.BeginVertical();
            Run(tabs[current].Title, tabs[current].Draw);
            GUILayout.EndVertical();
            WarnIfTooWide(tabs[current].Title);
            GUILayout.EndScrollView();

            Dropdown.Paint(window.size);
            DrawHint();
            GUI.DragWindow(new Rect(0, 0, 10000, Theme.TitleHeight));
        }

        // A round ✕ in the title bar's top-right corner. Drawn before the drag area, so it gets its clicks.
        /// <summary>The width a tab's page has without a horizontal scrollbar. For content that wraps by hand (<see cref="Gui.WrapToggles"/>).</summary>
        public static float ContentWidth { get; private set; } = 700f;

        private readonly HashSet<string> warnedWide = new HashSet<string>();

        // A page wider than the window gets a horizontal scrollbar: log it once per tab, with how much too wide, to find the row.
        private void WarnIfTooWide(string tab)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            float width = GUILayoutUtility.GetLastRect().width;
            if (width > ContentWidth + 8f && warnedWide.Add(tab))
                Plugin.Log.LogWarning($"{tab} tab is {width:0} px wide, the window shows {ContentWidth:0}: some row doesn't fit (font {Theme.FontSize.Value})");
        }

        // Returns the button's size, to keep the centred title clear of it.
        private float DrawCloseButton()
        {
            // As tall as the title's line, with the window's padding above it and to its right (clear of the resize strip).
            float size = Mathf.Max(18f, Theme.TitleLine);
            var rect = new Rect(window.width - size - Theme.ResizeEdge, Theme.TitlePad, size, size);
            if (GUI.Button(rect, "×", Theme.Circle)) // × is in every font, ✕ isn't
                Close();
            return size;
        }

        // Tab buttons in rows: a tab that doesn't fit the window's width starts the next row. Each row is stretched to the full width.
        private void DrawTabs()
        {
            GUIStyle style = GUI.skin.button;
            float available = window.width - GUI.skin.window.padding.horizontal;
            float x = 0f;
            GUILayout.BeginHorizontal();
            for (int i = 0; i < tabs.Count; i++)
            {
                float width = style.CalcSize(new GUIContent(tabs[i].Title)).x + Mathf.Max(style.margin.left, style.margin.right);
                if (x > 0f && x + width > available)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    x = 0f;
                }
                x += width;
                bool selected = GUILayout.Toggle(i == current, tabs[i].Title, style);
                if (selected && i != current)
                {
                    current = i;
                    scroll = Vector2.zero;
                    Run("Open", tabs[current].OnOpen);
                }
            }
            GUILayout.EndHorizontal();
        }

        private const float MinWidth = 420f;
        private const float MinHeight = 260f;
        private bool resizeLeft, resizeRight, resizeTop, resizeBottom;
        private Vector2 resizeStart; // screen point
        private Rect resizeFrom;
        private Rect? resized; // GUI.Window returns its own rect, so the new one is applied after it

        // Resize by the left, right and bottom edges and all four corners (a top corner moves the top too). The top edge between the
        // corners isn't used: that's the title bar, for moving the window. Nothing is drawn. Handled first thing in the window, so the
        // press isn't taken by a scrollbar or a button near the edge.
        private void Resize()
        {
            int id = GUIUtility.GetControlID(FocusType.Passive);
            Event e = Event.current;
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (e.button != 0)
                        break;
                    Vector2 m = e.mousePosition;
                    float edge = Theme.ResizeEdge; // the window's empty side and bottom margin
                    resizeLeft = m.x <= edge;
                    resizeRight = m.x >= window.width - edge;
                    resizeBottom = m.y >= window.height - edge;
                    resizeTop = m.y <= edge && (resizeLeft || resizeRight); // top corners only
                    if (!(resizeLeft || resizeRight || resizeBottom))
                        break;
                    resizeStart = GUIUtility.GUIToScreenPoint(m);
                    resizeFrom = window;
                    GUIUtility.hotControl = id;
                    e.Use();
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != id)
                        break;
                    Vector2 delta = GUIUtility.GUIToScreenPoint(e.mousePosition) - resizeStart;
                    Rect r = resizeFrom;
                    if (resizeRight)
                        r.width = Mathf.Max(MinWidth, resizeFrom.width + delta.x);
                    if (resizeLeft)
                    {
                        r.width = Mathf.Max(MinWidth, resizeFrom.width - delta.x);
                        r.x = resizeFrom.xMax - r.width;
                    }
                    if (resizeTop)
                    {
                        r.height = Mathf.Max(MinHeight, resizeFrom.height - delta.y);
                        r.y = resizeFrom.yMax - r.height;
                    }
                    if (resizeBottom)
                        r.height = Mathf.Max(MinHeight, resizeFrom.height + delta.y);
                    resized = r;
                    e.Use();
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl != id)
                        break;
                    GUIUtility.hotControl = 0;
                    e.Use();
                    break;
            }
        }
        private const float HintWidth = 380f;
        private static string hint;
        private static Vector2 hintAt;
        private static GUIStyle hintStyle;

        /// <summary>Shows a popup with this text next to the mouse, this frame. Call during Repaint, when the mouse is over the thing it explains.</summary>
        public static void Hint(string text)
        {
            if (string.IsNullOrEmpty(text) || Event.current.type != EventType.Repaint)
                return;
            hint = text;
            hintAt = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
        }

        // Drawn last inside the window, so it is on top of the tab's contents (and clipped to the window).
        private void DrawHint()
        {
            if (Event.current.type != EventType.Repaint || hint == null)
                return;
            if (hintStyle == null)
                hintStyle = new GUIStyle(GUI.skin.box) { wordWrap = true, alignment = TextAnchor.UpperLeft, padding = new RectOffset(8, 8, 6, 6) };
            Vector2 at = GUIUtility.ScreenToGUIPoint(hintAt);
            float height = hintStyle.CalcHeight(new GUIContent(hint), HintWidth);
            float x = Mathf.Min(at.x + 16f, window.width - HintWidth - 4f);
            float y = at.y + 20f + height > window.height ? at.y - height - 4f : at.y + 20f;
            GUI.Box(new Rect(Mathf.Max(4f, x), Mathf.Max(20f, y), HintWidth, height), hint, hintStyle);
            hint = null;
        }
    }
}
