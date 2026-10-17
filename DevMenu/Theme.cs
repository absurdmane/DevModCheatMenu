using BepInEx.Configuration;
using UnityEngine;

namespace DevMenu
{
    /// <summary>
    /// The look of our windows, from the config (UI section): a copy of the default IMGUI skin with our font size, text colour and a
    /// solid window background. It's set only while our windows are drawn (<see cref="Use"/>), so other mods' IMGUI is untouched.
    /// Rebuilt when one of the settings changes.
    /// </summary>
    internal static class Theme
    {
        internal static ConfigEntry<string> Background;
        internal static ConfigEntry<float> BackgroundAlpha;
        internal static ConfigEntry<string> TextColor;
        internal static ConfigEntry<int> FontSize;

        private static GUISkin skin;

        public static void Bind(ConfigFile config)
        {
            Background = config.Bind("UI", "Background", "#333333", "Window background colour (#RRGGBB).");
            BackgroundAlpha = config.Bind("UI", "BackgroundAlpha", 0.9f,
                new ConfigDescription("Window background opacity.", new AcceptableValueRange<float>(0.1f, 1f)));
            TextColor = config.Bind("UI", "TextColor", "#FFFFFF", "Text colour (#RRGGBB).");
            FontSize = config.Bind("UI", "FontSize", 18,
                new ConfigDescription("Font size of our windows.", new AcceptableValueRange<int>(8, 32)));
            config.SettingChanged += (_, e) =>
            {
                if (e.ChangedSetting.Definition.Section == "UI")
                    Invalidate();
            };
        }

        private static void Invalidate()
        {
            skin = null;
            Gui.ResetStyles();
        }

        /// <summary>Switches to our skin; returns the previous one to put back.</summary>
        public static GUISkin Use()
        {
            GUISkin previous = GUI.skin;
            if (skin == null)
                skin = Build(previous);
            GUI.skin = skin;
            return previous;
        }

