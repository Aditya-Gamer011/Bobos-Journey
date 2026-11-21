using System.IO;
using UnityEditor;
using UnityEngine;

namespace BobosJourney.EditorTools
{
    /// <summary>
    /// Builds Bobo's skins from the artist's drawings: the front of the ball shows the drawing, the back shows the
    /// same stripes with the face painted out. Also cuts the HUD portrait. Rebuild Art runs this too.
    /// </summary>
    public static partial class ArtBaker
    {
        public const string DrawnBoboPath = "Assets/_Game/Art/Drawings/bobo_happy.png";
        const string DrawnDir = "Assets/_Game/Art/Drawings";
        const string PortraitPath = "Assets/_Game/Resources/BoboPortrait.png";

        const int BallW = 1024, BallH = 512; // equirectangular skin size
        const int PortraitSize = 256;

        // which drawing each expression uses; anything without its own drawing falls back to the happy one
        static readonly (string file, Expression[] expressions)[] DrawnFaces =
        {
            ("bobo_happy", new[] { Expression.Happy, Expression.Excited, Expression.Determined, Expression.Blink, Expression.Angry }),
            ("bobo_scared", new[] { Expression.Scared, Expression.Sad, Expression.Hurt }),
        };

        [MenuItem("Bobo/Use Drawn Bobo")]
        public static void UseDrawnBobo()
        {
            var art = AssetDatabase.LoadAssetAtPath<BoboArt>(ArtAssetPath);
            if (!art)
            {
                Debug.LogError("[Bobo] Run Rebuild Art first.");
                return;
            }
            if (ApplyDrawnBobo(art))
            {
                EditorUtility.SetDirty(art);
                AssetDatabase.SaveAssets();
                Debug.Log("[Bobo] Bobo now wears the drawn skin.");
            }
        }

        static bool ApplyDrawnBobo(BoboArt art)
        {
            var happy = BakeDrawn(DrawnBoboPath, "bobo_drawn", true);
            if (!happy) return false;
            foreach (Expression e in System.Enum.GetValues(typeof(Expression)))
                art.boboFaces[(int)e] = happy;
            foreach (var (file, expressions) in DrawnFaces)
            {
                if (file == "bobo_happy") continue;
                var skin = BakeDrawn($"{DrawnDir}/{file}.png", "bobo_drawn_" + file.Replace("bobo_", ""), false);
                if (skin)
                {
                    foreach (var e in expressions)
                        art.boboFaces[(int)e] = skin;
                }
            }
            return true;
        }

        static Texture2D BakeDrawn(string path, string skinName, bool makePortrait)
        {
            if (!File.Exists(path)) return null;
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            src.LoadImage(File.ReadAllBytes(path));
            int width = src.width, height = src.height;
            var srcPixels = src.GetPixels();

            // the ball is the bounding box of everything drawn
            int minX = width, maxX = -1, minY = height, maxY = -1;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    var c = srcPixels[y * width + x];
                    if (c.a > 0.15f && (c.r < 0.92f || c.g < 0.92f || c.b < 0.92f))
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            if (maxX < 0)
            {
                Debug.LogError("[Bobo] No ball found in " + path);
                return null;
            }
            float cx = (minX + maxX) / 2f, cy = (minY + maxY) / 2f;
            float rOuter = ((maxX - minX) + (maxY - minY)) / 4f + 0.5f;

            // inner edge of the hand-drawn outline: walk inward until the bright blue fill starts
            float rInner = 0;
            int rays = 0;
            for (int k = 0; k < 16; k++)
            {
                float a = k * Mathf.PI / 8f;
                for (float r = rOuter; r > rOuter * 0.6f; r -= 0.5f)
                {
                    var c = src.GetPixel(Mathf.RoundToInt(cx + Mathf.Cos(a) * r), Mathf.RoundToInt(cy + Mathf.Sin(a) * r));
                    if (c.a > 0.5f && c.grayscale > 0.5f)
                    {
                        rInner += r;
                        rays++;
                        break;
                    }
                }
            }
            rInner = rays > 0 ? rInner / rays - 2.5f : rOuter * 0.9f;

            // paint the face out: dark ink strokes are replaced by the nearest stripe colour along the row
            var faceless = (Color[])srcPixels.Clone();
            var ink = new bool[width * height];
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    var c = srcPixels[y * width + x];
                    if (Mathf.Max(c.r, Mathf.Max(c.g, c.b)) < 0.3f && (x - cx) * (x - cx) + (y - cy) * (y - cy) < rInner * rInner)
                    {
                        // grow over the anti-aliased edge
                        for (int dy = -3; dy <= 3; dy++)
                            for (int dx = -3; dx <= 3; dx++)
                                ink[(y + dy) * width + x + dx] = true;
                    }
                }
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    if (!ink[y * width + x]) continue;
                    int l = x, r = x;
                    while (l > minX && ink[y * width + l]) l--;
                    while (r < maxX && ink[y * width + r]) r++;
                    float t = (x - l) / (float)Mathf.Max(1, r - l);
                    faceless[y * width + x] = Color.Lerp(srcPixels[y * width + l], srcPixels[y * width + r], t);
                }
            var back = new Texture2D(width, height, TextureFormat.RGBA32, false);
            back.SetPixels(faceless);
            back.Apply();

