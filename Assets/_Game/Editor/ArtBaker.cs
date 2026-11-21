using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BobosJourney.EditorTools
{
    /// <summary>Bakes the game's procedural textures (surfaces, FX and UI sprites) into BoboArt. Menu: Bobo > Rebuild Art.</summary>
    public static partial class ArtBaker
    {
        public const string GenDir = "Assets/_Game/Art/Generated";
        public const string ArtAssetPath = "Assets/_Game/Resources/BoboArt.asset";

        static readonly Color Ink = new Color(0.17f, 0.09f, 0.10f);

        [MenuItem("Bobo/Rebuild Art")]
        public static void BakeAll()
        {
            Directory.CreateDirectory(GenDir);
            Directory.CreateDirectory(Path.GetDirectoryName(ArtAssetPath));

            var art = AssetDatabase.LoadAssetAtPath<BoboArt>(ArtAssetPath);
            if (art == null)
            {
                art = ScriptableObject.CreateInstance<BoboArt>();
                AssetDatabase.CreateAsset(art, ArtAssetPath);
            }

            art.toon = Shader.Find("Bobo/Toon");
            art.fx = Shader.Find("Bobo/FX");
            art.portal = Shader.Find("Bobo/Portal");
            art.sky = Shader.Find("Bobo/Sky");

            if (!ApplyDrawnBobo(art))
                Debug.LogError("[Bobo] Bobo's drawings are missing from Assets/_Game/Art/Drawings.");

            art.stone = Save("stone", 512, 512, Stone(512), true);
            art.wood = Save("wood", 512, 512, Wood(512), true);
            art.grass = Save("grass", 512, 512, Grass(512), true);
            art.jelly = Save("jelly", 256, 256, Jelly(256), true);

            art.blob = Save("blob", 128, 128, Radial(128, r => Mathf.Pow(1f - Mathf.SmoothStep(0.15f, 1f, r), 1.6f)), false);
            art.softDot = Save("softdot", 64, 64, Radial(64, r => Mathf.Pow(1f - Mathf.Clamp01(r), 2f)), false);
            art.star = Save("star", 128, 128, Star(128), false);

            BakeUIArt(art);

            AssignFont(art);
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Bobo] Art rebuilt.");
        }

        public const string FontPath = "Assets/_Game/Art/Fonts/Bangers-Regular.ttf";

        /// <summary>Assigns the comic lettering font (Bangers, SIL OFL 1.1, licence beside the .ttf).</summary>
        [MenuItem("Bobo/Assign Comic Font")]
        public static void AssignFont() => AssignFont(AssetDatabase.LoadAssetAtPath<BoboArt>(ArtAssetPath));

        static void AssignFont(BoboArt art)
        {
            if (art == null) return;
            art.font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
        }

        #region Saving

        static Texture2D Save(string name, int w, int h, Color[] px, bool tile, bool linear = false)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            tex.Apply();
            string path = $"{GenDir}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = !linear;
            importer.alphaIsTransparency = true;
            importer.wrapMode = tile ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Sprite SaveSprite(string name, int w, Color[] px, int border, int h = -1)
        {
            if (h < 0) h = w;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            tex.Apply();
            string path = $"{GenDir}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spriteBorder = new Vector4(border, border, border, border);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        #endregion

        #region Math helpers

        static float Hash(float x, float y, float z)
        {
            float h = Mathf.Sin(x * 127.1f + y * 311.7f + z * 74.7f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        static float Noise(Vector3 p)
        {
            float ix = Mathf.Floor(p.x), iy = Mathf.Floor(p.y), iz = Mathf.Floor(p.z);
            float fx = p.x - ix, fy = p.y - iy, fz = p.z - iz;
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            fz = fz * fz * (3 - 2 * fz);

            float n000 = Hash(ix, iy, iz);
            float n100 = Hash(ix + 1, iy, iz);
            float n010 = Hash(ix, iy + 1, iz);
            float n110 = Hash(ix + 1, iy + 1, iz);
            float n001 = Hash(ix, iy, iz + 1);
            float n101 = Hash(ix + 1, iy, iz + 1);
            float n011 = Hash(ix, iy + 1, iz + 1);
            float n111 = Hash(ix + 1, iy + 1, iz + 1);

            float x00 = Mathf.Lerp(n000, n100, fx);
            float x10 = Mathf.Lerp(n010, n110, fx);
            float x01 = Mathf.Lerp(n001, n101, fx);
            float x11 = Mathf.Lerp(n011, n111, fx);
            return Mathf.Lerp(Mathf.Lerp(x00, x10, fy), Mathf.Lerp(x01, x11, fy), fz);
        }

        static float Fbm(Vector3 p, int octaves = 4)
        {
            float sum = 0, amplitude = 0.5f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Noise(p) * amplitude;
                p = p * 2.03f + new Vector3(1.7f, 9.2f, 3.1f);
                amplitude *= 0.5f;
            }
            return sum;
        }

        /// <summary>Tileable 2D noise on a torus (period 1).</summary>
        static float TileNoise(float u, float v, float freq, int octaves = 4)
        {
            float a1 = u * Mathf.PI * 2f, a2 = v * Mathf.PI * 2f;
            var p = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0) * freq * 0.3f + new Vector3(0, 0, 0);
            var q = new Vector3(Mathf.Cos(a2), Mathf.Sin(a2), 0) * freq * 0.3f;
            return Fbm(new Vector3(p.x + q.y * 0.5f + 11f, p.y + q.x * 0.7f, q.x + q.y + 5f), octaves);
        }

        static float Coverage(float sdf, float aa) => Mathf.Clamp01(0.5f - sdf / aa);

        static Vector2 Rotate(Vector2 p, float deg)
        {
            float rad = deg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            return new Vector2(p.x * cos + p.y * sin, -p.x * sin + p.y * cos);
        }

        // 2D signed distance functions (negative inside)
        static float SdEllipse(Vector2 p, Vector2 c, Vector2 r, float rotDeg = 0)
        {
            var q = Rotate(p - c, rotDeg);
            float k0 = new Vector2(q.x / r.x, q.y / r.y).magnitude;
            float k1 = new Vector2(q.x / (r.x * r.x), q.y / (r.y * r.y)).magnitude;
            if (k1 < 1e-6f) return -Mathf.Min(r.x, r.y);
            return k0 * (k0 - 1f) / k1;
        }



        /// <summary>Tapered stroke, thicker in the middle like an inked brush line.</summary>
        static float SdBrush(Vector2 p, Vector2 a, Vector2 b, float halfWidth)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Mathf.Max(1e-6f, Vector2.Dot(ba, ba)));
            float w = halfWidth * (0.55f + 0.45f * Mathf.Sin(h * Mathf.PI));
            return (pa - ba * h).magnitude - w;
        }


        static float SdStar(Vector2 p, float r, int n, float m)
        {
            // iq's star SDF: n points, m in [2, n]
            float an = Mathf.PI / n, en = Mathf.PI / m;
            Vector2 acs = new Vector2(Mathf.Cos(an), Mathf.Sin(an));
            Vector2 ecs = new Vector2(Mathf.Cos(en), Mathf.Sin(en));
            float bn = Mathf.Repeat(Mathf.Atan2(p.x, p.y), 2f * an) - an;
            p = p.magnitude * new Vector2(Mathf.Cos(bn), Mathf.Abs(Mathf.Sin(bn)));
            p -= r * acs;
            p += ecs * Mathf.Clamp(-Vector2.Dot(p, ecs), 0f, r * acs.y / ecs.y);
            return p.magnitude * Mathf.Sign(p.x);
        }

        #endregion

        #region Surfaces

        // all tile seamlessly; values stay near white so the material colours do the tinting

        static Color[] Stone(int n)
        {
            var rnd = new System.Random(7);
            int count = 26;
            var pts = new Vector2[count];
            var shade = new float[count];
            for (int i = 0; i < count; i++)
            {
                pts[i] = new Vector2((float)rnd.NextDouble(), (float)rnd.NextDouble());
                shade[i] = (float)rnd.NextDouble();
            }
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                    float warp = TileNoise(u, v, 6f, 3) * 0.05f;
                    var p = new Vector2(u + warp, v - warp);
                    float d1 = 9, d2 = 9;
                    int id = 0;
                    for (int i = 0; i < count; i++)
                        for (int ox = -1; ox <= 1; ox++)
                            for (int oy = -1; oy <= 1; oy++)
                            {
                                float d = (p - (pts[i] + new Vector2(ox, oy))).magnitude;
                                if (d < d1)
                                {
                                    d2 = d1;
                                    d1 = d;
                                    id = i;
                                }
                                else if (d < d2) d2 = d;
                            }
                    float edge = d2 - d1;
                    float grout = Mathf.SmoothStep(0.012f, 0.02f, edge);
                    float bevel = Mathf.SmoothStep(0.02f, 0.045f, edge);
                    float speck = TileNoise(u, v, 40f, 2);
                    float val = 0.86f + (shade[id] - 0.5f) * 0.12f + (speck - 0.5f) * 0.06f;
                    val = Mathf.Lerp(val + 0.07f, val, bevel); // light chipped rim on every stone
                    val = Mathf.Lerp(0.56f, val, grout);
                    px[y * n + x] = new Color(val * 1.0f, val * 0.98f, val * 1.02f, 1);
                }
            return px;
        }

        static Color[] Wood(int n)
        {
            var px = new Color[n * n];
            int planks = 4;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                    int plank = Mathf.FloorToInt(v * planks);
                    float fy = v * planks - plank;
                    float grain = Mathf.Sin((fy * 9f + TileNoise(u, v * 0.25f + plank * 0.17f, 5f, 3) * 6f) * Mathf.PI);
                    float val = 0.82f + Hash(plank, 0, 5) * 0.1f + grain * 0.045f;
                    float seam = Mathf.Min(fy, 1f - fy);
                    val = Mathf.Lerp(0.5f, val, Mathf.SmoothStep(0.0f, 0.035f, seam));
                    float knotDist = (new Vector2(Mathf.Repeat(u - Hash(plank, 1, 1), 1f), fy) - new Vector2(0.5f, 0.5f)).magnitude;
                    val -= Mathf.SmoothStep(0.07f, 0.03f, knotDist) * 0.12f;
                    px[y * n + x] = new Color(val, val * 0.96f, val * 0.92f, 1);
                }
            return px;
        }

        static Color[] Grass(int n)
        {
            var px = new Color[n * n];
            for (int i = 0; i < px.Length; i++)
            {
                int x = i % n, y = i / n;
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                float val = 0.86f + (TileNoise(u, v, 5f, 3) - 0.5f) * 0.14f;
                px[i] = new Color(val, val, val, 1);
            }

            // blade strokes
            var rnd = new System.Random(3);
            for (int k = 0; k < 420; k++)
            {
                float cx = (float)rnd.NextDouble(), cy = (float)rnd.NextDouble();
                float len = 0.012f + (float)rnd.NextDouble() * 0.014f, ang = -0.4f + (float)rnd.NextDouble() * 0.8f;
                bool light = rnd.NextDouble() > 0.55;
                var a = new Vector2(cx, cy);
                var b = a + new Vector2(Mathf.Sin(ang), Mathf.Cos(ang)) * len;
                int r = Mathf.CeilToInt(len * n) + 3;
                for (int oy = -r; oy <= r; oy++)
                    for (int ox = -r; ox <= r; ox++)
                    {
                        int px0 = Mathf.FloorToInt(cx * n) + ox, py0 = Mathf.FloorToInt(cy * n) + oy;
                        int wx = ((px0 % n) + n) % n, wy = ((py0 % n) + n) % n;
                        var p = new Vector2((px0 + 0.5f) / n, (py0 + 0.5f) / n);
                        float sd = SdBrush(p, a, b, 0.0022f);
                        float cover = Coverage(sd, 1.2f / n);
                        if (cover <= 0) continue;
                        int idx = wy * n + wx;
                        float target = light ? 0.98f : 0.68f;
                        float val = Mathf.Lerp(px[idx].r, target, cover * 0.8f);
                        px[idx] = new Color(val, val, val, 1);
                    }
            }
            return px;
        }

        static Color[] Jelly(int n)
        {
            var px = new Color[n * n];
            var rnd = new System.Random(11);
            var spots = new List<Vector3>();
            for (int i = 0; i < 9; i++)
                spots.Add(new Vector3((float)rnd.NextDouble(), (float)rnd.NextDouble(), 0.035f + (float)rnd.NextDouble() * 0.05f));
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    var p = new Vector2((x + 0.5f) / n, (y + 0.5f) / n);
                    float val = 0.9f;
                    foreach (var s in spots)
                        for (int ox = -1; ox <= 1; ox++)
                            for (int oy = -1; oy <= 1; oy++)
                            {
                                float sd = (p - new Vector2(s.x + ox, s.y + oy)).magnitude - s.z;
                                val = Mathf.Lerp(val, 1f, Coverage(sd, 1.5f / n));
                            }
                    px[y * n + x] = new Color(val, val, val, 1);
                }
            return px;
        }

        #endregion

        #region FX

        static Color[] Radial(int n, Func<float, float> alpha)
        {
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float r = new Vector2((x + 0.5f) / n - 0.5f, (y + 0.5f) / n - 0.5f).magnitude * 2f;
                    px[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01(alpha(r)));
                }
            return px;
        }

        static Color[] Star(int n)
        {
            var px = new Color[n * n];
            Color fill = Mats.Hex("#ffd75e");
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    var p = new Vector2((x + 0.5f) / n - 0.5f, (y + 0.5f) / n - 0.5f) * 2f;
                    float sd = SdStar(p, 0.78f, 5, 2.6f) - 0.06f;
                    float aa = 2.5f / n;
                    float outer = Coverage(sd - 0.07f, aa);
                    float inner = Coverage(sd, aa);
                    Color c = Color.Lerp(new Color(Ink.r, Ink.g, Ink.b, outer), new Color(fill.r, fill.g, fill.b, 1), inner);
                    float highlight = Coverage(SdEllipse(p, new Vector2(-0.12f, 0.2f), new Vector2(0.14f, 0.08f), 30f), aa);
                    c = Color.Lerp(c, new Color(1f, 0.97f, 0.8f, c.a), highlight * inner);
                    c.a = Mathf.Max(outer, inner);
                    px[y * n + x] = c;
                }
            return px;
        }

        #endregion
    }
}
