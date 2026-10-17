using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DevMenu
{
    /// <summary>
    /// Dropdown buttons and their option list, drawn inside the menu window on top of everything.
    /// - The list sits right under its button (above it when there's no room), centred, and follows it when the page scrolls.
    /// - A click outside the list closes it; that click still reaches what's under it, so another dropdown button opens its own list.
    /// - A click on the button of the open list closes it.
    /// Input is taken at the start of the window (<see cref="HandleInput"/>), before the page's controls see the click; the list is
    /// painted at the end (<see cref="Paint"/>), over them. Same idea as the menu's hints: rects are kept in screen GUI coordinates.
    /// </summary>
    internal static class Dropdown
    {
        // Where each dropdown button was last painted, in screen GUI coordinates.
        private static readonly Dictionary<string, Rect> anchors = new Dictionary<string, Rect>();

        private static string openId;
        private static string[] options;
        private static Action<int> pick;
        // The list a mouse-down outside it just closed: when that click lands on the same button, it must not reopen it.
        private static string closedByThisClick;

        public static void Close()
        {
            openId = null;
            options = null;
            pick = null;
        }

        /// <summary>A button showing <paramref name="text"/>; a click opens the list of <paramref name="getOptions"/> under it.</summary>
        public static void Button(string id, string text, Func<string[]> getOptions, Action<int> onPick, float width)
        {
            bool clicked = GUILayout.Button(text, Gui.CenteredButton, Gui.Fit(text, width, Gui.CenteredButton));
            if (Event.current.type == EventType.Repaint)
            {
                Rect r = GUILayoutUtility.GetLastRect();
                anchors[id] = new Rect(GUIUtility.GUIToScreenPoint(r.position), r.size);
            }
            if (!clicked)
                return;
            if (openId == id || closedByThisClick == id)
            {
                Close();
                return;
            }
            string[] list = getOptions();
            if (list == null || list.Length == 0)
                return;
            openId = id;
            options = list;
            pick = onPick;
        }

        /// <summary>Call first thing in the window: the list's clicks, and closing on a click outside.</summary>
        public static void HandleInput(Vector2 windowSize)
        {
            Event e = Event.current;
            if (e.type == EventType.MouseDown)
                closedByThisClick = null;
            if (openId == null || !Layout(windowSize, out Rect box, out float row))
                return;

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                Close();
                e.Use();
                return;
            }
            if (!e.isMouse && e.type != EventType.ScrollWheel)
                return;

            if (!box.Contains(e.mousePosition))
            {
                if (e.type == EventType.MouseDown)
                {
                    closedByThisClick = openId;
                    Close(); // not used: the click goes on to whatever is under the mouse
                }
                return;
            }

            if (e.type == EventType.MouseUp && e.button == 0)
            {
                int index = RowAt(box, row, e.mousePosition);
                Action<int> onPick = pick;
                Close();
                if (index >= 0)
                    Menu.Run("Pick", () => onPick(index));
            }
            e.Use(); // the page under the list doesn't see clicks or wheel on it
        }

        /// <summary>Call last in the window (before hints): paints the open list over the page.</summary>
        public static void Paint(Vector2 windowSize)
        {
            if (openId == null || Event.current.type != EventType.Repaint || !Layout(windowSize, out Rect box, out float row))
                return;
            Theme.Popup.Draw(box, GUIContent.none, false, false, false, false);
            Vector2 mouse = Event.current.mousePosition;
            for (int i = 0; i < options.Length; i++)
            {
                Rect r = RowRect(box, row, i);
                bool hover = r.Contains(mouse);
                Gui.CenteredButton.Draw(r, new GUIContent(options[i]), hover, hover && Input.GetMouseButton(0), false, false);
            }
        }

        private const float Gap = 2f;

        /// <summary>The list's box in window coordinates, under its button (or above when it doesn't fit), centred on it.</summary>
        private static bool Layout(Vector2 windowSize, out Rect box, out float row)
        {
            box = default;
            row = 0f;
            if (options == null || !anchors.TryGetValue(openId, out Rect anchorScreen))
                return false;
            Vector2 topLeft = GUIUtility.ScreenToGUIPoint(anchorScreen.position);
            var anchor = new Rect(topLeft, anchorScreen.size);

            GUIStyle button = Gui.CenteredButton;
            RectOffset pad = Theme.Popup.padding;
            row = button.CalcHeight(new GUIContent("Ay"), anchor.width);
            float width = Mathf.Max(anchor.width, options.Max(o => button.CalcSize(new GUIContent(o)).x)) + pad.horizontal;
            float height = options.Length * row + (options.Length - 1) * Gap + pad.vertical;

            float y = anchor.yMax + Gap;
            if (y + height > windowSize.y && anchor.y - Gap - height >= 0f)
                y = anchor.y - Gap - height;
            float x = Mathf.Clamp(anchor.center.x - width / 2f, 0f, Mathf.Max(0f, windowSize.x - width));
            box = new Rect(x, y, width, height);
            return true;
        }

        private static Rect RowRect(Rect box, float row, int i)
        {
            RectOffset pad = Theme.Popup.padding;
            return new Rect(box.x + pad.left, box.y + pad.top + i * (row + Gap), box.width - pad.horizontal, row);
        }

        private static int RowAt(Rect box, float row, Vector2 point)
        {
            for (int i = 0; i < options.Length; i++)
                if (RowRect(box, row, i).Contains(point))
                    return i;
            return -1;
        }
    }
}
