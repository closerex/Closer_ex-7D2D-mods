using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// A stroke owns a mesh and a bounded, lifetime-pruned history. No TrailRenderer, material cloning or scene lookup.
/// UV/color packing matches KF2VFXPort. Geometry interpolation is a Unity implementation, not a claim of UE3 byte equivalence.
/// </summary>
public sealed class KFAnimTrailMesh : IDisposable
{
    private struct Point
    {
        public KFAnimTrailSample sample;
        public double birth;
        public float distance;
    }

    private struct Section
    {
        public Vector3 first, control, second;
        public double birth;
        public float distance;
    }

    private const int MaxHistory = 4096;
    private const int MaxSubdivisions = 16;
    private readonly List<Point> history = new(64);
    private readonly List<Section> sections = new(128);
    private readonly List<Vector3> positions = new(384);
    private readonly List<Vector3> normals = new(384);
    private readonly List<Vector4> tangents = new(384);
    private readonly List<Color> colors = new(384);
    private readonly List<Vector4> uv = new(384);
    private readonly List<Vector4> dynamicData = new(384);
    private readonly List<Vector4> hdrRgb = new(384);
    private readonly List<Vector4> hdrAlpha = new(384);
    private readonly List<int> indices = new(1536);
    private bool disposed;
    private double lastBuild = double.NegativeInfinity;

    public Mesh Mesh { get; private set; }
    public int SampleCount { get { return history.Count; } }
    public int SectionCount { get { return sections.Count; } }
    public bool HasLivePoints { get { return history.Count > 0; } }

    public KFAnimTrailMesh()
    {
        Mesh = new Mesh { name = "KF2 Animation Trail (owned)", hideFlags = HideFlags.DontSave,
                          indexFormat = IndexFormat.UInt32 };
        Mesh.MarkDynamic();
    }

    public bool Append(KFAnimTrailSample sample, double birth)
    {
        if (disposed || !sample.IsFinite() || double.IsNaN(birth) || double.IsInfinity(birth)) return false;
        float distance = 0;
        if (history.Count > 0)
        {
            var previous = history[history.Count - 1];
            if (sample.time <= previous.sample.time || birth < previous.birth) return false;
            distance = previous.distance + Vector3.Distance(previous.sample.control, sample.control);
        }
        // Refuse bad/unbounded input rather than silently joining disconnected strokes.
        if (history.Count >= MaxHistory) return false;
        history.Add(new Point { sample = sample, birth = birth, distance = distance });
        return true;
    }

    public void Build(double now, KFAnimTrailProfile profile, Color? colorOverride = null)
    {
        if (disposed) throw new ObjectDisposedException(nameof(KFAnimTrailMesh));
        if (profile == null || !profile.IsValid() || double.IsNaN(now) || double.IsInfinity(now))
            throw new ArgumentException("Invalid trail profile or clock.");
        if (now < lastBuild) throw new ArgumentException("A trail clock cannot run backwards; create a new stroke.");
        lastBuild = now;
        double cutoff = now - profile.lifetime;
        int remove = 0;
        while (remove + 1 < history.Count && history[remove + 1].birth <= cutoff) remove++;
        if (remove > 0) history.RemoveRange(0, remove);
        if (history.Count == 1 && history[0].birth <= cutoff) history.Clear();
        ClearBuffers();
        for (int i = 0; i + 1 < history.Count; i++)
        {
            Point a = history[i], b = history[i + 1];
            if (b.birth <= cutoff || a.birth > now) continue;
            float start = b.birth > a.birth ? Mathf.Clamp01((float)((cutoff - a.birth) / (b.birth - a.birth))) : 0;
            float end = b.birth > a.birth ? Mathf.Clamp01((float)((now - a.birth) / (b.birth - a.birth))) : 1;
            float distance = Mathf.Max(Vector3.Distance(a.sample.first, b.sample.first),
                Mathf.Max(Vector3.Distance(a.sample.control, b.sample.control), Vector3.Distance(a.sample.second, b.sample.second)));
            int count = Mathf.Clamp(Mathf.CeilToInt(distance / profile.distanceTessellationStep), 1, MaxSubdivisions);
            if (sections.Count == 0) sections.Add(Interpolate(i, start));
            for (int j = 1; j <= count; j++) sections.Add(Interpolate(i, Mathf.Lerp(start, end, (float)j / count)));
        }
        if (sections.Count >= 2)
        {
            float distanceStart = sections[0].distance;
            float total = sections[sections.Count - 1].distance - distanceStart;
            for (int i = 0; i < sections.Count; i++)
            {
                Section s = sections[i];
                float age = Mathf.Clamp01((float)((now - s.birth) / profile.lifetime));
                float width = Mathf.Max(0, profile.width.Evaluate(age));
                Vector3 first = s.control + (s.first - s.control) * width;
                Vector3 second = s.control + (s.second - s.control) * width;
                Vector3 along = sections[Mathf.Min(i + 1, sections.Count - 1)].control - sections[Mathf.Max(0, i - 1)].control;
                if (along.sqrMagnitude < 1e-16f) along = sections[Mathf.Min(i + 1, sections.Count - 1)].first - sections[Mathf.Max(0, i - 1)].first;
                Vector3 across = second - first;
                Vector3 normal = Vector3.Cross(along, across).normalized;
                Vector3 tangent = -along.normalized;
                if (normal.sqrMagnitude < 0.5f) normal = Vector3.forward;
                if (tangent.sqrMagnitude < 0.5f) tangent = Vector3.right;
                float widthDistance = Vector3.Distance(first, s.control) + Vector3.Distance(s.control, second);
                float controlV = widthDistance > 1e-8f ? Vector3.Distance(first, s.control) / widthDistance : 0.5f;
                float u = total > 1e-8f ? 1 - (s.distance - distanceStart) / total : 0;
                float tiled = profile.tilingDistance > 0 ? s.distance / profile.tilingDistance : u;
                Color color = colorOverride ?? profile.color;
                float rgbScale = profile.rgbMultiplier != null && profile.rgbMultiplier.length > 0 ? profile.rgbMultiplier.Evaluate(age) : 1;
                color.r *= rgbScale; color.g *= rgbScale; color.b *= rgbScale;
                color.a *= profile.alpha.Evaluate(age);
                AddVertex(first, normal, tangent, u, 0, tiled, color);
                AddVertex(s.control, normal, tangent, u, controlV, tiled, color);
                AddVertex(second, normal, tangent, u, 1, tiled, color);
            }
            for (int i = 0; i + 1 < sections.Count; i++)
                for (int j = 0; j < 2; j++)
                {
                    int a = i * 3 + j, b = a + 3;
                    AddTriangle(a, b, a + 1);
                    AddTriangle(a + 1, b, b + 1);
                }
        }
        Mesh.Clear();
        Mesh.SetVertices(positions); Mesh.SetNormals(normals); Mesh.SetTangents(tangents); Mesh.SetColors(colors);
        Mesh.SetUVs(0, uv); Mesh.SetUVs(1, dynamicData); Mesh.SetUVs(2, hdrRgb); Mesh.SetUVs(3, hdrAlpha);
        Mesh.SetTriangles(indices, 0, true);
    }

