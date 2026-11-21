using System.Collections.Generic;
using UnityEngine;

namespace BobosJourney
{
    /// <summary>Smooth-normal meshes built in code, so the toon outline never cracks at hard edges.</summary>
    public static class ProceduralMeshes
    {
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        static Mesh Cached(string key, System.Func<Mesh> build)
        {
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = build();
            m.name = key;
            cache[key] = m;
            return m;
        }

        /// <summary>Unit-radius UV sphere; u = 0.5 faces +Z and the seam is at -Z, matching how ArtBaker paints faces.</summary>
        public static Mesh Sphere(int lonSeg = 64, int latSeg = 40) => Cached($"sphere{lonSeg}x{latSeg}", () =>
        {
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            for (int y = 0; y <= latSeg; y++)
            {
                float vv = (float)y / latSeg;
                float lat = (vv - 0.5f) * Mathf.PI;
                for (int x = 0; x <= lonSeg; x++)
                {
                    float uu = (float)x / lonSeg;
                    float lon = (uu - 0.5f) * Mathf.PI * 2f;
                    v.Add(new Vector3(Mathf.Sin(lon) * Mathf.Cos(lat), Mathf.Sin(lat), Mathf.Cos(lon) * Mathf.Cos(lat)));
                    uv.Add(new Vector2(uu, vv));
                }
            }
            int row = lonSeg + 1;
            for (int y = 0; y < latSeg; y++)
                for (int x = 0; x < lonSeg; x++)
                {
                    int a = y * row + x, b = a + 1, c = a + row, d = c + 1;
                    t.Add(a); t.Add(b); t.Add(c);
                    t.Add(b); t.Add(d); t.Add(c);
                }
            var m = new Mesh();
            m.SetVertices(v);
            m.SetNormals(v);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            if (m.uv.Length > 0) m.RecalculateTangents();
            return m;
        });

        /// <summary>Revolve a 2D profile (x = radius, y = height) around the Y axis.</summary>
        public static Mesh Lathe(string key, Vector2[] profile, int segments = 48, bool smoothProfile = true) => Cached(key, () =>
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            int pointCount = profile.Length;
            var profileNormals = new Vector2[pointCount];
            for (int i = 0; i < pointCount; i++)
            {
                Vector2 prev = profile[Mathf.Max(0, i - 1)];
                Vector2 next = profile[Mathf.Min(pointCount - 1, i + 1)];
                Vector2 tan = (next - prev).normalized;
                profileNormals[i] = new Vector2(tan.y, -tan.x); // outward for a profile listed bottom to top
            }

            float totalLength = 0;
            var arcLength = new float[pointCount];
            for (int i = 1; i < pointCount; i++)
            {
                totalLength += Vector2.Distance(profile[i - 1], profile[i]);
                arcLength[i] = totalLength;
            }

            for (int s = 0; s <= segments; s++)
            {
                float a = (float)s / segments * Mathf.PI * 2f;
                float ca = Mathf.Cos(a);
                float sa = Mathf.Sin(a);
                for (int i = 0; i < pointCount; i++)
                {
                    var p = profile[i];
                    v.Add(new Vector3(p.x * ca, p.y, p.x * sa));
                    n.Add(new Vector3(profileNormals[i].x * ca, profileNormals[i].y, profileNormals[i].x * sa).normalized);
                    uv.Add(new Vector2((float)s / segments, totalLength > 0 ? arcLength[i] / totalLength : 0));
                }
            }
            for (int s = 0; s < segments; s++)
                for (int i = 0; i < pointCount - 1; i++)
                {
                    int a = s * pointCount + i, b = a + 1, c = a + pointCount, d = c + 1;
                    t.Add(a); t.Add(b); t.Add(c);
                    t.Add(c); t.Add(b); t.Add(d);
                }
            var m = new Mesh();
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            if (m.uv.Length > 0) m.RecalculateTangents();
            return m;
        });

        /// <summary>Rounded puck: a disc of radius 1 and height 1 (y from 0 to 1) with bevelled rims.</summary>
        public static Mesh Puck(float bevel = 0.22f, float bottomTaper = 0.82f)
        {
            string key = $"puck{bevel:0.00}_{bottomTaper:0.00}";
            var prof = new List<Vector2>();
            prof.Add(new Vector2(0.0f, 0f));
            for (int i = 0; i <= 5; i++) // bottom bevel
            {
                float a = Mathf.Lerp(-Mathf.PI * 0.5f, 0f, i / 5f);
                prof.Add(new Vector2(Mathf.Lerp(bottomTaper, 1f, i / 5f) - bevel + Mathf.Cos(a) * bevel, bevel + Mathf.Sin(a) * bevel));
            }
            for (int i = 0; i <= 6; i++) // top bevel
            {
                float a = Mathf.Lerp(0f, Mathf.PI * 0.5f, i / 6f);
                prof.Add(new Vector2(1f - bevel + Mathf.Cos(a) * bevel, 1f - bevel + Mathf.Sin(a) * bevel));
            }
            prof.Add(new Vector2(0f, 1f));
            return Lathe(key, prof.ToArray(), 56);
        }

