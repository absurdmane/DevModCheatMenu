using System.Linq;
using UnityEngine;

namespace DevMenu
{
    /// <summary>The FPS window switch and the spike profiler.</summary>
    internal sealed class FpsTab : Tab
    {
        public override string Title => "FPS";

        public override void Draw()
        {
            Gui.Heading("FPS");
            // A short label on the check button (it doesn't wrap); the details go in a wrapping label under it.
            FpsWindow.Visible = Gui.Toggle("FPS window", FpsWindow.Visible);
            GUILayout.Label("Stays open with the menu closed.");

            Gui.Heading("Spike profiler");
            SpikeProfiler p = SpikeProfiler.Instance;
            if (p == null)
                return;
            GUILayout.BeginHorizontal();
            string button = p.Current switch
            {
                SpikeProfiler.State.Off => "Start",
                SpikeProfiler.State.Patching => "Cancel",
                SpikeProfiler.State.On => "Stop",
                _ => "Stopping...",
            };
            // As wide as its longest text, so the status next to it doesn't jump when the state changes.
            float buttonWidth = new[] { "Start", "Cancel", "Stop", "Stopping..." }.Max(t => GUI.skin.button.CalcSize(new GUIContent(t)).x);
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && p.Current != SpikeProfiler.State.Unpatching;
            if (GUILayout.Button(button, GUILayout.Width(buttonWidth)))
                p.Toggle();
            GUI.enabled = enabled;
            // Short (no wrap), centred on the button; the failed count goes on its own wrapping line below.
            Gui.InlineLabel(p.Current switch
            {
                SpikeProfiler.State.Patching => $"patching {p.Patched} / {p.ToPatch}...",
                SpikeProfiler.State.On => $"on: {p.Spikes} slow frames",
                SpikeProfiler.State.Unpatching => "removing the timers...",
                _ => "off",
            });
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            if (p.Current == SpikeProfiler.State.On && p.Failed > 0)
                GUILayout.Label($"{p.Failed} methods could not be timed.");
            GUILayout.Label($"Times the game's scripts and writes frames over {SpikeProfiler.SpikeMs.Value:0.#} ms to " +
                            "BepInEx\\DevMenu\\spikes.txt; Stop adds a summary. The game runs a bit slower while it's on. " +
                            "Threshold and coroutines: [Spike profiler] in the config.");
            if (p.LastSpike.Length > 0)
                GUILayout.Label($"Last: {p.LastSpike}");
        }
    }
}
