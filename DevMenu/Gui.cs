using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DevMenu
{
    internal static class Gui
    {
        private static GUIStyle row;

        /// <summary>A left-aligned button for list rows; long text wraps instead of widening the page.</summary>
        public static GUIStyle Row => row ?? (row = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, wordWrap = true });

        private static GUIStyle header;
        /// <summary>Drops the cached styles: they're built from the skin in use, which changes with the UI settings.</summary>
        public static void ResetStyles()
        {
            row = null;
            header = null;
            centered = null;
            checkLabel = null;
            checkMark = null;
            inlineLabel = null;
        }

        /// <summary>A left-aligned, rich-text button used as a collapsible section header.</summary>
        public static GUIStyle Header => header ?? (header = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, richText = true, wordWrap = true });

        /// <summary>Heading text: bold, 1.5× the font size.</summary>
        public static string Big(string text) => $"<size={Mathf.RoundToInt(Theme.FontSize.Value * 1.5f)}><b>{text}</b></size>";

        public static void Heading(string text)
        {
            GUILayout.Space(6);
            GUILayout.Label(Big(text));
        }


        /// <summary>
        /// Toggle buttons that wrap onto the next row when the page is too narrow (IMGUI rows don't wrap by themselves).
        /// Returns the index clicked this frame, or -1.
        /// </summary>
        public static int WrapToggles(IList<string> labels, int selected)
        {
            GUIStyle style = GUI.skin.button;
            float available = Menu.ContentWidth;
            int clicked = -1;
            float x = 0f;
            GUILayout.BeginHorizontal();
            for (int i = 0; i < labels.Count; i++)
            {
                float width = style.CalcSize(new GUIContent(labels[i])).x + Mathf.Max(style.margin.left, style.margin.right);
                if (x > 0f && x + width > available)
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    x = 0f;
                }
                x += width;
                if (GUILayout.Toggle(i == selected, labels[i], style, GUILayout.ExpandWidth(false)) && i != selected)
                    clicked = i;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            return clicked;
        }

        /// <summary>A checkbox with a label: the same check button as <see cref="CheckButton"/>, as wide as its text.</summary>
        public static bool Toggle(string label, bool value) => CheckButton(label, value, 0f);

        private static GUIStyle centered;
        private static GUIStyle checkLabel;
        private static GUIStyle checkMark;

        /// <summary>A button with its text centred (the skin's default may not be).</summary>
        public static GUIStyle CenteredButton => centered ?? (centered = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleCenter });

        /// <summary>
        /// A button with a checkbox inside it, left of the label; a click flips the value. The box is as tall as a line of text,
        /// centred in the button, drawn like a text field with a ✓ when on. <paramref name="minWidth"/>: at least this wide
        /// (never narrower than box + label need).
        /// </summary>
        public static bool CheckButton(string label, bool value, float minWidth)
        {
            GUIStyle button = GUI.skin.button;
            float box = Mathf.Round(button.fontSize > 0 ? button.fontSize * 1.15f : 16f);
            if (checkLabel == null)
                checkLabel = new GUIStyle(button) { alignment = TextAnchor.MiddleLeft };
            checkLabel.padding = new RectOffset(button.padding.left + (int)box + 8, button.padding.right, button.padding.top, button.padding.bottom);

            var content = new GUIContent(label);
            Rect rect = GUILayoutUtility.GetRect(content, checkLabel, Fit(label, minWidth, checkLabel));
            if (GUI.Button(rect, content, checkLabel))
                value = !value;
            if (Event.current.type == EventType.Repaint)
            {
                var r = new Rect(rect.x + button.padding.left, rect.y + (rect.height - box) / 2f, box, box);
                Theme.CheckBox.Draw(r, GUIContent.none, false, false, false, false);
                if (value)
                {
                    if (checkMark == null)
                        checkMark = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, padding = new RectOffset() };
                    checkMark.fontSize = Mathf.RoundToInt(box * 0.85f);
                    checkMark.Draw(r, new GUIContent("✓"), false, false, false, false);
                }
            }
            return value;
        }

        /// <summary>A bold heading centred over the rows below.</summary>
        public static void CenteredHeading(string text)
        {
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(Big(text), GUILayout.ExpandWidth(false));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        /// <summary>A left-column label of a fixed width, for "Name:  [controls]" rows.</summary>
        /// <summary>A left-column label for "Name:  [controls]" rows, as tall as a button and vertically centred with it.</summary>
        public static void RowLabel(string text, float width = 110f)
        {
            GUIStyle style = InlineLabel(TextAnchor.MiddleLeft);
            GUILayout.Label(text, style, Fit(text, width, style), ButtonHeight(text));
        }

        /// <summary>A label in a row of buttons or fields, as wide as its text, vertically centred with them.</summary>
        public static void InlineLabel(string text, string tooltip = null)
        {
            GUIStyle style = InlineLabel(TextAnchor.MiddleLeft);
            GUILayout.Label(new GUIContent(text, tooltip), style, GUILayout.ExpandWidth(false), ButtonHeight(text));
        }

        /// <summary>Text between buttons in a row: as tall as a button and centred both ways, so it lines up with them.</summary>
        public static void CenteredLabel(string text, float minWidth)
        {
            GUIStyle style = InlineLabel(TextAnchor.MiddleCenter);
            GUILayout.Label(text, style, Fit(text, minWidth, style), ButtonHeight(text));
        }

        private static GUIStyle inlineLabel;

        private static GUIStyle InlineLabel(TextAnchor alignment)
        {
            if (inlineLabel == null)
                inlineLabel = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = false }; // short texts next to controls
            inlineLabel.alignment = alignment;
            inlineLabel.margin = GUI.skin.button.margin;
            return inlineLabel;
        }

        private static GUILayoutOption ButtonHeight(string text) =>
            GUILayout.Height(GUI.skin.button.CalcHeight(new GUIContent(text), 1000f));

        /// <summary>
        /// A fixed width that never cuts the text: <paramref name="width"/>, or what <paramref name="text"/> needs in this style
        /// (padding included) if that's more. Use it instead of <c>GUILayout.Width</c> for anything with text.
        /// </summary>
        public static GUILayoutOption Fit(string text, float width, GUIStyle style = null) =>
            GUILayout.Width(Mathf.Max(width, (style ?? GUI.skin.button).CalcSize(new GUIContent(text)).x));

        /// <summary>True on the frame Enter is pressed in the text field with this control name.</summary>
        public static bool EnterPressed(string controlName) =>
            Event.current.type == EventType.KeyDown &&
            (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) &&
            GUI.GetNameOfFocusedControl() == controlName;
    }

    /// <summary>A searchable list: substring, case-insensitive; shows at most <see cref="MaxRows"/> rows.</summary>
    internal sealed class SearchList<T>
    {
        private const int MaxRows = 50;

        private readonly Func<IEnumerable<T>> source;
        private readonly Func<T, string> label;
        private List<T> all;
        private List<T> shown = new List<T>();
        private string query = "";
        private string shownFor;

        public SearchList(Func<IEnumerable<T>> source, Func<T, string> label)
        {
            this.source = source;
            this.label = label;
        }

        public void Reload()
        {
            all = null;
            shownFor = null;
        }

        /// <summary>Draws the field and the rows; returns the clicked item or default.</summary>
        public T Draw()
        {
            if (all == null)
                all = source().Where(x => x != null).OrderBy(label).ToList();

            GUILayout.BeginHorizontal();
            Gui.InlineLabel("Search");
            query = GUILayout.TextField(query ?? "");
            if (GUILayout.Button("Reload", Gui.Fit("Reload", 70)))
                Reload();
            GUILayout.EndHorizontal();

            if (all == null)
                return default;
            if (shownFor != query)
            {
                string q = query.Trim().ToLowerInvariant();
                shown = (q.Length == 0 ? all : all.Where(x => label(x).ToLowerInvariant().Contains(q))).ToList();
                shownFor = query;
            }

            GUILayout.Label($"{shown.Count} of {all.Count}" + (shown.Count > MaxRows ? $", first {MaxRows} shown" : ""));
            T clicked = default;
            foreach (T item in shown.Take(MaxRows))
                if (GUILayout.Button(label(item), Gui.Row))
                    clicked = item;
            return clicked;
        }
    }
}
