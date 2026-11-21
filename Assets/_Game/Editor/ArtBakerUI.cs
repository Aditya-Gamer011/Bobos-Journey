using UnityEditor;
using UnityEngine;

namespace BobosJourney.EditorTools
{
    /// <summary>Procedural hand-inked UI sprites: ink box, torn parchment, brush stroke, splat, ring and line.</summary>
    public static partial class ArtBaker
    {
        static readonly Color InkUI = new Color(0.1f, 0.06f, 0.08f);

        [MenuItem("Bobo/Rebuild UI Art")]
        public static void BakeUIOnly()
        {
            var art = AssetDatabase.LoadAssetAtPath<BoboArt>(ArtAssetPath);
            BakeUIArt(art);
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
        }

        static void BakeUIArt(BoboArt art)
        {
            art.inkBox = SaveSprite("ui_inkbox", 256, InkBox(256, 7), 72);
            art.paperTorn = SaveSprite("ui_paper_torn", 512, PaperTorn(512), 130);
            art.brush = SaveSprite("ui_brush", 512, BrushStroke(512, 128, 3), 0, 128);
            art.splat = SaveSprite("ui_splat", 256, Splat(256, 11), 0);
            art.inkRing = SaveSprite("ui_inkring", 128, InkRing(128), 0);
            art.circle = SaveSprite("ui_circle", 128, Disc(128), 0);
            art.inkLine = Save("ui_inkline", 64, 512, InkLineV(64, 512), true);
        }

        /// <summary>A hand-inked stroke from a to b: wobbles, presses harder in the middle, overshoots the ends.</summary>
        static float HandLine(Vector2 p, Vector2 a, Vector2 b, float over, float halfW, float wobble, float seed)
        {
            Vector2 e = b - a;
            float len = e.magnitude;
            Vector2 dir = e / len;
            Vector2 nrm = new Vector2(-dir.y, dir.x);
            float t = Vector2.Dot(p - a, dir), d = Vector2.Dot(p - a, nrm);
            if (t < -over - halfW || t > len + over + halfW) return 999f;
            float u = t / len;
            float wob = (Mathf.PerlinNoise(seed, u * 2.3f) - 0.5f) * 2f * wobble + (Mathf.PerlinNoise(seed + 5.1f, u * 9f) - 0.5f) * wobble * 0.5f;
            float k = Mathf.Clamp01((t + over) / (len + 2f * over));
            float pressure = 0.45f + 0.55f * Mathf.Sin(k * Mathf.PI);
            float w = halfW * pressure * (0.85f + 0.3f * Mathf.PerlinNoise(seed + 9.7f, u * 6f));
            float endFade = Mathf.Min(t + over, len + over - t);
            float dist = Mathf.Abs(d - wob) - w;
            return endFade < 0 ? Mathf.Max(dist, -endFade) : dist;
        }

        static Color[] InkBox(int n, int seed)
        {
            var px = new Color[n * n];
            float margin = 22f, s = seed;
            var c00 = new Vector2(margin, margin);
            var c10 = new Vector2(n - margin, margin + 1.5f);
            var c11 = new Vector2(n - margin - 1f, n - margin);
            var c01 = new Vector2(margin + 1f, n - margin - 1f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    // fill printed slightly off-register from the line work
                    var q = p - new Vector2(3f, -3f);
                    float edgeN = (Mathf.PerlinNoise(q.x * 0.05f + s, q.y * 0.05f) - 0.5f) * 4f;
                    float inside = Mathf.Min(Mathf.Min(q.x - margin - 3f, n - margin - 3f - q.x), Mathf.Min(q.y - margin - 3f, n - margin - 3f - q.y)) + edgeN;
                    float fill = Mathf.Clamp01(inside / 1.2f + 0.5f);
                    float line = Mathf.Min(
                        Mathf.Min(HandLine(p, c00, c10, 9f, 3.2f, 2.2f, s + 1), HandLine(p, c10, c11, 7f, 2.8f, 2.2f, s + 2)),
                        Mathf.Min(HandLine(p, c11, c01, 10f, 3.4f, 2.2f, s + 3), HandLine(p, c01, c00, 8f, 2.9f, 2.2f, s + 4)));
                    float ink = Mathf.Clamp01(0.5f - line / 1.1f);
                    // paper grain in the fill
                    float grain = 0.94f + 0.06f * Mathf.PerlinNoise(x * 0.35f, y * 0.35f);
                    Color c = new Color(grain, grain, grain, fill);
                    c = Color.Lerp(c, new Color(InkUI.r, InkUI.g, InkUI.b, 1), ink);
                    c.a = Mathf.Max(fill, ink);
                    px[y * n + x] = c;
                }
            return px;
        }

        static Color[] PaperTorn(int n)
        {
            var px = new Color[n * n];
            float margin = 34f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float rough = (Mathf.PerlinNoise(x * 0.035f, y * 0.035f) - 0.5f) * 22f
                                + (Mathf.PerlinNoise(x * 0.16f + 7, y * 0.16f) - 0.5f) * 7f
                                + (Mathf.PerlinNoise(x * 0.6f, y * 0.6f + 3) - 0.5f) * 2.5f;
                    float d = Mathf.Min(Mathf.Min(x - margin, n - margin - x), Mathf.Min(y - margin, n - margin - y)) + rough;
                    float a = Mathf.Clamp01(d / 1.3f + 0.5f);
                    float fiber = 0.9f + 0.07f * Mathf.PerlinNoise(x * 0.08f, y * 0.5f) + 0.05f * Mathf.PerlinNoise(x * 0.9f, y * 0.9f);
                    float stain = Mathf.PerlinNoise(x * 0.012f + 11, y * 0.012f) * 0.12f;
                    var col = new Color(0.95f, 0.9f, 0.78f) * (fiber - stain);
                    float burn = Mathf.Clamp01(1f - d / 30f);
                    col = Color.Lerp(col, new Color(0.52f, 0.38f, 0.26f), burn * burn * 0.85f);
                    col = Color.Lerp(col, new Color(0.25f, 0.15f, 0.1f), Mathf.Clamp01(1f - d / 5f) * 0.8f);
                    col.a = a;
                    px[y * n + x] = col;
                }
            return px;
        }