    private void ClearBuffers()
    {
        sections.Clear(); positions.Clear(); normals.Clear(); tangents.Clear(); colors.Clear(); uv.Clear();
        dynamicData.Clear(); hdrRgb.Clear(); hdrAlpha.Clear(); indices.Clear();
    }

    private void AddVertex(Vector3 p, Vector3 normal, Vector3 tangent, float u, float v, float tiled, Color color)
    {
        positions.Add(p); normals.Add(normal); tangents.Add(new Vector4(tangent.x, tangent.y, tangent.z, 1));
        colors.Add(Color.white);
        // Exactly one native-to-Unity V conversion; the generated shader reverses it at its input boundary.
        uv.Add(new Vector4(u, 1 - v, tiled, 1 - v)); dynamicData.Add(Vector4.zero);
        hdrRgb.Add(new Vector4(0, color.r, color.g, color.b)); hdrAlpha.Add(new Vector4(color.a, 0, 0, 0));
    }

    private void AddTriangle(int a, int b, int c)
    {
        // Control and second sockets can legitimately coincide. Do not emit a degenerate half-sheet.
        if (Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]).sqrMagnitude <= 1e-24f) return;
        indices.Add(a); indices.Add(b); indices.Add(c);
    }

    private Section Interpolate(int i, float t)
    {
        Point a = history[i], b = history[i + 1];
        KFAnimTrailSample previous = history[Mathf.Max(0, i - 1)].sample;
        KFAnimTrailSample next = history[Mathf.Min(history.Count - 1, i + 2)].sample;
        float duration = b.sample.time - a.sample.time;
        float inScale = duration / (b.sample.time - previous.time);
        float outScale = duration / (next.time - a.sample.time);
        return new Section {
            first = Hermite(a.sample.first, b.sample.first, (b.sample.first - previous.first) * inScale, (next.first - a.sample.first) * outScale, t),
            control = Hermite(a.sample.control, b.sample.control, (b.sample.control - previous.control) * inScale, (next.control - a.sample.control) * outScale, t),
            second = Hermite(a.sample.second, b.sample.second, (b.sample.second - previous.second) * inScale, (next.second - a.sample.second) * outScale, t),
            birth = a.birth + (b.birth - a.birth) * t,
            distance = Mathf.Lerp(a.distance, b.distance, t)
        };
    }

    private static Vector3 Hermite(Vector3 a, Vector3 b, Vector3 da, Vector3 db, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return (2 * t3 - 3 * t2 + 1) * a + (t3 - 2 * t2 + t) * da + (-2 * t3 + 3 * t2) * b + (t3 - t2) * db;
    }

    public static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    public static bool Finite(Vector3 v) { return Finite(v.x) && Finite(v.y) && Finite(v.z); }
    public static bool Finite(Color v) { return Finite(v.r) && Finite(v.g) && Finite(v.b) && Finite(v.a); }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (Mesh != null) { if (Application.isPlaying) UnityEngine.Object.Destroy(Mesh); else UnityEngine.Object.DestroyImmediate(Mesh); }
        Mesh = null; history.Clear(); ClearBuffers();
    }
}
