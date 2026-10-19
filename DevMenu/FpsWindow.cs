using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DevMenu
{
    /// <summary>
    /// A small FPS window, shown with the menu open or closed: current FPS, the 90th / 99th percentile frame time of the last
    /// few seconds (the frames slower than 90 % / 99 % of the others) with the FPS they correspond to, and a graph of the frame
    /// times. GC lines: managed collections counted since the window opened, the average gap between them, the managed heap,
    /// and the last slow frame with whether a collection ran in it; collection frames are marked on the graph.
    /// </summary>
    internal sealed class FpsWindow : MonoBehaviour
    {
        private const int WindowId = 0x0BE6;
        private const float Span = 5f;
        private const float CurrentSpan = 0.5f;
        private const float RecalcEvery = 0.25f;

        private const float GraphWidth = 260f;
        private const float GraphHeight = 70f;
        private const float GraphMaxMs = 50f; // the top of the graph: 20 FPS; slower frames are clipped there
        private const float SlowMs = 33.3f; // a "slow frame" for the last-spike line: below 30 FPS
        private const int GapsKept = 5;

        public static bool Visible;

        private readonly Queue<(float at, float ms, bool gc)> frames = new Queue<(float, float, bool)>();
        private readonly Queue<float> gcGaps = new Queue<float>();
        private int gcCount = -1;
        private int gcSeen;
        private float lastGcAt = -1f;
        private (float at, float ms, bool gc) lastSlow = (-1f, 0f, false);
        private Rect window = new Rect(Screen.width - 300, 10, 0, 0); // GUILayout.Window sizes it to the content
        private float recalcAt;
        private string text = "";
        private static Texture2D white;

        private void Update()
        {
            if (!Visible)
            {
                frames.Clear();
                gcGaps.Clear();
                gcCount = -1;
                gcSeen = 0;
                lastGcAt = -1f;
                lastSlow = (-1f, 0f, false);
                return;
            }
            float t = Time.unscaledTime;
            float ms = Time.unscaledDeltaTime * 1000f;
            // unscaledDeltaTime is the previous frame's length, and a collection in that frame ran after its Update, so both
            // show up here together.
            int count = GC.CollectionCount(0);
            bool gc = gcCount >= 0 && count != gcCount;
            gcCount = count;
            if (gc)
            {
                gcSeen++;
                if (lastGcAt >= 0f)
                {
                    gcGaps.Enqueue(t - lastGcAt);
                    while (gcGaps.Count > GapsKept)
                        gcGaps.Dequeue();
                }
                lastGcAt = t;
            }
            if (ms > SlowMs)
                lastSlow = (t, ms, gc);
            frames.Enqueue((t, ms, gc));
            while (frames.Count > 0 && frames.Peek().at < t - Span)
                frames.Dequeue();
            if (t >= recalcAt)
            {
                recalcAt = t + RecalcEvery;
                Recalculate(t);
            }
        }

        private void Recalculate(float now)
        {
            float[] all = frames.Select(f => f.ms).OrderBy(m => m).ToArray();
            if (all.Length == 0)
                return;
            float[] recent = frames.Where(f => f.at >= now - CurrentSpan).Select(f => f.ms).ToArray();
            float current = recent.Length > 0 ? 1000f / recent.Average() : 0f;
            float p90 = Percentile(all, 0.90f);
            float p99 = Percentile(all, 0.99f);
            text = $"FPS {current:0}\n" +
                   $"p90 {p90:0.0} ms ({1000f / p90:0} FPS)\n" +
                   $"p99 {p99:0.0} ms ({1000f / p99:0} FPS)\n" +
                   // Always the same line count, each capped to its template in Widest: the window never changes size.
                   $"GC {gcSeen}, last " + (lastGcAt >= 0f ? Ago(now - lastGcAt) : "-") + "\n" +
                   "GC every " + (gcGaps.Count > 0 ? $"~{Mathf.Min(gcGaps.Average(), 999f):0.0} s" : "-") +
                   $", heap {GC.GetTotalMemory(false) / (1024f * 1024f):0} MB\n" +
                   "Slow " + (lastSlow.at >= 0f
                       ? $"{Mathf.Min(lastSlow.ms, 9999f):0} ms, {Ago(now - lastSlow.at)}, " + (lastSlow.gc ? "GC" : "no GC")
                       : "-");
        }

        private static string Ago(float seconds) => seconds >= 999f ? ">999 s ago" : $"{seconds:0.0} s ago";

        // The longest each line can get. The window is as wide as the widest of them (or the graph); lines never wrap.
        private static readonly string[] Widest =
        {
            "p99 999.9 ms (9999 FPS)", "GC 9999, last >999 s ago", "GC every ~999.9 s, heap 9999 MB", "Slow 9999 ms, >999 s ago, no GC"
        };
        private GUIStyle textStyle;
        private int textStyleFont = -1;
        private float textWidth;

        private static float Percentile(float[] sorted, float p) =>
            sorted[Mathf.Clamp(Mathf.CeilToInt(p * sorted.Length) - 1, 0, sorted.Length - 1)];

        private void OnGUI()
        {
            if (!Visible)
                return;
            GUISkin previous = Theme.Use();
            try
            {
                if (textStyle == null || textStyleFont != Theme.FontSize.Value)
                {
                    textStyle = new GUIStyle(GUI.skin.label) { wordWrap = false };
                    textStyleFont = Theme.FontSize.Value;
                    textWidth = Widest.Max(s => textStyle.CalcSize(new GUIContent(s)).x);
                }
                float width = Mathf.Max(GraphWidth, textWidth);
                // Wider than the graph at big fonts: keep the right edge on screen.
                window.x = Mathf.Min(window.x, Screen.width - width - GUI.skin.window.padding.horizontal);
                window = GUILayout.Window(WindowId, window, Draw, GUIContent.none, GUILayout.Width(width + GUI.skin.window.padding.horizontal));
            }
            finally
            {
                GUI.skin = previous;
            }
        }

        private void Draw(int id)
        {
            Theme.DrawTitle(window.width, $"FPS, last {Span:0} s");
            GUILayout.Label(text, textStyle);
            Rect graph = GUILayoutUtility.GetRect(GraphWidth, GraphHeight, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
                DrawGraph(graph);
            GUI.DragWindow();
        }

        /// <summary>
        /// One bar per pixel column, oldest left: the slowest frame in that slice of time. Green up to 16.7 ms (60 FPS), yellow up to
        /// 33.3 ms (30 FPS), red above. Faint lines mark 60 and 30 FPS. A cyan tick above a column: a GC ran in that slice.
        /// </summary>
        private void DrawGraph(Rect r)
        {
            if (white == null)
            {
                white = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                white.SetPixel(0, 0, Color.white);
                white.Apply();
            }
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.35f);
            GUI.DrawTexture(r, white);

            var list = frames.ToArray();
            int columns = Mathf.Max(1, Mathf.FloorToInt(r.width));
            float now = Time.unscaledTime;
            var slowest = new float[columns];
            var gcAt = new bool[columns];
            foreach ((float at, float ms, bool gc) in list)
            {
                int c = Mathf.Clamp(Mathf.FloorToInt((at - (now - Span)) / Span * columns), 0, columns - 1);
                if (ms > slowest[c])
                    slowest[c] = ms;
                gcAt[c] |= gc;
            }
            for (int c = 0; c < columns; c++)
            {
                float ms = slowest[c];
                if (ms <= 0f)
                    continue;
                float h = Mathf.Min(ms, GraphMaxMs) / GraphMaxMs * r.height;
                GUI.color = ms <= 16.7f ? new Color(0.35f, 0.85f, 0.4f) : ms <= 33.3f ? new Color(0.95f, 0.8f, 0.25f) : new Color(0.95f, 0.3f, 0.25f);
                GUI.DrawTexture(new Rect(r.x + c, r.yMax - h, 1f, h), white);
            }
            GUI.color = new Color(0.3f, 0.85f, 1f);
            for (int c = 0; c < columns; c++)
                if (gcAt[c])
                    GUI.DrawTexture(new Rect(Mathf.Clamp(r.x + c - 1f, r.x, r.xMax - 3f), r.y, 3f, 6f), white);

            GUI.color = new Color(1f, 1f, 1f, 0.25f);
            foreach (float mark in new[] { 16.7f, 33.3f })
                GUI.DrawTexture(new Rect(r.x, r.yMax - mark / GraphMaxMs * r.height, r.width, 1f), white);
            GUI.color = previous;
        }
    }
}