        static Color[] BrushStroke(int w, int h, int seed)
        {
            // broad sumi-style stroke: solid body, ragged feathered edge, dry gaps only in the tail
            var px = new Color[w * h];
            float seedOffset = seed * 3.7f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                    float centre = 0.5f + (u - 0.5f) * 0.05f + (Mathf.PerlinNoise(u * 2.2f + seedOffset, 0.3f) - 0.5f) * 0.06f;
                    float halfH = 0.34f + 0.07f * Mathf.Sin(u * 2.8f + 0.4f) - 0.1f * Mathf.SmoothStep(0.6f, 1f, u);
                    float edge = (Mathf.PerlinNoise(u * 18f + seedOffset, v * 3f) - 0.5f) * 0.07f + (Mathf.PerlinNoise(u * 60f, v * 8f + seedOffset) - 0.5f) * 0.025f;
                    float vert = Mathf.Clamp01((halfH + edge - Mathf.Abs(v - centre)) * h / 2.5f);
                    // rounded, slightly blobby start; tapering, splitting tail
                    float startEdge = 0.035f + (Mathf.PerlinNoise(v * 6f + seedOffset, 1.1f) - 0.5f) * 0.03f + Mathf.Pow(Mathf.Abs(v - centre) * 2.2f, 2f) * 0.03f;
                    float endEdge = 0.93f - Mathf.Pow(Mathf.Abs(v - centre) * 2.4f, 2f) * 0.12f + (Mathf.PerlinNoise(v * 9f, seedOffset + 2f) - 0.5f) * 0.08f;
                    float across = Mathf.Clamp01((u - startEdge) * w / 3f) * Mathf.Clamp01((endEdge - u) * w / 4f);
                    float lane = Mathf.PerlinNoise(v * 11f + seedOffset, u * 3.5f);
                    float dry = Mathf.SmoothStep(0.62f, 1f, u);
                    float keep = Mathf.Clamp01((lane - dry * 0.55f) * 3f + 0.35f);
                    float a = vert * across * Mathf.Lerp(1f, keep, dry);
                    a *= 0.9f + 0.1f * Mathf.PerlinNoise(u * 40f, v * 40f); // ink density variation
                    px[y * w + x] = new Color(1, 1, 1, a);
                }
            return px;
        }

        static Color[] Splat(int n, int seed)
        {
            var px = new Color[n * n];
            var rnd = new System.Random(seed);
            var drops = new Vector3[9];
            for (int i = 0; i < drops.Length; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f, r = 0.68f + (float)rnd.NextDouble() * 0.26f;
                drops[i] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0.02f + (float)rnd.NextDouble() * 0.045f);
            }
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    var p = new Vector2((x + 0.5f) / n - 0.5f, (y + 0.5f) / n - 0.5f) * 2f;
                    float th = Mathf.Atan2(p.y, p.x);
                    float r = 0.5f + 0.09f * Mathf.Sin(th * 5f + 1.3f) + 0.06f * Mathf.Sin(th * 9f + 0.4f) + (Mathf.PerlinNoise(Mathf.Cos(th) * 2f + 4f, Mathf.Sin(th) * 2f + 4f) - 0.5f) * 0.18f;
                    float sd = p.magnitude - r;
                    foreach (var d in drops)
                        sd = Mathf.Min(sd, (p - new Vector2(d.x, d.y)).magnitude - d.z);
                    float a = Mathf.Clamp01(0.5f - sd * n * 0.5f / 1.2f);
                    px[y * n + x] = new Color(1, 1, 1, a);
                }
            return px;
        }

        static Color[] InkRing(int n)
        {
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    var p = new Vector2(x + 0.5f - n * 0.5f, y + 0.5f - n * 0.5f);
                    float th = Mathf.Atan2(p.y, p.x), u = (th + Mathf.PI) / (Mathf.PI * 2f);
                    float r = n * 0.42f + (Mathf.PerlinNoise(u * 4f, 1.7f) - 0.5f) * 3f;
                    float w = 2.6f + 1.4f * Mathf.Sin(u * Mathf.PI * 2f + 0.7f);
                    float a = Mathf.Clamp01(0.5f - (Mathf.Abs(p.magnitude - r) - w) / 1.1f);
                    px[y * n + x] = new Color(InkUI.r, InkUI.g, InkUI.b, a);
                }
            return px;
        }

        static Color[] Disc(int n)
        {
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = new Vector2(x + 0.5f - n * 0.5f, y + 0.5f - n * 0.5f).magnitude - n * 0.42f;
                    px[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01(0.5f - d / 1.1f));
                }
            return px;
        }

        static Color[] InkLineV(int w, int h)
        {
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h * Mathf.PI * 2f;
                float cx = w * 0.5f + Mathf.Sin(v * 2f) * 3f + Mathf.Sin(v * 5f + 1f) * 1.4f;
                float hw = 2.4f + 0.9f * Mathf.Sin(v * 3f + 0.5f);
                for (int x = 0; x < w; x++)
                    px[y * w + x] = new Color(1, 1, 1, Mathf.Clamp01(0.5f - (Mathf.Abs(x + 0.5f - cx) - hw) / 1.1f));
            }
            return px;
        }
    }
}