        private static GUISkin Build(GUISkin from)
        {
            GUISkin s = Object.Instantiate(from);
            s.hideFlags = HideFlags.HideAndDontSave;
            Color text = Parse(TextColor.Value, Color.white);
            foreach (GUIStyle style in new[] { s.label, s.button, s.toggle, s.textField, s.textArea, s.box, s.window })
            {
                style.fontSize = FontSize.Value;
                style.normal.textColor = style.onNormal.textColor = text;
                style.hover.textColor = style.onHover.textColor = text;
                style.active.textColor = style.onActive.textColor = text;
                style.focused.textColor = style.onFocused.textColor = text;
            }
            s.label.richText = true;
            // Long labels wrap instead of widening the page (which would show a horizontal scrollbar).
            s.label.wordWrap = true;
            s.textArea.wordWrap = true;
            // Roomier buttons: twice the default inner padding.
            RectOffset p = s.button.padding;
            s.button.padding = new RectOffset(p.left * 2, p.right * 2, p.top * 2, p.bottom * 2);

            Color back = Parse(Background.Value, new Color(0.2f, 0.2f, 0.2f));
            back.a = BackgroundAlpha.Value;
            Texture2D windowBack = Solid(back);
            s.window.normal.background = s.window.onNormal.background = windowBack;
            s.window.focused.background = s.window.onFocused.background = windowBack;
            s.window.border = new RectOffset(0, 0, 0, 0);
            // Twice the default side and bottom padding: that empty margin is where the window is grabbed to resize it.
            // The title bar (the window's top padding) has the same space above its text and below it.
            // The style can't draw the title there (it clips text to the area inside the padding), so our windows pass no title
            // and draw it themselves with DrawTitle.
            RectOffset wp = s.window.padding;
            TitlePad = wp.left * 2;
            s.window.contentOffset = Vector2.zero;
            s.window.padding = new RectOffset(wp.left * 2, wp.right * 2, TitleHeight, wp.bottom * 2);
            title = new GUIStyle(s.label)
            {
                fontSize = Mathf.RoundToInt(FontSize.Value * 1.2f),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(),
                margin = new RectOffset(),
                clipping = TextClipping.Clip
            };
            ResizeEdge = Mathf.Max(6, Mathf.Min(s.window.padding.left, Mathf.Min(s.window.padding.right, s.window.padding.bottom)));

            // Flat buttons: solid grey a step lighter than the window, slightly rounded corners, no outline. Hover and press go
            // lighter; a selected toggle (tabs, apartment buttons) is the lightest, so it stands out.
            Color solid = back;
            solid.a = 1f;
            Color Lighter(float t) => Color.Lerp(solid, Color.white, t);
            Fill(s.button, Corner, Lighter);
            s.button.border = new RectOffset(Corner, Corner, Corner, Corner);

            // Text fields: no frame, a flat fill darker than the window (lighter while typing in it), the buttons' padding.
            Texture2D fieldBack = Rounded(Color.Lerp(solid, Color.black, 0.25f), Corner);
            // While typing: a 2-pixel light frame around the field.
            Texture2D fieldFocused = Rounded(Color.Lerp(solid, Color.black, 0.1f), Corner, Lighter(0.45f), 2);
            foreach (GUIStyle field in new[] { s.textField, s.textArea })
            {
                field.normal.background = field.hover.background = field.active.background = fieldBack;
                field.onNormal.background = field.onHover.background = field.onActive.background = fieldBack;
                field.focused.background = field.onFocused.background = fieldFocused;
                field.border = new RectOffset(Corner, Corner, Corner, Corner);
                field.padding = new RectOffset(s.button.padding.left, s.button.padding.right, s.button.padding.top, s.button.padding.bottom);
            }

            // The box of a check button: like a text field.
            checkBox = new GUIStyle(s.box) { border = new RectOffset(Corner, Corner, Corner, Corner), padding = new RectOffset() };
            checkBox.normal.background = checkBox.onNormal.background = fieldBack;

            // Scrollbars: the thumb like a button, the track a bit darker; rounded too, no arrow buttons.
            foreach ((GUIStyle track, GUIStyle thumb) in new[]
                     {
                         (s.verticalScrollbar, s.verticalScrollbarThumb),
                         (s.horizontalScrollbar, s.horizontalScrollbarThumb)
                     })
            {
                Texture2D trackBack = Rounded(Lighter(0.05f), Corner);
                track.normal.background = track.hover.background = track.active.background = track.focused.background = trackBack;
                track.border = new RectOffset(Corner, Corner, Corner, Corner);
                Fill(thumb, Corner, Lighter);
                thumb.border = new RectOffset(Corner, Corner, Corner, Corner);
            }
            foreach (GUIStyle arrow in new[]
                     {
                         s.verticalScrollbarUpButton, s.verticalScrollbarDownButton,
                         s.horizontalScrollbarLeftButton, s.horizontalScrollbarRightButton
                     })
            {
                arrow.fixedWidth = arrow.fixedHeight = 0f;
                arrow.normal.background = arrow.hover.background = arrow.active.background = null;
            }

            // The window's close button: a circle (a round texture stretched over a square button).
            circle = new GUIStyle(s.button)
            {
                border = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0),
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            Fill(circle, CircleRadius, Lighter);

            // Popups (dropdown lists): the window colour, opaque, no border, some padding.
            popup = new GUIStyle(s.box)
            {
                padding = new RectOffset(8, 8, 8, 8),
                border = new RectOffset(0, 0, 0, 0)
            };
            Texture2D popupBack = Solid(solid);
            popup.normal.background = popup.onNormal.background = popupBack;
            popup.focused.background = popup.onFocused.background = popupBack;
            popup.hover.background = popup.onHover.background = popupBack;
            popup.active.background = popup.onActive.background = popupBack;
            return s;
        }

        private static GUIStyle popup;
        private static GUIStyle checkBox;
        private static GUIStyle title;