        /// <summary>Puck with ragged rock sides and a flat top (fair to land on); a few variants are cached.</summary>
        public static Mesh RockyPuck(int variant)
        {
            variant = Mathf.Abs(variant) % 6;
            return Cached("rockypuck" + variant, () =>
            {
                var src = Puck(0.18f, 0.7f);
                var m = Object.Instantiate(src);
                var v = m.vertices;
                float seed = variant * 17.31f;
                for (int i = 0; i < v.Length; i++)
                {
                    var p = v[i];
                    float r = new Vector2(p.x, p.z).magnitude;
                    if (r < 1e-4f) continue;
                    float a = Mathf.Atan2(p.z, p.x);
                    // layered lumps around the rim, stronger toward the bottom (broken off underside)
                    float n = Mathf.Sin(a * 3f + seed) * 0.05f + Mathf.Sin(a * 7f + seed * 1.7f) * 0.035f + Mathf.Sin(a * 13f + seed * 0.3f + p.y * 6f) * 0.025f;
                    float depth = 1f - Mathf.Clamp01(p.y);
                    if (p.y > 0.97f) n *= 0.35f;
                    float k = 1f + n * (0.6f + depth * 1.4f);
                    v[i] = new Vector3(p.x * k, p.y > 0.97f ? p.y : p.y - Mathf.Abs(n) * depth * 0.8f, p.z * k);
                }
                m.vertices = v;
                m.RecalculateNormals();
                m.RecalculateBounds();
                m.RecalculateTangents();
                return m;
            });
        }

