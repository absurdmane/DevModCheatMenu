using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx.Configuration;
using BepInEx.Unity.Mono.Configuration;
using UnityEngine;

namespace DevMenu
{
    /// <summary>
    /// Edits a plugin's BepInEx config in place: bools as toggles, small enums as a dropdown, keys (KeyCode, KeyboardShortcut) by
    /// pressing them, everything else (numbers, strings, big enums) as a text field applied as soon as it parses. A change saves the config and fires the mod's SettingChanged, as
    /// ConfigurationManager does.
    /// </summary>
    internal static class SettingsEditor
    {
        private const int EnumButtonsMax = 6;

        // Text being typed per entry, kept until it parses.
        private static readonly Dictionary<ConfigEntryBase, string> typing = new Dictionary<ConfigEntryBase, string>();

        // A mod's master switch ([Mod] Enabled) goes at the bottom, below its other settings.
        private const string MasterSection = "Mod";

        public static void Draw(ConfigFile config)
        {
            string section = null;
            foreach (ConfigEntryBase entry in config.Values.OrderBy(e => e.Definition.Section == MasterSection ? 1 : 0).ThenBy(e => e.Definition.Section).ThenBy(e => e.Definition.Key))
            {
                if (entry.Definition.Section != section)
                {
                    section = entry.Definition.Section;
                    GUILayout.Label(Gui.Big(section));
                }
                GUILayout.BeginHorizontal();
                Gui.RowLabel(entry.Definition.Key, KeyWidth);
                // What's left of the row for the control: the key may be wider than KeyWidth.
                float keyWidth = Mathf.Max(KeyWidth, GUI.skin.label.CalcSize(new GUIContent(entry.Definition.Key)).x);
                Menu.Run(entry.Definition.Key, () => Edit(entry, Menu.ContentWidth - keyWidth - 24f));
                GUILayout.EndHorizontal();
                if (GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                    Menu.Hint(entry.Description?.Description);
            }
        }

        private const float KeyWidth = 200f;

        private static void Edit(ConfigEntryBase entry, float room)
        {
            Type type = entry.SettingType;
            object value = entry.BoxedValue;

            if (type == typeof(bool))
            {
                bool on = GUILayout.Toggle((bool)value, (bool)value ? "on" : "off", GUI.skin.button, Gui.Fit("off", 60));
                if (on != (bool)value)
                    Set(entry, on);
                return;
            }

            if (type.IsEnum && Enum.GetValues(type).Length <= EnumButtonsMax)
            {
                // A dropdown, not a row of buttons: long option names in a row would widen the page.
                object[] options = Enum.GetValues(type).Cast<object>().ToArray();
                Dropdown.Button($"{entry.ConfigFile.ConfigFilePath}|{entry.Definition}", value.ToString(),
                    () => options.Select(o => o.ToString()).ToArray(),
                    i => Set(entry, options[i]), 220f);
                return;
            }

            if (type == typeof(KeyCode) || type == typeof(KeyboardShortcut))
            {
                KeyCapture(entry, type, value);
                return;
            }

            if (type == typeof(int) || type == typeof(float) || type == typeof(double))
            {
                Stepper(entry, type, Convert.ToDouble(value, CultureInfo.InvariantCulture));
                return;
            }

            string shown = typing.TryGetValue(entry, out string t) ? t : Format(value);
            // As wide as the text, at most what's left of the row (a long list scrolls inside the field), at least 120.
            float need = GUI.skin.textField.CalcSize(new GUIContent(shown)).x;
            string typed = GUILayout.TextField(shown, GUILayout.Width(Mathf.Max(120f, Mathf.Min(need, room))));
            if (typed == shown)
                return;
            typing[entry] = typed;
            if (TryParse(type, typed, out object parsed))
            {
                Set(entry, entry.Description?.AcceptableValues != null ? entry.Description.AcceptableValues.Clamp(parsed) : parsed);
                typing.Remove(entry);
            }
        }

        /// <summary>Numbers: − value + buttons. Step 1 for ints; for fractional settings it follows the range: 0.05 up to 2, 0.1 up to 10, 0.5 up to 100.</summary>
        private static void Stepper(ConfigEntryBase entry, Type type, double value)
        {
            double step = 1;
            if (type != typeof(int) && entry.Description?.AcceptableValues is AcceptableValueBase range)
            {
                double? min = Bound(range, "MinValue"), max = Bound(range, "MaxValue");
                if (min.HasValue && max.HasValue)
                    step = max - min <= 2 ? 0.05 : max - min <= 10 ? 0.1 : max - min <= 100 ? 0.5 : 1;
            }

            double next = value;
            if (GUILayout.Button("-", Gui.Fit("-", 30)))
                next = value - step;
            string number = type == typeof(int) ? ((int)value).ToString() : value.ToString("0.##", CultureInfo.InvariantCulture);
            GUILayout.Label(number, Gui.Fit(number, 60, GUI.skin.label));
            if (GUILayout.Button("+", Gui.Fit("+", 30)))
                next = value + step;
            if (next == value)
                return;

            next = Math.Round(next / step) * step;
            object boxed = type == typeof(int) ? (object)(int)Math.Round(next)
                : type == typeof(float) ? (object)(float)next : next;
            if (entry.Description?.AcceptableValues != null)
                boxed = entry.Description.AcceptableValues.Clamp(boxed);
            Set(entry, boxed);
        }

        // The key entry waiting for a key press, and the last frame its button was drawn.
        private static ConfigEntryBase Capturing;
        private static int capturingDrawn;

        /// <summary>
        /// A key is being set: hotkeys are off meanwhile, so the press only sets the key. Ends by itself once the button isn't
        /// drawn any more (menu closed, another tab).
        /// </summary>
        public static bool IsCapturing
        {
            get
            {
                if (Capturing != null && Time.frameCount - capturingDrawn > 2)
                    Capturing = null;
                return Capturing != null;
            }
        }

        /// <summary>
        /// A button with the current key; click it, then press the new key (with Shift/Ctrl/Alt for a shortcut).
        /// Esc cancels, Backspace or Delete clears the key (None).
        /// </summary>
        private static void KeyCapture(ConfigEntryBase entry, Type type, object value)
        {
            bool capturing = Capturing == entry;
            if (capturing)
                capturingDrawn = Time.frameCount;
            Event e = Event.current;
            if (capturing && e.type == EventType.KeyDown && e.keyCode != KeyCode.None && !IsModifier(e.keyCode))
            {
                if (e.keyCode != KeyCode.Escape)
                {
                    KeyCode key = e.keyCode == KeyCode.Backspace || e.keyCode == KeyCode.Delete ? KeyCode.None : e.keyCode;
                    if (type == typeof(KeyCode))
                        Set(entry, key);
                    else
                        Set(entry, key == KeyCode.None ? KeyboardShortcut.Empty : new KeyboardShortcut(key, Modifiers(e)));
                }
                Capturing = null;
                e.Use();
                return;
            }

            string shown = capturing ? "Press a key…" : KeyName(value);
            if (GUILayout.Button(shown, Gui.Fit("Press a key…", 140)))
            {
                Capturing = capturing ? null : entry;
                capturingDrawn = Time.frameCount;
            }
            if (capturing)
                GUILayout.Label("Esc: cancel, Backspace: none", GUILayout.ExpandWidth(false));
        }

        private static string KeyName(object value)
        {
            string name = value is KeyboardShortcut shortcut ? shortcut.ToString() : value?.ToString();
            return string.IsNullOrEmpty(name) || name == nameof(KeyCode.None) ? "None" : name;
        }

        private static bool IsModifier(KeyCode key) =>
            key == KeyCode.LeftShift || key == KeyCode.RightShift || key == KeyCode.LeftControl || key == KeyCode.RightControl ||
            key == KeyCode.LeftAlt || key == KeyCode.RightAlt || key == KeyCode.LeftCommand || key == KeyCode.RightCommand;

        private static KeyCode[] Modifiers(Event e)
        {
            var modifiers = new List<KeyCode>();
            if (e.control)
                modifiers.Add(KeyCode.LeftControl);
            if (e.shift)
                modifiers.Add(KeyCode.LeftShift);
            if (e.alt)
                modifiers.Add(KeyCode.LeftAlt);
            return modifiers.ToArray();
        }

        private static double? Bound(AcceptableValueBase range, string property)
        {
            object v = range.GetType().GetProperty(property)?.GetValue(range);
            return v == null ? (double?)null : Convert.ToDouble(v, CultureInfo.InvariantCulture);
        }

        private static void Set(ConfigEntryBase entry, object value)
        {
            entry.BoxedValue = value; // fires SettingChanged and saves (SaveOnConfigSet)
            if (!entry.ConfigFile.SaveOnConfigSet)
                entry.ConfigFile.Save();
        }

        private static string Format(object value) =>
            value is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : value?.ToString() ?? "";

        private static bool TryParse(Type type, string text, out object value)
        {
            value = null;
            text = text.Trim();
            try
            {
                if (type == typeof(string))
                    value = text;
                else if (type == typeof(int))
                    value = int.Parse(text, CultureInfo.InvariantCulture);
                else if (type == typeof(float))
                    value = float.Parse(text.Replace(',', '.'), CultureInfo.InvariantCulture);
                else if (type == typeof(double))
                    value = double.Parse(text.Replace(',', '.'), CultureInfo.InvariantCulture);
                else if (type.IsEnum)
                    value = Enum.Parse(type, text, ignoreCase: true);
                else
                    value = TomlTypeConverter.ConvertToValue(text, type);
                return value != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