        /// <summary>
        /// The window's title in its bar: bold, 20 % larger, centred, with the window's padding above. Call inside the window
        /// function; pass no title to <c>GUI.Window</c>. <paramref name="rightReserve"/>: space kept free on the right (close button).
        /// </summary>
        public static void DrawTitle(float windowWidth, string text, float rightReserve = 0f)
        {
            float side = TitlePad + rightReserve;
            GUI.Label(new Rect(side, TitlePad, Mathf.Max(0f, windowWidth - side * 2f), TitleLine), text, title ?? GUI.skin.label);
        }

        /// <summary>The box of a check button (<see cref="Gui.CheckButton"/>).</summary>
        public static GUIStyle CheckBox => checkBox ?? GUI.skin.box;

        /// <summary>Space above the title (and the close button), the same as the window's side padding.</summary>
        public static int TitlePad { get; private set; } = 8;

        /// <summary>Height of the title's line of text.</summary>
        public static int TitleLine => Mathf.RoundToInt(FontSize.Value * 1.2f * 1.3f);

        /// <summary>How close to the window's side or bottom edge a press resizes it: the whole empty margin.</summary>
        public static int ResizeEdge { get; private set; } = 6;

        /// <summary>Height of the window's title bar (the drag area): the title's line plus space above and below.</summary>
        public static int TitleHeight => TitleLine + TitlePad * 2;

        /// <summary>The background of popup windows (dropdown lists). Built with the skin, so valid while our windows draw.</summary>
        public static GUIStyle Popup => popup ?? GUI.skin.box;

        private const int Corner = 4;
        private const int CircleRadius = 32;

        private static GUIStyle circle;

        /// <summary>A round button (use it square): the menu window's close button.</summary>
        public static GUIStyle Circle => circle ?? GUI.skin.button;

        // Every state of a button style, rounded with this radius; shades as in the buttons above.
        private static void Fill(GUIStyle style, int radius, System.Func<float, Color> lighter)
        {
            style.normal.background = Rounded(lighter(0.12f), radius);
            style.hover.background = Rounded(lighter(0.2f), radius);
            style.active.background = Rounded(lighter(0.28f), radius);
            style.focused.background = style.normal.background;
            style.onNormal.background = Rounded(lighter(0.32f), radius);
            style.onHover.background = Rounded(lighter(0.38f), radius);
            style.onActive.background = Rounded(lighter(0.42f), radius);
            style.onFocused.background = style.onNormal.background;
        }

        /// <summary>
        /// A filled square of 2 × radius + 2 pixels with rounded corners (anti-aliased edge). Used as a 9-slice with border = radius,
        /// the corners keep their shape and the middle stretches; with border 0 on a square button it's a circle.
        /// </summary>
        private static Texture2D Rounded(Color color, int radius) => Rounded(color, radius, color, 0);

        /// <summary>Same, with an outline of <paramref name="thickness"/> pixels along the rounded edge.</summary>
        private static Texture2D Rounded(Color color, int radius, Color outline, int thickness)
        {
            int size = radius * 2 + 2;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            float r = radius;
            float half = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // Signed distance to the rounded square's edge: negative inside.
                    float qx = Mathf.Abs(x + 0.5f - half) - (half - r);
                    float qy = Mathf.Abs(y + 0.5f - half) - (half - r);
                    float dist = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
                    // Within `thickness` of the edge: the outline colour (anti-aliased towards the fill).
                    Color c = thickness > 0 ? Color.Lerp(color, outline, Mathf.Clamp01(thickness + dist + 0.5f)) : color;
                    c.a *= Mathf.Clamp01(0.5f - dist);
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return t;
        }

        private static Texture2D Solid(Color color)
        {
            var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, color);
            t.Apply();
            return t;
        }

        private static Color Parse(string hex, Color fallback) =>
            ColorUtility.TryParseHtmlString(hex?.Trim(), out Color c) ? c : fallback;
    }
}
