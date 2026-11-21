using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BobosJourney.EditorTools
{
    /// <summary>Sets up import settings and materials for the Poly Haven scans and gathers Dreamscapes props into EnvArt.</summary>
    public static class EnvBuilder
    {
        const string PolyHavenDir = "Assets/_Game/Art/PolyHaven";
        const string MatDir = "Assets/_Game/Art/Materials";
        const string EnvPath = "Assets/_Game/Resources/EnvArt.asset";
        const string DreamscapesDir = "Assets/Dreamscapes";

        [MenuItem("Bobo/Build Environment Art")]
        public static void Build()
        {
            Directory.CreateDirectory(MatDir);
            FixTextureImports();
            FixModelImports();

            var env = AssetDatabase.LoadAssetAtPath<EnvArt>(EnvPath);
            if (env == null)
            {
                env = ScriptableObject.CreateInstance<EnvArt>();
                AssetDatabase.CreateAsset(env, EnvPath);
            }

            var triplanar = Shader.Find("Bobo/TriplanarPBR");
            env.island = TriplanarMaterial("Island", triplanar, "rock_face_03", 0.3f, "forest_ground_04", 0.33f, 0.5f, Color.white, new Color(0.78f, 0.9f, 0.74f));
            env.crumble = TriplanarMaterial("Crumble", triplanar, "rock_face_03", 0.45f, "forest_ground_04", 0.33f, 0.93f, new Color(0.92f, 0.76f, 0.62f), new Color(0.9f, 0.8f, 0.6f));
            env.stoneBare = TriplanarMaterial("StoneBare", triplanar, "rock_face_03", 0.35f, "forest_ground_04", 0.33f, -1f, Color.white, Color.white);
            env.wall = TriplanarMaterial("TowerWall", triplanar, "rustic_stone_wall_02", 0.26f, "forest_ground_04", 0.3f, 0.62f, Color.white, Color.white);
            env.wood = TriplanarMaterial("Wood", triplanar, "weathered_brown_planks", 0.45f, "forest_ground_04", 0.3f, -1f, Color.white, Color.white);
            env.wallCheap = TriplanarMaterial("TowerWallCheap", triplanar, "rustic_stone_wall_02", 0.26f, "forest_ground_04", 0.3f, -1f, Color.white, Color.white);
            EnableKeyword(env.wallCheap, "_UVMODE", "_UVMode");
            EnableKeyword(env.stoneBare, "_NOTOP", "_NoTop");
            EnableKeyword(env.wood, "_NOTOP", "_NoTop");

            env.emissive = SaveMat("Glow", () =>
            {
                var m = new Material(Shader.Find("Standard"));
                m.color = new Color(0.25f, 0.2f, 0.18f);
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(1f, 0.72f, 0.4f) * 2f);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                return m;
            });
            env.cloth = SaveMat("Cloth", () =>
            {
                var m = new Material(Shader.Find("Standard"));
                m.color = new Color(0.45f, 0.12f, 0.16f);
                m.SetFloat("_Glossiness", 0.12f);
                return m;
            });

            // every mesh inside each scanned rock FBX becomes a piece
            var meshes = new List<Mesh>();
            var mats = new List<Material>();
            foreach (var id in new[] { "rock_moss_set_01", "rock_moss_set_02", "boulder_01" })
            {
                var fbx = Directory.GetFiles($"{PolyHavenDir}/{id}", "*.fbx").FirstOrDefault();
                if (fbx == null) continue;
                var mat = SaveMat("Scan_" + id, () =>
                {
                    var m = new Material(Shader.Find("Bobo/ScannedPBR"));
                    m.SetTexture("_MainTex", ScanTexture(id, "diff"));
                    m.SetTexture("_BumpMap", ScanTexture(id, "nor_gl"));
                    m.SetTexture("_ARM", ScanTexture(id, "arm"));
                    m.SetColor("_MossColor", new Color(0.85f, 0.95f, 0.8f, 0.3f));
                    return m;
                });
                foreach (var mesh in AssetDatabase.LoadAllAssetsAtPath(fbx.Replace('\\', '/')).OfType<Mesh>())
                {
                    if (mesh.name.Contains("_LOD") && !mesh.name.EndsWith("LOD0")) continue;
                    meshes.Add(Lowpoly(mesh));
                    mats.Add(mat);
                }
            }
            env.rockMeshes = meshes.ToArray();
            env.rockMaterials = mats.ToArray();

            env.bushes = Prefabs($"{DreamscapesDir}/Nature/Trees/Models", "bush");
            env.saplings = Prefabs($"{DreamscapesDir}/Nature/Trees/Models", "sapling");
            env.pebbles = Prefabs($"{DreamscapesDir}/Nature/Rocks", "pebbles");
            env.crystals = Prefabs($"{DreamscapesDir}/Nature/Rocks", "crystal0");
            env.pillars = Prefabs($"{DreamscapesDir}/Nature/Rocks", "pillar");
            env.mushrooms = Directory.GetFiles($"{DreamscapesDir}/Nature/Mushrooms", "*.fbx").Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p.Replace('\\', '/'))).Where(g => g).ToArray();
            env.ambientFx = new[] { "spirits01", "fireflies01", "dust01", "fog01" }
                .Select(n => AssetDatabase.LoadAssetAtPath<GameObject>($"{DreamscapesDir}/ParticleSystems/{n}.prefab")).Where(g => g).ToArray();

            var foliage = Shader.Find("Bobo/Foliage");
            var cards = new List<Material>();
            foreach (var n in new[] { "grass01", "grass02", "grass03", "grass05", "flower01", "flower02", "flower03" })
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{DreamscapesDir}/Nature/Grass/{n}.tga");
                if (tex == null) continue;
                cards.Add(SaveMat("Card_" + n, () =>
                {
                    var m = new Material(foliage);
                    m.SetTexture("_MainTex", tex);
                    m.SetColor("_Color", n.StartsWith("grass") ? new Color(0.82f, 0.88f, 0.8f) : Color.white);
                    m.enableInstancing = true;
                    return m;
                }));
            }
            env.grassCards = cards.ToArray();

            EditorUtility.SetDirty(env);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Bobo] Environment art: {env.rockMeshes.Length} scanned rocks, {env.bushes.Length} bushes, {env.mushrooms.Length} mushrooms, {cards.Count} grass cards.");
        }

        const string LowpolyDir = PolyHavenDir + "/Lowpoly";

        /// <summary>
        /// Saves a game-ready copy of a scan, vertex-clustered to roughly a thousand triangles.
        /// Positions are shared per grid cell (no cracks); UV seams stay split so the textures still line up.
        /// </summary>
        static Mesh Lowpoly(Mesh src, int grid = 20)
        {
            Directory.CreateDirectory(LowpolyDir);
            string path = $"{LowpolyDir}/{src.name}_low.asset";
            var verts = src.vertices;
            var normals = src.normals;
            var uvs = src.uv;
            var tris = src.triangles;
            var bounds = src.bounds;
            float cell = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)) / grid;

            var cellOf = new long[verts.Length];
            var cellPos = new Dictionary<long, Vector3>();
            var cellCount = new Dictionary<long, int>();
            for (int i = 0; i < verts.Length; i++)
            {
                var q = (verts[i] - bounds.min) / cell;
                long key = ((long)Mathf.FloorToInt(q.x) * 73856093L) ^ ((long)Mathf.FloorToInt(q.y) * 19349663L) ^ ((long)Mathf.FloorToInt(q.z) * 83492791L);
                cellOf[i] = key;
                cellPos[key] = (cellPos.TryGetValue(key, out var p) ? p : Vector3.zero) + verts[i];
                cellCount[key] = (cellCount.TryGetValue(key, out var c) ? c : 0) + 1;
            }

            var outIndex = new Dictionary<(long, int, int), int>();
            var outVerts = new List<Vector3>();
            var outNormals = new List<Vector3>();
            var outUvs = new List<Vector2>();
            var uvCount = new List<int>();
            var remap = new int[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                var k = (cellOf[i], Mathf.FloorToInt(uvs[i].x * 12f), Mathf.FloorToInt(uvs[i].y * 12f));
                if (!outIndex.TryGetValue(k, out int o))
                {
                    o = outVerts.Count;
                    outIndex[k] = o;
                    outVerts.Add(cellPos[cellOf[i]] / cellCount[cellOf[i]]);
                    outNormals.Add(Vector3.zero);
                    outUvs.Add(Vector2.zero);
                    uvCount.Add(0);
                }
                outNormals[o] += normals[i];
                outUvs[o] += uvs[i];
                uvCount[o]++;
                remap[i] = o;
            }
            for (int i = 0; i < outVerts.Count; i++)
            {
                outNormals[i] = outNormals[i].normalized;
                outUvs[i] /= uvCount[i];
            }

            var outTris = new List<int>();
            for (int i = 0; i < tris.Length; i += 3)
            {
                int a = remap[tris[i]], b = remap[tris[i + 1]], c = remap[tris[i + 2]];
                if (a == b || b == c || a == c) continue;
                outTris.Add(a);
                outTris.Add(b);
                outTris.Add(c);
            }

            var mesh = new Mesh { name = src.name + "_low" };
            if (outVerts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(outVerts);
            mesh.SetNormals(outNormals);
            mesh.SetUVs(0, outUvs);
            mesh.SetTriangles(outTris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static void EnableKeyword(Material m, string kw, string prop)
        {
            m.EnableKeyword(kw);
            m.SetFloat(prop, 1);
            EditorUtility.SetDirty(m);
        }

        static GameObject[] Prefabs(string dir, string prefix) =>
            Directory.GetFiles(dir, prefix + "*.prefab").Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p.Replace('\\', '/'))).Where(g => g).ToArray();

        static Texture2D ScanTexture(string id, string map)
        {
            var f = Directory.GetFiles($"{PolyHavenDir}/{id}", $"*_{map}_*.jpg").FirstOrDefault();
            return f == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(f.Replace('\\', '/'));
        }

        static Material TriplanarMaterial(string name, Shader sh, string side, float sideScale, string top, float topScale, float topAmount, Color tint, Color topTint)
        {
            return SaveMat(name, () =>
            {
                var m = new Material(sh);
                m.SetTexture("_MainTex", ScanTexture(side, "diff"));
                m.SetTexture("_BumpMap", ScanTexture(side, "nor_gl"));
                m.SetTexture("_ARM", ScanTexture(side, "arm"));
                m.SetFloat("_Scale", sideScale);
                m.SetTexture("_TopTex", ScanTexture(top, "diff"));
                m.SetTexture("_TopBump", ScanTexture(top, "nor_gl"));
                m.SetTexture("_TopARM", ScanTexture(top, "arm"));
                m.SetFloat("_TopScale", topScale);
                m.SetFloat("_TopAmount", topAmount);
                m.SetColor("_Color", tint);
                m.SetColor("_TopColor", topTint);
                m.enableInstancing = true;
                return m;
            });
        }

        static Material SaveMat(string name, System.Func<Material> make)
        {
            string path = $"{MatDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            var fresh = make();
            fresh.name = name;
            if (existing != null)
            {
                existing.shader = fresh.shader;
                existing.CopyPropertiesFromMaterial(fresh);
                existing.shaderKeywords = fresh.shaderKeywords;
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(fresh, path);
            return fresh;
        }

        static void FixTextureImports()
        {
            foreach (var f in Directory.GetFiles(PolyHavenDir, "*.jpg", SearchOption.AllDirectories))
            {
                var path = f.Replace('\\', '/');
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) continue;
                bool normal = path.Contains("_nor_gl_"), data = path.Contains("_arm_");
                var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                if (imp.textureType == type && imp.sRGBTexture == !(normal || data) && imp.maxTextureSize == 2048) continue;
                imp.textureType = type;
                imp.sRGBTexture = !(normal || data);
                imp.maxTextureSize = 2048;
                imp.anisoLevel = 8;
                imp.mipmapEnabled = true;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.textureCompression = TextureImporterCompression.Compressed;
                imp.SaveAndReimport();
            }
        }

        static void FixModelImports()
        {
            foreach (var f in Directory.GetFiles(PolyHavenDir, "*.fbx", SearchOption.AllDirectories))
            {
                var imp = AssetImporter.GetAtPath(f.Replace('\\', '/')) as ModelImporter;
                if (imp == null || imp.materialImportMode == ModelImporterMaterialImportMode.None) continue;
                imp.materialImportMode = ModelImporterMaterialImportMode.None;
                imp.importTangents = ModelImporterTangents.CalculateMikk;
                imp.SaveAndReimport();
            }
        }
    }
}
