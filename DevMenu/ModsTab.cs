using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using BepInEx.Unity.Mono.Bootstrap;
using UnityEngine;

namespace DevMenu
{
    /// <summary>
    /// A section per loaded BepInEx plugin, any author (ours first, then the rest by name): its settings (the BepInEx config, editable)
    /// and, if the mod contains a static class <c>DevMenu</c> in the namespace of its plugin class, its buttons. Every public static void method
    /// without parameters becomes a button (label: its [Description], else the name); an optional
    /// <c>public static string Status()</c> is shown on top. Found by name, so neither side references the other.
    /// </summary>
    internal sealed class ModsTab : Tab
    {
        public override string Title => "Mods";

        private const string OurPrefix = "obenseuer.absurdmane.";
        private const float StatusInterval = 0.25f;

        private sealed class Section
        {
            public string Name;
            public MethodInfo Status;
            public List<(string Label, MethodInfo Method)> Buttons;
            public string StatusText = "";
            public bool Expanded;
            public BepInEx.Configuration.ConfigFile Config;
            public float StatusAt;
        }

        private List<Section> sections;
        private int scannedVersion = -1;

        public override void OnOpen() => Rescan();

        // Again after a script reload: the reloaded plugins are new objects of new types.
        private void Rescan()
        {
            if (sections != null && scannedVersion == ScriptLoader.Version)
                return;
            scannedVersion = ScriptLoader.Version;
            sections = Scan();
        }

        private static List<Section> Scan()
        {
            var result = new List<Section>();
            IEnumerable<BepInEx.PluginInfo> plugins = UnityChainloader.Instance.Plugins.Values
                .Where(i => i.Instance != null)
                .OrderBy(i => i.Metadata.GUID.StartsWith(OurPrefix) ? 0 : 1)
                .ThenBy(i => i.Metadata.Name, StringComparer.OrdinalIgnoreCase);
            foreach (var info in plugins)
            {
                Type pluginType = info.Instance.GetType();
                // Only a static class: an unrelated class that happens to be named DevMenu in some mod isn't taken for a section.
                Type menu = pluginType.Assembly.GetType(pluginType.Namespace + ".DevMenu");
                if (menu != null && !(menu.IsAbstract && menu.IsSealed))
                    menu = null;
                MethodInfo[] methods = menu?.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly) ?? new MethodInfo[0];
                BepInEx.Configuration.ConfigFile config = (info.Instance as BepInEx.Unity.Mono.BaseUnityPlugin)?.Config;
                // Nothing to show: no settings and no buttons.
                if (menu == null && (config == null || config.Count == 0))
                    continue;
                result.Add(new Section
                {
                    Name = info.Metadata.Name,
                    Config = config,
                    Status = methods.FirstOrDefault(m => m.Name == "Status" && m.ReturnType == typeof(string) && m.GetParameters().Length == 0),
                    Buttons = methods
                        .Where(m => m.ReturnType == typeof(void) && m.GetParameters().Length == 0)
                        .Select(m => (m.GetCustomAttribute<DescriptionAttribute>()?.Description ?? m.Name, m))
                        .ToList()
                });
                Plugin.Log.LogInfo($"Dev menu section: {info.Metadata.Name}");
            }
            return result;
        }

        public override void Draw()
        {
            Rescan();
            if (sections.Count == 0)
                GUILayout.Label("No mod with settings is loaded.");
            foreach (Section section in sections)
            {
                GUILayout.Space(4);
                if (GUILayout.Button($"{(section.Expanded ? "▼" : "►")}  <b>{section.Name}</b>", Gui.Header))
                    section.Expanded = !section.Expanded;
                if (!section.Expanded)
                    continue;
                if (section.Status != null)
                {
                    if (Time.unscaledTime >= section.StatusAt)
                    {
                        section.StatusAt = Time.unscaledTime + StatusInterval;
                        section.StatusText = Invoke(section.Status) as string ?? "";
                    }
                    GUILayout.Label(section.StatusText);
                }
                foreach ((string label, MethodInfo method) in section.Buttons)
                    if (GUILayout.Button(label, GUILayout.ExpandWidth(false)))
                    {
                        Invoke(method);
                        Menu.Notify($"{section.Name}: {label}");
                    }
                if (section.Config != null && section.Config.Count > 0)
                {
                    Gui.Heading("Settings");
                    SettingsEditor.Draw(section.Config);
                }
            }
        }

        private static object Invoke(MethodInfo method)
        {
            try
            {
                return method.Invoke(null, null);
            }
            catch (Exception e)
            {
                Exception inner = e is TargetInvocationException t && t.InnerException != null ? t.InnerException : e;
                Plugin.Log.LogError($"{method.DeclaringType?.FullName}.{method.Name} failed: {inner}");
                return $"{method.Name} failed: {inner.Message}";
            }
        }
    }
}