        /// <summary>Cone of radius 1 height 1 with a rounded tip, base at y = 0.</summary>
        public static Mesh Cone() => Lathe("cone", new[]
        {
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.55f, 0.5f), new Vector2(0.18f, 0.9f),
            new Vector2(0.08f, 0.98f), new Vector2(0, 1),
        }, 24);

        /// <summary>Upper part of a sphere (cap/dome), radius 1, from latitude startDeg to the pole.</summary>
        public static Mesh Dome(float startDeg = 0f)
        {
            var prof = new List<Vector2>();
            for (int i = 0; i <= 16; i++)
            {
                float a = Mathf.Lerp(startDeg, 90f, i / 16f) * Mathf.Deg2Rad;
                prof.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
            }
            return Lathe($"dome{startDeg:0}", prof.ToArray(), 48);
        }

        /// <summary>Box with rounded edges, centred on the origin.</summary>
        public static Mesh RoundedBox(Vector3 size, float radius, int res = 6)
        {
            string key = $"rbox{size.x:0.00}_{size.y:0.00}_{size.z:0.00}_{radius:0.00}";
            return Cached(key, () =>
            {
                var v = new List<Vector3>();
                var n = new List<Vector3>();
                var uv = new List<Vector2>();
                var t = new List<int>();
                Vector3 half = size * 0.5f;
                Vector3 inner = new Vector3(Mathf.Max(0, half.x - radius), Mathf.Max(0, half.y - radius), Mathf.Max(0, half.z - radius));
                int seg = res * 2;
                // build six faces of a subdivided cube, then push vertices onto the rounded surface
                Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
                foreach (var fn in normals)
                {
                    Vector3 ax1 = Mathf.Abs(fn.y) > 0.5f ? Vector3.right : Vector3.up;
                    Vector3 ax2 = Vector3.Cross(fn, ax1);
                    int start = v.Count;
                    for (int i = 0; i <= seg; i++)
                        for (int j = 0; j <= seg; j++)
                        {
                            float a = (i / (float)seg) * 2f - 1f;
                            float b = (j / (float)seg) * 2f - 1f;
                            Vector3 cube = fn + ax1 * a + ax2 * b; // on unit cube [-1,1]
                            Vector3 p = Vector3.Scale(cube, half);
                            Vector3 clamped = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                            Vector3 dir = p - clamped;
                            if (dir.sqrMagnitude < 1e-8f) dir = fn;
                            dir.Normalize();
                            v.Add(clamped + dir * radius);
                            n.Add(dir);
                            uv.Add(new Vector2(i / (float)seg, j / (float)seg));
                        }
                    int row = seg + 1;
                    for (int i = 0; i < seg; i++)
                        for (int j = 0; j < seg; j++)
                        {
                            int a = start + i * row + j, b = a + 1, c = a + row, d = c + 1;
                            t.Add(a); t.Add(b); t.Add(c);
                            t.Add(c); t.Add(b); t.Add(d);
                        }
                }
                var m = new Mesh();
                m.SetVertices(v);
                m.SetNormals(n);
                m.SetUVs(0, uv);
                m.SetTriangles(t, 0);
                // fix winding so faces point outwards
                var tris = m.triangles;
                for (int i = 0; i < tris.Length; i += 3)
                {
                    Vector3 p0 = v[tris[i]], p1 = v[tris[i + 1]], p2 = v[tris[i + 2]];
                    Vector3 faceNormal = Vector3.Cross(p1 - p0, p2 - p0);
                    if (Vector3.Dot(faceNormal, n[tris[i]] + n[tris[i + 1]] + n[tris[i + 2]]) < 0)
                    {
                        int tmp = tris[i + 1];
                        tris[i + 1] = tris[i + 2];
                        tris[i + 2] = tmp;
                    }
                }
                m.triangles = tris;
                m.RecalculateBounds();
                if (m.uv.Length > 0) m.RecalculateTangents();
                return m;
            });
        }

        /// <summary>Hollow cylinder section (inner + outer walls + top/bottom rims).</summary>
        public static Mesh WallRing(float innerR, float outerR, float height, int segments, float uvScale)
        {
            string key = $"wall{innerR:0.0}_{outerR:0.0}_{height:0.0}_{segments}";
            return Cached(key, () =>
            {
                var v = new List<Vector3>();
                var n = new List<Vector3>();
                var uv = new List<Vector2>();
                var t = new List<int>();
                for (int w = 0; w < 2; w++)
                {
                    float r = w == 0 ? outerR : innerR;
                    float s = w == 0 ? 1f : -1f;
                    int b = v.Count;
                    for (int i = 0; i <= segments; i++)
                    {
                        float a = i * Mathf.PI * 2f / segments;
                        var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                        v.Add(d * r);
                        v.Add(d * r + Vector3.up * height);
                        n.Add(d * s);
                        n.Add(d * s);
                        float u = (float)i / segments * 2f * Mathf.PI * r * uvScale;
                        uv.Add(new Vector2(u, 0));
                        uv.Add(new Vector2(u, height * uvScale));
                    }
                    for (int i = 0; i < segments; i++)
                    {
                        int a0 = b + i * 2, a1 = a0 + 1, b0 = a0 + 2, b1 = a0 + 3;
                        if (w == 0)
                        {
                            t.Add(a0); t.Add(a1); t.Add(b0);
                            t.Add(b0); t.Add(a1); t.Add(b1);
                        }
                        else
                        {
                            t.Add(a0); t.Add(b0); t.Add(a1);
                            t.Add(b0); t.Add(b1); t.Add(a1);
                        }
                    }
                }
                var m = new Mesh();
                m.SetVertices(v);
                m.SetNormals(n);
                m.SetUVs(0, uv);
                m.SetTriangles(t, 0);
                m.RecalculateBounds();
                if (m.uv.Length > 0) m.RecalculateTangents();
                return m;
            });
        }

        /// <summary>Two crossed vertical quads (a grass clump), 1 wide, 1 tall, root at y = 0, UV.y 0 at the root.</summary>
        public static Mesh GrassCross() => Cached("grasscross", () =>
        {
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var n = new List<Vector3>();
            var t = new List<int>();
            for (int q = 0; q < 2; q++)
            {
                float a = q * Mathf.PI * 0.5f + 0.4f;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.5f;
                var nn = Vector3.Cross(Vector3.up, d).normalized;
                int b = v.Count;
                v.Add(-d); v.Add(d); v.Add(d + Vector3.up); v.Add(-d + Vector3.up);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
                for (int k = 0; k < 4; k++) n.Add(Vector3.Lerp(nn, Vector3.up, 0.6f).normalized); // soft, grass-like normals
                t.Add(b); t.Add(b + 2); t.Add(b + 1);
                t.Add(b); t.Add(b + 3); t.Add(b + 2);
            }
            var m = new Mesh();
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        });

        /// <summary>Flat quad on the XZ plane, 1 x 1, facing up.</summary>
        public static Mesh FlatQuad() => Cached("flatquad", () =>
        {
            var m = new Mesh();
            m.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateBounds();
            if (m.uv.Length > 0) m.RecalculateTangents();
            return m;
        });

        /// <summary>Flat ring on the XZ plane between the inner and outer radius; UV.x runs around, UV.y across.</summary>
        public static Mesh Ring(float inner, float outer, int seg = 64) => Cached($"ring{inner:0.00}_{outer:0.00}", () =>
        {
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            var n = new List<Vector3>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v.Add(d * inner);
                v.Add(d * outer);
                n.Add(Vector3.up);
                n.Add(Vector3.up);
                uv.Add(new Vector2((float)i / seg, 0));
                uv.Add(new Vector2((float)i / seg, 1));
            }
            for (int i = 0; i < seg; i++)
            {
                int a = i * 2;
                t.Add(a); t.Add(a + 1); t.Add(a + 2);
                t.Add(a + 2); t.Add(a + 1); t.Add(a + 3);
            }
            var m = new Mesh();
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            if (m.uv.Length > 0) m.RecalculateTangents();
            return m;
        });

        public static GameObject Spawn(string name, Mesh mesh, Material mat, Transform parent, Vector3 localPos, Vector3 localScale,
            Quaternion? localRot = null, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot ?? Quaternion.identity;
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
    }
}
