using System.Collections.Generic;
using UnityEngine;

namespace DevMenu
{
    /// <summary>
    /// Green frames drawn from OnGUI (Repaint only): a rectangle around a UI element and a box around an object in the world,
    /// on top of everything. Screen points are in pixels with the origin at the bottom left, as Unity's screen space.
    /// </summary>
    internal static class Frame
    {
        private static readonly Color Green = new Color(0f, 1f, 0f, 1f); // #00FF00
        private const float Thickness = 2f;

        private static Texture2D white;
        private static Material material;

        private static Texture2D White
        {
            get
            {
                if (white == null)
                {
                    white = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                    white.SetPixel(0, 0, Color.white);
                    white.Apply();
                }
                return white;
            }
        }

        /// <summary>A rectangle around (min)-(max) in screen pixels.</summary>
        public static void Rect(Vector2 min, Vector2 max)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            // GUI space has y down.
            float left = min.x, right = max.x, top = Screen.height - max.y, bottom = Screen.height - min.y;
            Color previous = GUI.color;
            GUI.color = Green;
            GUI.DrawTexture(new Rect(left - Thickness, top - Thickness, right - left + 2 * Thickness, Thickness), White);
            GUI.DrawTexture(new Rect(left - Thickness, bottom, right - left + 2 * Thickness, Thickness), White);
            GUI.DrawTexture(new Rect(left - Thickness, top, Thickness, bottom - top), White);
            GUI.DrawTexture(new Rect(right, top, Thickness, bottom - top), White);
            GUI.color = previous;
        }

        // The 12 edges of a box, as indices into its 8 corners (bit 0 = x, bit 1 = y, bit 2 = z).
        private static readonly int[] Edges = { 0, 1, 2, 3, 4, 5, 6, 7, 0, 2, 1, 3, 4, 6, 5, 7, 0, 4, 1, 5, 2, 6, 3, 7 };

        /// <summary>The box's 12 edges, projected by the camera. An edge with an end behind the camera is skipped.</summary>
        public static void Box(Camera camera, Vector3[] corners)
        {
            if (Event.current.type != EventType.Repaint || camera == null || !EnsureMaterial())
                return;
            var screen = new Vector3[8];
            for (int i = 0; i < 8; i++)
                screen[i] = camera.WorldToScreenPoint(corners[i]);
            GL.PushMatrix();
            GL.LoadPixelMatrix();
            material.SetPass(0);
            GL.Begin(GL.QUADS);
            GL.Color(Green);
            for (int e = 0; e < Edges.Length; e += 2)
            {
                Vector3 a = screen[Edges[e]], b = screen[Edges[e + 1]];
                if (a.z <= 0f || b.z <= 0f)
                    continue;
                Vector2 dir = ((Vector2)b - (Vector2)a).normalized;
                Vector3 side = new Vector3(-dir.y, dir.x, 0f) * (Thickness / 2f);
                a.z = b.z = 0f;
                GL.Vertex(a - side);
                GL.Vertex(a + side);
                GL.Vertex(b + side);
                GL.Vertex(b - side);
            }
            GL.End();
            GL.PopMatrix();
        }

        private static bool EnsureMaterial()
        {
            if (material != null)
                return true;
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
                return false;
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            material.SetInt("_ZWrite", 0);
            material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            return true;
        }

        /// <summary>
        /// The 8 world corners of a box around the object, in its own rotation: its renderers (and its children's), or its
        /// colliders if it has no renderers, or a 20 cm cube at its position.
        /// </summary>
        public static Vector3[] BoxAround(GameObject go)
        {
            Transform root = go.transform;
            var points = new List<Vector3>();
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled)
                    continue;
                MeshFilter filter = r.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                    AddCorners(points, filter.sharedMesh.bounds, r.transform);
                else if (r is SkinnedMeshRenderer skinned)
                    AddCorners(points, skinned.localBounds, skinned.rootBone != null ? skinned.rootBone : r.transform);
                else
                    AddCorners(points, r.bounds, null);
            }
            if (points.Count == 0)
                foreach (Collider c in go.GetComponentsInChildren<Collider>())
                    if (c.enabled)
                        AddCorners(points, c.bounds, null);

            Bounds local;
            if (points.Count == 0)
                local = new Bounds(Vector3.zero, Vector3.one * 0.2f);
            else
            {
                local = new Bounds(root.InverseTransformPoint(points[0]), Vector3.zero);
                foreach (Vector3 p in points)
                    local.Encapsulate(root.InverseTransformPoint(p));
            }
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
                corners[i] = root.TransformPoint(Corner(local, i));
            return corners;
        }

        /// <summary>Adds the bounds' 8 corners in world space; <paramref name="space"/> null means the bounds are in world space already.</summary>
        private static void AddCorners(List<Vector3> points, Bounds b, Transform space)
        {
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = Corner(b, i);
                points.Add(space != null ? space.TransformPoint(p) : p);
            }
        }

        private static Vector3 Corner(Bounds b, int i) => new Vector3(
            (i & 1) == 0 ? b.min.x : b.max.x,
            (i & 2) == 0 ? b.min.y : b.max.y,
            (i & 4) == 0 ? b.min.z : b.max.z);
    }
}
