using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DevMenu
{
    /// <summary>
    /// A small FPS window, shown with the menu open or closed: current FPS, the 90th / 99th percentile frame time of the last
    /// few seconds (the frames slower than 90 % / 99 % of the others) with the FPS they correspond to, and a graph of the frame
    /// times.
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

        public static bool Visible;

        private readonly Queue<(float at, float ms)> frames = new Queue<(float, float)>();
        private Rect window = new Rect(Screen.width - 300, 10, 0, 0); // GUILayout.Window sizes it to the content
        private float recalcAt;
        private string text = "";
        private static Texture2D white;

        private void Update()
        {
            if (!Visible)
            {
                frames.Clear();
                return;
            }
            float t = Time.unscaledTime;
            float ms = Time.unscaledDeltaTime * 1000f;
            frames.Enqueue((t, ms));
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
                   $"p99 {p99:0.0} ms ({1000f / p99:0} FPS)";
        }

        private static float Percentile(float[] sorted, float p) =>
            sorted[Mathf.Clamp(Mathf.CeilToInt(p * sorted.Length) - 1, 0, sorted.Length - 1)];

        private void OnGUI()
        {
            if (!Visible)
                return;
            GUISkin previous = Theme.Use();
            try
            {
                window = GUILayout.Window(WindowId, window, Draw, GUIContent.none, GUILayout.Width(GraphWidth + GUI.skin.window.padding.horizontal));
            }
            finally
            {
                GUI.skin = previous;
            }
        }

        private void Draw(int id)
        {
            Theme.DrawTitle(window.width, $"FPS, last {Span:0} s");
            GUILayout.Label(text);
            Rect graph = GUILayoutUtility.GetRect(GraphWidth, GraphHeight, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
                DrawGraph(graph);
            GUI.DragWindow();
        }

        /// <summary>
        /// One bar per pixel column, oldest left: the slowest frame in that slice of time. Green up to 16.7 ms (60 FPS), yellow up to
        /// 33.3 ms (30 FPS), red above. Faint lines mark 60 and 30 FPS.
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
            foreach ((float at, float ms) in list)
            {
                int c = Mathf.Clamp(Mathf.FloorToInt((at - (now - Span)) / Span * columns), 0, columns - 1);
                if (ms > slowest[c])
                    slowest[c] = ms;
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

            GUI.color = new Color(1f, 1f, 1f, 0.25f);
            foreach (float mark in new[] { 16.7f, 33.3f })
                GUI.DrawTexture(new Rect(r.x, r.yMax - mark / GraphMaxMs * r.height, r.width, 1f), white);
            GUI.color = previous;
        }
    }
}