            // wrap onto the sphere: front is +Z, longitude across, latitude up
            var px = new Color[BallW * BallH];
            for (int y = 0; y < BallH; y++)
            {
                float lat = ((y + 0.5f) / BallH - 0.5f) * Mathf.PI;
                for (int x = 0; x < BallW; x++)
                {
                    float lon = ((x + 0.5f) / BallW - 0.5f) * Mathf.PI * 2f;
                    var d = new Vector3(Mathf.Sin(lon) * Mathf.Cos(lat), Mathf.Sin(lat), Mathf.Cos(lon) * Mathf.Cos(lat));
                    float poleAngle = Mathf.Acos(Mathf.Clamp(Mathf.Abs(d.z), 0f, 1f));
                    // project straight on at the drawing's own scale so the face keeps its size; stop inside the
                    // drawn outline so the stripes carry on to the edge (the game draws its own outline)
                    float rad = Mathf.Min(Mathf.Sin(poleAngle) * rOuter, rInner - 1f);
                    var dir = new Vector2(-d.x, d.y);
                    dir = dir.sqrMagnitude > 1e-8f ? dir.normalized : Vector2.zero;
                    float sx = cx + dir.x * rad, sy = cy + dir.y * rad;
                    var c = (d.z >= 0 ? src : back).GetPixelBilinear((sx + 0.5f) / width, (sy + 0.5f) / height);
                    c.a = 1;
                    px[y * BallW + x] = c;
                }
            }
            var skin = Save(skinName, BallW, BallH, px, false);
            Object.DestroyImmediate(back);
            if (!makePortrait)
            {
                Object.DestroyImmediate(src);
                return skin;
            }

            // HUD portrait: the whole ball with its outline, on transparency
            float scale = (rOuter * 2f + 6f) / PortraitSize;
            var portraitPixels = new Color[PortraitSize * PortraitSize];
            for (int y = 0; y < PortraitSize; y++)
                for (int x = 0; x < PortraitSize; x++)
                {
                    float sx = cx + (x + 0.5f - PortraitSize / 2f) * scale, sy = cy + (y + 0.5f - PortraitSize / 2f) * scale;
                    var c = src.GetPixelBilinear(sx / width, sy / height);
                    float dist = Mathf.Sqrt((sx - cx) * (sx - cx) + (sy - cy) * (sy - cy));
                    c.a *= Mathf.Clamp01((rOuter - dist) / (1.4f * scale) + 0.5f);
                    portraitPixels[y * PortraitSize + x] = c;
                }
            var portrait = new Texture2D(PortraitSize, PortraitSize, TextureFormat.RGBA32, false);
            portrait.SetPixels(portraitPixels);
            portrait.Apply();
            File.WriteAllBytes(PortraitPath, portrait.EncodeToPNG());
            AssetDatabase.ImportAsset(PortraitPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(PortraitPath);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            Object.DestroyImmediate(src);
            Object.DestroyImmediate(portrait);
            return skin;
        }
    }
}
