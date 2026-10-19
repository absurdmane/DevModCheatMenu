using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Unity.Mono.Bootstrap;
using UnityEngine;

namespace DevMenu
{
    /// <summary>
    /// Buttons of debug scripts: one-off probes kept out of the mods. A section per loaded plugin of ours (GUID
    /// <c>obenseuer.absurdmane.*</c>) with a static class <c>DebugScripts</c> next to its plugin class; same rules as the Mods tab's
    /// <c>DevMenu</c> class (<see cref="ButtonClass"/>). Built with <c>-p:Deploy=scripts</c>, a script is changed and run again
    /// without restarting the game (Reload tab → Reload changed).
    /// </summary>
    internal sealed class DebugTab : Tab
    {
        public override string Title => "Debug";

        private const string OurPrefix = "obenseuer.absurdmane.";
        private const float StatusInterval = 0.25f;

        private sealed class Section
        {
            public string Name;
            public ButtonClass Scripts;
            public string StatusText = "";
            public float StatusAt;
            public bool Collapsed;
        }

        private List<Section> sections;
        private int scannedVersion = -1;

        public override void OnOpen() => Rescan();

        // Again after a script reload: the reloaded plugins are new objects of new types. Collapsed sections stay collapsed.
        private void Rescan()
        {
            if (sections != null && scannedVersion == ScriptLoader.Version)
                return;
            scannedVersion = ScriptLoader.Version;
            var collapsed = new HashSet<string>(sections?.Where(s => s.Collapsed).Select(s => s.Name) ?? Enumerable.Empty<string>());
            sections = UnityChainloader.Instance.Plugins.Values
                .Where(i => i.Instance != null && i.Metadata.GUID.StartsWith(OurPrefix))
                .OrderBy(i => i.Metadata.Name, StringComparer.OrdinalIgnoreCase)
                .Select(i => new Section
                {
                    Name = i.Metadata.Name,
                    Scripts = ButtonClass.Find(i.Instance.GetType(), "DebugScripts"),
                    Collapsed = collapsed.Contains(i.Metadata.Name)
                })
                .Where(s => s.Scripts != null)
                .ToList();
        }

        public override void Draw()
        {
            Rescan();
            if (sections.Count == 0)
            {
                GUILayout.Label("No debug scripts loaded. A plugin of ours with a static class DebugScripts next to its plugin class " +
                                "gets a section here: each public static void method is a button, public static string Status() a live line. " +
                                "Build it with -p:Deploy=scripts and use Reload → Reload changed after each change.");
                return;
            }
            foreach (Section section in sections)
            {
                GUILayout.Space(4);
                if (GUILayout.Button($"{(section.Collapsed ? "►" : "▼")}  <b>{section.Name}</b>", Gui.Header))
                    section.Collapsed = !section.Collapsed;
                if (section.Collapsed)
                    continue;
                if (section.Scripts.Status != null)
                {
                    if (Time.unscaledTime >= section.StatusAt)
                    {
                        section.StatusAt = Time.unscaledTime + StatusInterval;
                        section.StatusText = ButtonClass.Invoke(section.Scripts.Status) as string ?? "";
                    }
                    GUILayout.Label(section.StatusText);
                }
                if (section.Scripts.Buttons.Count == 0)
                    GUILayout.Label("No buttons (public static void methods without parameters).");
                foreach ((string label, MethodInfo method) in section.Scripts.Buttons)
                    if (GUILayout.Button(label, GUILayout.ExpandWidth(false)))
                    {
                        ButtonClass.Invoke(method);
                        Menu.Notify($"{section.Name}: {label}");
                    }
            }
        }
    }
}
