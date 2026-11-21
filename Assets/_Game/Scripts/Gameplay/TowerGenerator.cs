using System.Collections.Generic;
using UnityEngine;

namespace BobosJourney
{
    /// <summary>Builds the tower walls and a spiralling platform path ahead of Bobo from a fixed seed, so every run is the same climb.</summary>
    public class TowerGenerator : MonoBehaviour
    {
        [Header("Tower")]
        public Vector3 center = new Vector3(80, 0, 80);
        public float innerRadius = 43f;
        public float wallThickness = 2f;
        public float wallSection = 40f;
        public float baseY = 0f;
        public bool buildPitFloor = true;

        [Header("Path")]
        public int seed = 20261121;
        public Vector3 startPoint = new Vector3(80, 0, 58);
        public float lookAhead = 110f;
        public float checkpointEvery = 45f;
        public float zoneHeight = 100f;

        [Header("Runtime")]
        public Transform player;

        public struct Zone
        {
            public string name;
            public Color stone, top, accent, wall, wood;

            public Zone(string name, string stone, string top, string accent, string wall, string wood)
            {
                this.name = name;
                this.stone = Mats.Hex(stone);
                this.top = Mats.Hex(top);
                this.accent = Mats.Hex(accent);
                this.wall = Mats.Hex(wall);
                this.wood = Mats.Hex(wood);
            }
        }

        public static readonly Zone[] Zones =
        {
            new Zone("The Sunken Courtyard", "#a9a1c4", "#82c46c", "#ffd166", "#8b84ad", "#b98a5e"),
            new Zone("Rosebrick Stair",      "#d9a0ae", "#f6c9d4", "#ff6f9d", "#b9849a", "#c98f73"),
            new Zone("The Azure Climb",      "#93b6da", "#cdeeff", "#5fd0ff", "#7593bb", "#9c8f86"),
            new Zone("Amber Spires",         "#d8b17b", "#f4d27a", "#ff9a3c", "#b48d61", "#a77245"),
            new Zone("The Violet Hush",      "#a08bcb", "#c9bbff", "#c08cff", "#6f5c9c", "#8d6d9a"),
        };

        public Zone ZoneAt(float worldY)
        {
            int i = Mathf.FloorToInt(Mathf.Max(0, worldY - baseY) / zoneHeight);
            return Zones[i % Zones.Length];
        }

        public int ZoneIndexAt(float worldY) => Mathf.FloorToInt(Mathf.Max(0, worldY - baseY) / zoneHeight);

        public readonly List<Checkpoint> checkpoints = new List<Checkpoint>();

        /// <summary>Layer for everything the tower builds; it has its own light.</summary>
        public const int TowerLayer = 8;
        /// <summary>Grass and small props: same lighting, much shorter draw distance.</summary>
        public const int DetailLayer = 9;

        int pathLaid;
        int wallLaid;

        public static void SetLayer(Transform t, int layer)
        {
            if (layer == TowerLayer && (t.name == "Grass" || t.name.StartsWith("Detail_"))) layer = DetailLayer;
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++) SetLayer(t.GetChild(i), layer);
        }

        void LayNew()
        {
            for (; pathLaid < pathRoot.childCount; pathLaid++)
            {
                var t = pathRoot.GetChild(pathLaid);
                SetLayer(t, TowerLayer);
                // static pieces get merged into static batches to cut draw calls
                if (t.name == "Island" || t.name == "Stepping Stone" || t.name == "Beam") Batch(t, t.gameObject);
            }
            int firstWall = wallLaid;
            for (; wallLaid < wallRoot.childCount; wallLaid++) SetLayer(wallRoot.GetChild(wallLaid), TowerLayer);
            if (wallLaid > firstWall)
            {
                var section = new List<GameObject>();
                for (int i = firstWall; i < wallLaid; i++)
                    foreach (var mf in wallRoot.GetChild(i).GetComponentsInChildren<MeshFilter>())
                        if (mf.sharedMesh && mf.sharedMesh.isReadable && mf.GetComponent<MeshRenderer>()) section.Add(mf.gameObject);
                if (section.Count > 1) StaticBatchingUtility.Combine(section.ToArray(), wallRoot.gameObject);
            }
        }

        static void Batch(Transform root, GameObject batchRoot)
        {
            var list = new List<GameObject>();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh && mf.sharedMesh.isReadable && mf.GetComponent<MeshRenderer>()) list.Add(mf.gameObject);
            if (list.Count > 1) StaticBatchingUtility.Combine(list.ToArray(), batchRoot);
        }

        System.Random rng;
        System.Random visualRng;
        Vector3 lastSpot; // top-surface centre of the last walkable spot
        float lastSpotRadius;
        float ringTarget;
        float wallTop;
        float nextCheckpointY;
        string lastKind = "";
        int stepCount;
        Transform pathRoot;
        Transform wallRoot;
        Mesh colCylinder;

        float R => innerRadius;

        void Awake()
        {
            rng = new System.Random(seed);
            visualRng = new System.Random(seed * 7 + 3);
            pathRoot = new GameObject("Path").transform;
            pathRoot.SetParent(transform, false);
            wallRoot = new GameObject("Walls").transform;
            wallRoot.SetParent(transform, false);
            colCylinder = ProceduralMeshes.Lathe("colcyl", new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) }, 16);
            lastSpot = startPoint;
            waypoints.Add(startPoint);
            lastSpotRadius = 1.2f;
            ringTarget = 22f;
            wallTop = baseY;
            nextCheckpointY = startPoint.y + checkpointEvery;
            BuildBase();
            Generate(startPoint.y + lookAhead + 40f);
        }

        void Update()
        {
            if (player == null) return;
            if (player.position.y + lookAhead > lastSpot.y) Generate(player.position.y + lookAhead);
            while (wallTop < player.position.y + 160f) BuildWallSection();
        }

        /// <summary>Generate until flag number k (1-based) exists, and return it.</summary>
        public Checkpoint EnsureCheckpoint(int k)
        {
            int guard = 0;
            while (checkpoints.Count < k && guard++ < 200) Generate(lastSpot.y + 20f);
            return k >= 1 && k <= checkpoints.Count ? checkpoints[k - 1] : null;
        }

        public float NextCheckpointHeightAbove(float y)
        {
            foreach (var cp in checkpoints)
            {
                if (cp.transform.position.y > y + 0.5f) return cp.transform.position.y;
            }
            return nextCheckpointY;
        }

        float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        bool Chance(float p) => rng.NextDouble() < p;
        float Difficulty(float y) => Mathf.Clamp01((y - startPoint.y) / 450f);

        #region Path

        void Generate(float untilY)
        {
            int guard = 0;
            while (lastSpot.y < untilY && guard++ < 400)
            {
                if (lastSpot.y >= nextCheckpointY)
                {
                    PlaceCheckpoint();
                    nextCheckpointY = lastSpot.y + checkpointEvery;
                    continue;
                }
                stepCount++;
                if (stepCount % 6 == 0) ringTarget = Rand(15f, R - 9f);
                string kind = PickSegment();
                switch (kind)
                {
                    case "stairs": Stairs(); break;
                    case "beam": Beam(); break;
                    case "mover": Mover(); break;
                    case "crumble": Crumbles(); break;
                    case "bounce": Bounce(); break;
                    case "spinner": Spinner(); break;
                    case "sweeper": SweeperPad(); break;
                    default: SimplePad(); break;
                }
                lastKind = kind;
            }
            LayNew();
        }

        string PickSegment()
        {
            float h = lastSpot.y - startPoint.y;
            var options = new List<(string, float)> { ("pad", 3f), ("stairs", 2f) };
            if (h > 12) options.Add(("beam", 1.5f));
            if (h > 20) options.Add(("bounce", 0.9f));
            if (h > 30) options.Add(("mover", 1.6f));
            if (h > 40) options.Add(("crumble", 1.4f));
            if (h > 60) options.Add(("spinner", 1.1f));
            if (h > 85) options.Add(("sweeper", 1.1f));
            float total = 0;
            foreach (var o in options)
            {
                if (o.Item1 != lastKind || o.Item1 == "pad") total += o.Item2;
            }
            float pick = Rand(0, total);
            foreach (var o in options)
            {
                if (o.Item1 == lastKind && o.Item1 != "pad") continue;
                pick -= o.Item2;
                if (pick <= 0) return o.Item1;
            }
            return "pad";
        }

        /// <summary>Next platform centre, 'gap' edge-to-edge from the current spot, spiralling around the tower.</summary>
        Vector3 Next(float gap, float dy, float footR, out Vector3 dir)
        {
            float dist = lastSpotRadius + gap + footR;
            Vector3 radial = Flat(lastSpot - center).normalized;
            if (radial.sqrMagnitude < 0.01f) radial = Vector3.right;
            Vector3 tangent = Vector3.Cross(radial, Vector3.up); // counter-clockwise when seen from above
            float ring = Flat(lastSpot - center).magnitude;
            float steer = Mathf.Clamp((ringTarget - ring) * 0.05f, -0.6f, 0.6f) + Rand(-0.35f, 0.35f);
            Vector3 candidate = lastSpot;
            dir = tangent;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                dir = (tangent * Mathf.Cos(steer) + radial * Mathf.Sin(steer)).normalized;
                candidate = lastSpot + dir * dist + Vector3.up * dy;
                float r = Flat(candidate - center).magnitude;
                if (r > R - footR - 3f) steer -= 0.3f;
                else if (r < 8f + footR) steer += 0.3f;
                else break;
            }
            return candidate;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);

        void Advance(Vector3 top, float r)
        {
            lastSpot = top;
            lastSpotRadius = r;
            waypoints.Add(top);
        }

        /// <summary>Every walkable spot of the climb, in order (start, each platform, each flag).</summary>
        public readonly List<Vector3> waypoints = new List<Vector3>();

        /// <summary>Flat direction from 'from' toward the next waypoint after the nearest one.</summary>
        public Vector3 HeadingFrom(Vector3 from)
        {
            int nearest = 0;
            float nearestSqrDist = float.MaxValue;
            for (int i = 0; i < waypoints.Count; i++)
            {
                float sqrDist = (waypoints[i] - from).sqrMagnitude;
                if (sqrDist < nearestSqrDist)
                {
                    nearestSqrDist = sqrDist;
                    nearest = i;
                }
            }
            for (int j = nearest + 1; j < waypoints.Count; j++)
            {
                var dir = Flat(waypoints[j] - from);
                if (dir.sqrMagnitude > 1.5f * 1.5f) return dir.normalized;
            }
            var fallback = Vector3.Cross(Flat(from - center).normalized, Vector3.up);
            return fallback.sqrMagnitude > 0.01f ? fallback : Vector3.forward;
        }

        float Gap(float d) => Mathf.Min(4.6f, Rand(1.3f, 2.4f + 1.2f * d) + d * 0.6f);
        float Rise(float d, float gap) => gap > 3.8f ? Rand(0.2f, 0.9f) : Rand(0.5f, 1.3f + 0.6f * d);

        void SimplePad()
        {
            float d = Difficulty(lastSpot.y);
            float r = Mathf.Max(1.0f, Rand(1.5f, 2.5f) - d * 0.9f);
            float gap = Gap(d);
            var c = Next(gap, Rise(d, gap), r, out _);
            Island(c, r, ZoneAt(c.y), true);
            Advance(c, r);
        }

        void Stairs()
        {
            float d = Difficulty(lastSpot.y);
            int n = rng.Next(3, 6);
            for (int i = 0; i < n; i++)
            {
                float r = Rand(0.9f, 1.25f) - d * 0.15f;
                float gap = Rand(1.1f, 2.0f + d);
                var c = Next(gap, Rand(0.7f, 1.2f), r, out _);
                Island(c, r, ZoneAt(c.y), false, small: true);
                Advance(c, r);
            }
        }

        void Beam()
        {
            float d = Difficulty(lastSpot.y);
            float len = Rand(5f, 9f);
            float width = Mathf.Lerp(1.6f, 0.75f, d) + Rand(-0.1f, 0.15f);
            float rise = Rand(0.5f, 2.2f);
            float gap = Rand(0.8f, 1.6f);
            var start = Next(gap, Rand(0.2f, 0.7f), 0.01f, out var dir);
            var end = start + Flat(dir) * len + Vector3.up * rise;
            var mid = (start + end) * 0.5f;
            var z = ZoneAt(mid.y);
            var root = new GameObject("Beam").transform;
            root.SetParent(pathRoot, false);
            root.position = mid - Vector3.up * 0.25f;
            root.rotation = Quaternion.LookRotation(end - start, Vector3.up);
            var mesh = ProceduralMeshes.RoundedBox(new Vector3(1, 1, 1), 0.18f);
            var body = ProceduralMeshes.Spawn("Plank", mesh, WoodMat(z), root, Vector3.zero, new Vector3(width, 0.5f, (end - start).magnitude + 0.4f));
            body.AddComponent<BoxCollider>();
            // posts at each end make the beam easier to read from a distance
            var postMat = WoodMat(z);
            foreach (float s in new[] { -0.5f, 0.5f })
                foreach (float side in new[] { -1f, 1f })
                    ProceduralMeshes.Spawn("Post", ProceduralMeshes.RoundedBox(Vector3.one, 0.3f), postMat, root,
                        new Vector3(side * (width * 0.5f - 0.1f), 0.35f, s * ((end - start).magnitude + 0.1f)), new Vector3(0.16f, 0.6f, 0.16f));
            Advance(end, 0.2f);
        }

        void Mover()
        {
            float d = Difficulty(lastSpot.y);
            float r = Rand(1.4f, 1.8f) - d * 0.25f;
            var a = Next(Rand(1.1f, 1.6f), Rand(0.2f, 0.8f), r, out var dir);
            float travel = Rand(6f, 10f);
            bool vertical = Chance(0.3f);
            Vector3 b = vertical ? a + Vector3.up * Rand(5f, 8f) + Flat(dir) * 1.5f : a + Flat(dir) * travel + Vector3.up * Rand(0f, 2f);
            var z = ZoneAt(a.y);
            var root = new GameObject("Lift").transform;
            root.SetParent(pathRoot, false);
            root.position = a;
            var rb = root.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            var mp = root.gameObject.AddComponent<MovingPlatform>();
            mp.pointA = a;
            mp.pointB = b;
            mp.period = Rand(3.4f, 5f) - d;
            mp.phase = Rand(0, 1f);
            BuildDisc(root, r, BareStoneMat(z), z, false);
            for (int i = 0; i < 3; i++)
                ProceduralMeshes.Spawn("Plank", ProceduralMeshes.RoundedBox(Vector3.one, 0.05f), WoodMat(z), root, new Vector3(0, 0.02f, (i - 1) * r * 0.55f), new Vector3(r * 1.7f, 0.1f, r * 0.48f));
            var lantern = GlowMat(new Color(1f, 0.66f, 0.35f) * 1.6f);
            for (int i = 0; i < 3; i++)
            {
                float ang = i * Mathf.PI * 2f / 3f;
                ProceduralMeshes.Spawn("Lantern", ProceduralMeshes.Sphere(16, 10), lantern, root, new Vector3(Mathf.Cos(ang) * r * 0.85f, -0.55f, Mathf.Sin(ang) * r * 0.85f), Vector3.one * 0.18f, null, false);
            }
            Vector3 landFrom = b;
            float lr = Rand(1.6f, 2.2f);
            var next = NextFrom(landFrom, r, Rand(1.3f, 1.8f), Rand(0.3f, 0.8f), out _, lr);
            Island(next, lr, ZoneAt(next.y), true);
            Advance(next, lr);
        }

        Vector3 NextFrom(Vector3 from, float fromR, float gap, float dy, out Vector3 dir, float footR = 1.8f)
        {
            var savedSpot = lastSpot;
            var savedRadius = lastSpotRadius;
            lastSpot = from;
            lastSpotRadius = fromR;
            var p = Next(gap, dy, footR, out dir);
            lastSpot = savedSpot;
            lastSpotRadius = savedRadius;
            return p;
        }

        void Crumbles()
        {
            float d = Difficulty(lastSpot.y);
            int n = rng.Next(2, 4);
            for (int i = 0; i < n; i++)
            {
                float r = Rand(1.0f, 1.3f);
                var c = Next(Rand(1.4f, 2.3f + d), Rand(0.3f, 1.0f), r, out _);
                CrumbleIsland(c, r, ZoneAt(c.y));
                Advance(c, r);
            }
            float lr = Rand(1.6f, 2.2f);
            var land = Next(Rand(1.4f, 2.2f), Rand(0.3f, 0.9f), lr, out _);
            Island(land, lr, ZoneAt(land.y), true);
            Advance(land, lr);
        }

        void Bounce()
        {
            float d = Difficulty(lastSpot.y);
            float r = 1.25f;
            var c = Next(Rand(1.3f, 2.0f), Rand(-0.6f, 0.4f), r, out _);
            float launch = Rand(9f, 12.5f);
            var z = ZoneAt(c.y);
            var root = new GameObject("BouncePad").transform;
            root.SetParent(pathRoot, false);
            root.position = c;
            Island(c, r + 0.35f, z, false, small: true, parent: root, collider: false);
            var vis = new GameObject("Mushroom").transform;
            vis.SetParent(root, false);
            vis.localPosition = new Vector3(0, -0.1f, 0);
            var shroom = Prop(Env.mushrooms.Length > 0 ? Env.mushrooms[0] : null, vis, Vector3.zero, 1f, V(0, 360));
            if (shroom != null)
            {
                var bounds = new Bounds(shroom.transform.position, Vector3.zero);
                foreach (var rend in shroom.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(rend.bounds);
                float fit = (r * 2.3f) / Mathf.Max(0.01f, Mathf.Max(bounds.size.x, bounds.size.z));
                shroom.transform.localScale *= fit;
            }
            // the glow marks the pad from far below
            var glowLight = new GameObject("Glow").AddComponent<Light>();
            glowLight.transform.SetParent(root, false);
            glowLight.transform.localPosition = Vector3.up * 0.8f;
            glowLight.type = LightType.Point;
            glowLight.color = new Color(1f, 0.45f, 0.75f);
            glowLight.range = 6f;
            glowLight.intensity = 1.6f;
            glowLight.shadows = LightShadows.None;
            var col = new GameObject("Col");
            col.transform.SetParent(root, false);
            col.transform.localPosition = new Vector3(0, -0.6f, 0);
            col.transform.localScale = new Vector3(r + 0.35f, 1.0f, r + 0.35f);
            var mc = col.AddComponent<MeshCollider>();
            mc.sharedMesh = colCylinder;
            mc.convex = true;
            var pad = root.gameObject.AddComponent<BouncePad>();
            pad.launchHeight = launch;
            pad.visual = vis;
            // landing island up near the top of the launch
            float lr = Rand(1.8f, 2.4f);
            var land = NextFrom(c, r, Rand(0.8f, 1.8f), launch - Rand(1.5f, 3f), out _, lr);
            Island(land, lr, ZoneAt(land.y), true);
            Advance(land, lr);
        }

        void Spinner()
        {
            float d = Difficulty(lastSpot.y);
            float r = Rand(2.6f, 3.3f);
            var c = Next(Rand(1.4f, 2.2f), Rand(0.4f, 1.1f), r, out _);
            var z = ZoneAt(c.y);
            var root = new GameObject("Spinner").transform;
            root.SetParent(pathRoot, false);
            root.position = c;
            var rb = root.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            var sp = root.gameObject.AddComponent<SpinningPlatform>();
            sp.degreesPerSecond = (Chance(0.5f) ? 1 : -1) * Rand(28f, 45f + 40f * d);
            BuildDisc(root, r, StoneMat(z), z, true);
            Advance(c, r * 0.6f);
        }

        void SweeperPad()
        {
            float d = Difficulty(lastSpot.y);
            float r = Rand(3.0f, 3.6f);
            var c = Next(Rand(1.4f, 2.0f), Rand(0.4f, 1.0f), r, out _);
            var z = ZoneAt(c.y);
            var island = Island(c, r, z, false);
            var post = ProceduralMeshes.Spawn("Hub", ProceduralMeshes.Puck(0.3f, 1f), BareStoneMat(z), island.transform, Vector3.zero, new Vector3(0.45f, 1.1f, 0.45f));
            post.AddComponent<CapsuleCollider>().radius = 1f;
            var arm = new GameObject("Sweeper").transform;
            arm.SetParent(pathRoot, false);
            arm.position = c + Vector3.up * 0.62f;
            var rb = arm.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            var sw = arm.gameObject.AddComponent<Sweeper>();
            sw.degreesPerSecond = (Chance(0.5f) ? 1 : -1) * Rand(70f, 95f + 50f * d);
            var hazard = WoodMat(z);
            var bar = ProceduralMeshes.Spawn("Bar", ProceduralMeshes.RoundedBox(Vector3.one, 0.2f), hazard, arm, Vector3.zero, new Vector3(r * 2f - 0.2f, 0.42f, 0.42f));
            bar.AddComponent<BoxCollider>();
            if (Chance(0.4f + d * 0.4f))
            {
                var bar2 = ProceduralMeshes.Spawn("Bar2", ProceduralMeshes.RoundedBox(Vector3.one, 0.2f), hazard, arm, Vector3.zero, new Vector3(0.42f, 0.42f, r * 2f - 0.2f));
                bar2.AddComponent<BoxCollider>();
            }
            Advance(c, r);
        }

        void PlaceCheckpoint()
        {
            float r = 3.6f;
            var c = Next(Rand(1.4f, 2.2f), Rand(0.5f, 1.0f), r, out var dir);
            var z = ZoneAt(c.y);
            var island = Island(c, r, z, false);
            var root = island.transform;
            var cp = root.gameObject.AddComponent<Checkpoint>();
            cp.index = checkpoints.Count + 1;
            checkpoints.Add(cp);
            var trig = root.gameObject.AddComponent<BoxCollider>();
            trig.isTrigger = true;
            trig.center = new Vector3(0, 2f, 0);
            trig.size = new Vector3(r * 2f, 4f, r * 2f);

            // banner pole
            var poleMat = WoodMat(z);
            Vector3 polePos = -Flat(dir).normalized * (r - 0.8f);
            ProceduralMeshes.Spawn("Pole", ProceduralMeshes.Puck(0.4f, 1f), poleMat, root, polePos, new Vector3(0.12f, 4.2f, 0.12f));
            var lanternMat = new Material(Env.emissive);
            lanternMat.SetColor("_EmissionColor", new Color(0.35f, 0.25f, 0.2f));
            ProceduralMeshes.Spawn("Lantern", ProceduralMeshes.Sphere(20, 12), lanternMat, root, polePos + Vector3.up * 4.35f, new Vector3(0.3f, 0.38f, 0.3f));
            ProceduralMeshes.Spawn("LanternCap", ProceduralMeshes.Cone(), poleMat, root, polePos + Vector3.up * 4.5f, new Vector3(0.24f, 0.2f, 0.24f));
            var bannerMat = new Material(Env.cloth);
            var banner = ProceduralMeshes.Spawn("Banner", ProceduralMeshes.RoundedBox(Vector3.one, 0.04f), bannerMat, root, polePos + Vector3.up * 3.3f + Vector3.Cross(Vector3.up, Flat(dir).normalized) * 0.42f,
                new Vector3(0.7f, 1.6f, 0.03f), Quaternion.LookRotation(Flat(dir).normalized) * Quaternion.Euler(0, 0, 3f));
            cp.banner = banner.GetComponent<Renderer>();
            var lightGo = new GameObject("Glow");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.localPosition = Vector3.up * 2.5f;
            lightGo.transform.position = root.TransformPoint(polePos) + Vector3.up * 4.35f;
            var glowLight = lightGo.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.color = new Color(1f, 0.7f, 0.42f);
            glowLight.range = 12f;
            glowLight.intensity = 2.2f;
            glowLight.enabled = false;
            glowLight.shadows = LightShadows.None;
            cp.lantern = lanternMat;
            cp.glow = glowLight;
            cp.burst = FxFactory.Confetti(root, Vector3.up * 0.5f);
            // ring of small standing stones
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI * 2f / 7f + 0.2f;
                var p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (r - 0.4f);
                if (Vector3.Distance(p, polePos) < 1.2f) continue;
                ScanRock(root, p + Vector3.up * 0.15f, V(0.45f, 0.7f), Quaternion.Euler(V(-10, 10), V(0, 360), 0));
            }
            Grass(root, r * 0.8f, 6);
            Advance(c, r);
        }

        #endregion

        #region Builders

        // visuals use their own generator so restyling never changes the climb itself
        float V(float a, float b) => a + (float)visualRng.NextDouble() * (b - a);
        bool VChance(float p) => visualRng.NextDouble() < p;
        static EnvArt Env => EnvArt.Instance;
        readonly Dictionary<string, Material> tinted = new Dictionary<string, Material>();

        /// <summary>A zone-tinted copy of an environment material (kept subtle so the scans stay believable).</summary>
        Material Tint(Material baseMat, Color tint, float amount, string key)
        {
            key = baseMat.name + key;
            if (tinted.TryGetValue(key, out var m)) return m;
            m = new Material(baseMat) { name = key };
            m.SetColor("_Color", Color.Lerp(Color.white, tint, amount));
            tinted[key] = m;
            return m;
        }

        Material StoneMat(Zone z) => Tint(Env.island, z.stone, 0.35f, z.name);
        Material BareStoneMat(Zone z) => Tint(Env.stoneBare, z.stone, 0.3f, z.name);
        Material WoodMat(Zone z) => Env.wood;
        Material GlowMat(Color c)
        {
            string key = "glow" + ColorUtility.ToHtmlStringRGB(c);
            if (tinted.TryGetValue(key, out var m)) return m;
            m = new Material(Env.emissive) { name = key };
            m.SetColor("_EmissionColor", c);
            tinted[key] = m;
            return m;
        }

        /// <summary>One of the photoscanned rocks, scaled so its largest side is 'size' metres.</summary>
        GameObject ScanRock(Transform parent, Vector3 localPos, float size, Quaternion rot, bool shadows = false)
        {
            int i = visualRng.Next(Env.rockMeshes.Length);
            var mesh = Env.rockMeshes[i];
            float s = size / Mathf.Max(0.0001f, Mathf.Max(mesh.bounds.size.x, Mathf.Max(mesh.bounds.size.y, mesh.bounds.size.z)));
            var go = ProceduralMeshes.Spawn("Rock", mesh, Env.rockMaterials[i], parent, Vector3.zero, Vector3.one * s, rot, shadows);
            // re-centre on the mesh's bounds so placement is predictable
            go.transform.localPosition = localPos - rot * (mesh.bounds.center * s);
            return go;
        }

        /// <summary>Instantiate a decorative prop, uniformly scaled, with its colliders removed.</summary>
        GameObject Prop(GameObject prefab, Transform parent, Vector3 localPos, float scale, float yaw)
        {
            if (prefab == null) return null;
            var go = Instantiate(prefab, parent);
            go.name = "Detail_" + prefab.name;
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0) * prefab.transform.localRotation;
            go.transform.localScale = prefab.transform.localScale * scale;
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            return go;
        }

        /// <summary>Floating rock island whose top surface sits at topCenter.</summary>
        GameObject Island(Vector3 topCenter, float r, Zone z, bool decorate, bool small = false, Transform parent = null, bool collider = true)
        {
            var root = new GameObject(small ? "Stepping Stone" : "Island");
            root.transform.SetParent(parent ? parent : pathRoot, true);
            root.transform.position = topCenter;
            root.transform.rotation = Quaternion.Euler(0, V(0, 360), 0);
            BuildIslandVisual(root.transform, r, z, small);
            if (collider)
            {
                var col = new GameObject("Col");
                col.transform.SetParent(root.transform, false);
                col.transform.localPosition = new Vector3(0, -0.85f, 0);
                col.transform.localScale = new Vector3(r, 0.85f, r);
                var mc = col.AddComponent<MeshCollider>();
                mc.sharedMesh = colCylinder;
                mc.convex = true;
            }
            if (decorate && !small) Decorate(root.transform, r, z);
            else if (small) Grass(root.transform, r * 0.6f, visualRng.Next(0, 3));
            return root;
        }

        void BuildIslandVisual(Transform root, float r, Zone z, bool small)
        {
            float thick = small ? 0.6f : 0.85f;
            ProceduralMeshes.Spawn("Body", ProceduralMeshes.RockyPuck(visualRng.Next(6)), StoneMat(z), root, new Vector3(0, -thick, 0), new Vector3(r, thick, r));
            // scanned rock hanging underneath
            ScanRock(root, new Vector3(V(-0.15f, 0.15f) * r, -thick - r * 0.35f, V(-0.15f, 0.15f) * r), r * V(1.2f, 1.6f), Quaternion.Euler(V(150, 210), V(0, 360), V(-20, 20)));
            if (!small && r > 1.5f && VChance(0.7f))
                ScanRock(root, new Vector3(r * 0.45f, -thick - 0.2f, -r * 0.3f), r * V(0.5f, 0.8f), Quaternion.Euler(V(0, 360), V(0, 360), V(0, 360)));
        }

        void BuildDisc(Transform root, float r, Material body, Zone z, bool spokes)
        {
            ProceduralMeshes.Spawn("Body", ProceduralMeshes.RockyPuck(visualRng.Next(6)), body, root, new Vector3(0, -0.6f, 0), new Vector3(r, 0.6f, r));
            if (spokes)
            {
                for (int i = 0; i < 4; i++)
                    ProceduralMeshes.Spawn("Spoke", ProceduralMeshes.RoundedBox(Vector3.one, 0.04f), WoodMat(z), root, new Vector3(0, 0.03f, 0), new Vector3(0.28f, 0.08f, r * 1.75f), Quaternion.Euler(0, i * 45f, 0));
                ProceduralMeshes.Spawn("Hub", ProceduralMeshes.Puck(0.4f, 1f), BareStoneMat(z), root, Vector3.zero, new Vector3(0.55f, 0.18f, 0.55f));
            }
            var col = new GameObject("Col");
            col.transform.SetParent(root, false);
            col.transform.localPosition = new Vector3(0, -0.6f, 0);
            col.transform.localScale = new Vector3(r, 0.64f, r);
            var mc = col.AddComponent<MeshCollider>();
            mc.sharedMesh = colCylinder;
            mc.convex = true;
        }

        void CrumbleIsland(Vector3 c, float r, Zone z)
        {
            var root = new GameObject("Crumbling Stone").transform;
            root.SetParent(pathRoot, false);
            root.position = c;
            var vis = new GameObject("Visual").transform;
            vis.SetParent(root, false);
            ProceduralMeshes.Spawn("Body", ProceduralMeshes.RockyPuck(visualRng.Next(6)), Env.crumble, vis, new Vector3(0, -0.6f, 0), new Vector3(r, 0.6f, r));
            ScanRock(vis, new Vector3(0, -0.75f, 0), r * 1.1f, Quaternion.Euler(V(160, 200), V(0, 360), 0));
            // loose pebbles on top hint that it won't hold
            if (Env.pebbles.Length > 0) Prop(EnvArt.Pick(Env.pebbles, visualRng), vis, new Vector3(V(-0.3f, 0.3f), 0, V(-0.3f, 0.3f)) * r, 0.5f, V(0, 360));
            var col = new GameObject("Col");
            col.transform.SetParent(root, false);
            col.transform.localPosition = new Vector3(0, -0.62f, 0);
            col.transform.localScale = new Vector3(r, 0.64f, r);
            var mc = col.AddComponent<MeshCollider>();
            mc.sharedMesh = colCylinder;
            mc.convex = true;
            var cp = root.gameObject.AddComponent<CrumblePlatform>();
            cp.visual = vis;
            cp.solid = mc;
        }

        /// <summary>Clumps of alpha-cut grass and wildflower cards.</summary>
        void Grass(Transform root, float radius, int clumps)
        {
            if (Env.grassCards == null || Env.grassCards.Length == 0) return;
            var mesh = ProceduralMeshes.GrassCross();
            for (int i = 0; i < clumps; i++)
            {
                float a = V(0, Mathf.PI * 2);
                float d = Mathf.Sqrt(V(0.1f, 1f)) * radius;
                var p = new Vector3(Mathf.Cos(a) * d, -0.02f, Mathf.Sin(a) * d);
                var mat = Env.grassCards[visualRng.Next(Env.grassCards.Length)];
                float h = mat.name.Contains("flower") ? V(0.35f, 0.55f) : V(0.4f, 0.75f);
                ProceduralMeshes.Spawn("Grass", mesh, mat, root, p, new Vector3(h * 1.3f, h, h * 1.3f), Quaternion.Euler(0, V(0, 360), 0), false);
            }
        }

        void Decorate(Transform root, float r, Zone z)
        {
            Grass(root, r * 0.85f, visualRng.Next(3, 7));
            int n = visualRng.Next(0, 3);
            for (int i = 0; i < n; i++)
            {
                float a = V(0, Mathf.PI * 2);
                float d = V(0.55f, 0.8f) * r;
                var p = new Vector3(Mathf.Cos(a) * d, -0.02f, Mathf.Sin(a) * d);
                int kind = visualRng.Next(0, 5);
                if (kind == 0) Prop(EnvArt.Pick(Env.bushes, visualRng), root, p, V(0.35f, 0.55f), V(0, 360));
                else if (kind == 1) Prop(EnvArt.Pick(Env.mushrooms, visualRng), root, p, V(0.6f, 1.1f), V(0, 360));
                else if (kind == 2) Prop(EnvArt.Pick(Env.pebbles, visualRng), root, p, V(0.5f, 0.8f), V(0, 360));
                else if (kind == 3 && r > 1.8f) Prop(EnvArt.Pick(Env.saplings, visualRng), root, p, V(0.4f, 0.6f), V(0, 360));
                else ScanRock(root, p + Vector3.up * 0.1f, V(0.35f, 0.6f), Quaternion.Euler(V(0, 30), V(0, 360), 0));
            }
            if (VChance(0.12f)) Prop(EnvArt.Pick(Env.crystals, visualRng), root, new Vector3(V(-0.4f, 0.4f) * r, -0.05f, V(-0.4f, 0.4f) * r), V(0.4f, 0.7f), V(0, 360));
        }

        #endregion

        #region Base and walls

        void BuildBase()
        {
            if (!buildPitFloor)
            {
                ProceduralMeshes.Spawn("StartRing", ProceduralMeshes.Ring(1.6f, 2.0f), Mats.Fx("startring", Mats.Hex("#c39bff") * new Color(1, 1, 1, 0.7f), null, true), transform,
                    startPoint + Vector3.up * 0.05f, Vector3.one, null, false);
                return;
            }
            var deco = new System.Random(seed + 1);
            float DR(float a, float b) => a + (float)deco.NextDouble() * (b - a);
            var pit = new GameObject("Pit").transform;
            pit.SetParent(transform, false);
            var z = Zones[0];
            var floorY = center.y;

            // grassy floor of the pit
            var grass = Mats.Toon("pitgrass", Mats.Hex("#86c66e"), BoboArt.Instance.grass, true, 0.22f, 0f, new Color(0.6f, 0.62f, 0.8f));
            var floor = ProceduralMeshes.Spawn("Floor", ProceduralMeshes.Puck(0.05f, 1f), grass, pit, new Vector3(center.x, floorY - 3f, center.z), new Vector3(R + 1.5f, 3f, R + 1.5f));
            var fc = floor.AddComponent<MeshCollider>();
            fc.sharedMesh = colCylinder;
            fc.convex = true;
            // darker worn path ring
            var dirt = Mats.Toon("pitdirt", Mats.Hex("#c7b08a"), BoboArt.Instance.stone, true, 0.4f, 0f);
            ProceduralMeshes.Spawn("Path", ProceduralMeshes.Puck(0.4f, 1f), dirt, pit, new Vector3(center.x, floorY - 0.06f, center.z), new Vector3(10.5f, 0.08f, 10.5f), null, false);
            ProceduralMeshes.Spawn("PathInner", ProceduralMeshes.Puck(0.4f, 1f), grass, pit, new Vector3(center.x, floorY - 0.04f, center.z), new Vector3(8.6f, 0.08f, 8.6f), null, false);

            // landing dais
            var stoneM = StoneMat(z);
            var dais = ProceduralMeshes.Spawn("Dais", ProceduralMeshes.Puck(0.25f, 0.92f), stoneM, pit, startPoint - Vector3.up * 0.55f, new Vector3(2.6f, 0.55f, 2.6f));
            var dc = new GameObject("Col");
            dc.transform.SetParent(dais.transform, false);
            dc.transform.localScale = Vector3.one;
            var dmc = dc.AddComponent<MeshCollider>();
            dmc.sharedMesh = colCylinder;
            dmc.convex = true;
            var ring = ProceduralMeshes.Spawn("StartRing", ProceduralMeshes.Ring(1.55f, 1.95f), Mats.Fx("startring", Mats.Hex("#c39bff") * new Color(1, 1, 1, 0.75f), null, true), pit,
                startPoint + Vector3.up * 0.03f, Vector3.one, null, false);

            // ring of standing stones around the middle
            var henge = Mats.Toon("henge", Mats.Hex("#b4adc9"), BoboArt.Instance.stone, true, 0.3f);
            var moss = Mats.Toon("moss", Mats.Hex("#7fbf63"), BoboArt.Instance.grass, true, 0.4f);
            int stones = 10;
            for (int i = 0; i < stones; i++)
            {
                float a = i * Mathf.PI * 2f / stones;
                var p = new Vector3(center.x + Mathf.Cos(a) * 9.5f, floorY, center.z + Mathf.Sin(a) * 9.5f);
                if (i == 7) // one has fallen over
                {
                    var fallen = ProceduralMeshes.Spawn("Fallen", ProceduralMeshes.RoundedBox(Vector3.one, 0.25f), henge, pit, p + Vector3.up * 0.5f, new Vector3(3.2f, 1f, 1.4f), Quaternion.Euler(0, -a * Mathf.Rad2Deg + 20, 4));
                    fallen.AddComponent<BoxCollider>();
                    continue;
                }
                float h = DR(2.6f, 3.6f);
                var st = ProceduralMeshes.Spawn("Standing", ProceduralMeshes.RoundedBox(Vector3.one, 0.25f), henge, pit, p + Vector3.up * h * 0.5f, new Vector3(1.4f, h, 0.9f),
                    Quaternion.Euler(DR(-4, 4), -a * Mathf.Rad2Deg + 90f, DR(-4, 4)));
                st.AddComponent<BoxCollider>();
                ProceduralMeshes.Spawn("Moss", ProceduralMeshes.RoundedBox(Vector3.one, 0.2f), moss, st.transform, new Vector3(0, 0.48f, 0), new Vector3(1.04f, 0.08f, 1.06f), null, false);
                if (i % 3 == 0)
                {
                    float a2 = (i + 1) * Mathf.PI * 2f / stones;
                    var p2 = new Vector3(center.x + Mathf.Cos(a2) * 9.5f, floorY, center.z + Mathf.Sin(a2) * 9.5f);
                    var mid = (p + p2) * 0.5f + Vector3.up * (h + 0.35f);
                    var lintel = ProceduralMeshes.Spawn("Lintel", ProceduralMeshes.RoundedBox(Vector3.one, 0.2f), henge, pit, mid, new Vector3((p2 - p).magnitude + 1.4f, 0.7f, 1.0f),
                        Quaternion.LookRotation(Vector3.Cross(Vector3.up, (p2 - p).normalized)));
                    lintel.AddComponent<BoxCollider>();
                }
            }

            // broken pillars and boulders around the edge
            for (int i = 0; i < 9; i++)
            {
                float a = DR(0, Mathf.PI * 2f);
                float d = DR(R - 10f, R - 3f);
                var p = new Vector3(center.x + Mathf.Cos(a) * d, floorY, center.z + Mathf.Sin(a) * d);
                if (Vector3.Distance(Flat(p), Flat(startPoint)) < 6f) continue;
                if (deco.NextDouble() < 0.5)
                {
                    int segs = deco.Next(2, 5);
                    for (int s = 0; s < segs; s++)
                    {
                        var seg = ProceduralMeshes.Spawn("Pillar", ProceduralMeshes.Puck(0.15f, 0.95f), henge, pit, p + Vector3.up * s * 1.1f + new Vector3(DR(-0.08f, 0.08f), 0, DR(-0.08f, 0.08f)),
                            new Vector3(0.75f, 1.05f, 0.75f), Quaternion.Euler(DR(-3, 3), DR(0, 360), DR(-3, 3)));
                        if (s == 0) seg.AddComponent<CapsuleCollider>().radius = 1f;
                    }
                }
                else
                {
                    float s = DR(0.8f, 2f);
                    var b = ProceduralMeshes.Spawn("Boulder", ProceduralMeshes.Sphere(24, 14), henge, pit, p + Vector3.up * s * 0.3f, new Vector3(s * 1.3f, s * 0.8f, s), Quaternion.Euler(0, DR(0, 360), 0));
                    b.AddComponent<SphereCollider>();
                }
            }

            // flowers, tufts and mushrooms, no colliders
            var stalk = Mats.Toon("stalk", Mats.Hex("#5e9c55"));
            var petals = new[]
            {
                Mats.Toon("petalA", Mats.Hex("#ff9eb5")),
                Mats.Toon("petalB", Mats.Hex("#ffe27a")),
                Mats.Toon("petalC", Mats.Hex("#b6a4ff")),
                Mats.Toon("petalD", Color.white),
            };
            var capM = Mats.Toon("mushcap", Mats.Hex("#ff7f6e"));
            var stemM = Mats.Toon("stem", Mats.Hex("#f5ead8"));
            for (int i = 0; i < 260; i++)
            {
                float a = DR(0, Mathf.PI * 2f);
                float d = Mathf.Sqrt(DR(0.02f, 1f)) * (R - 1.5f);
                var p = new Vector3(center.x + Mathf.Cos(a) * d, floorY, center.z + Mathf.Sin(a) * d);
                if (Vector3.Distance(Flat(p), Flat(startPoint)) < 3f) continue;
                int kind = deco.Next(0, 10);
                if (kind < 5)
                {
                    float h = DR(0.25f, 0.5f);
                    ProceduralMeshes.Spawn("Stalk", ProceduralMeshes.Puck(0.4f, 1f), stalk, pit, p, new Vector3(0.04f, h, 0.04f), null, false);
                    ProceduralMeshes.Spawn("Bloom", ProceduralMeshes.Sphere(16, 10), petals[deco.Next(petals.Length)], pit, p + Vector3.up * h, new Vector3(0.16f, 0.1f, 0.16f), null, false);
                }
                else if (kind < 9)
                {
                    for (int k = 0; k < 3; k++)
                        ProceduralMeshes.Spawn("Blade", ProceduralMeshes.Cone(), grass, pit, p + new Vector3(DR(-0.15f, 0.15f), 0, DR(-0.15f, 0.15f)), new Vector3(0.07f, DR(0.3f, 0.55f), 0.07f),
                            Quaternion.Euler(DR(-20, 20), 0, DR(-20, 20)), false);
                }
                else
                {
                    float s = DR(0.8f, 1.8f);
                    ProceduralMeshes.Spawn("Stem", ProceduralMeshes.Puck(0.4f, 0.9f), stemM, pit, p, new Vector3(0.1f, 0.28f, 0.1f) * s);
                    ProceduralMeshes.Spawn("Cap", ProceduralMeshes.Dome(0f), capM, pit, p + Vector3.up * 0.25f * s, new Vector3(0.25f, 0.18f, 0.25f) * s);
                }
            }

            // glowing crystal clusters near the walls
            var crystal = Mats.Toon("pitcrystal", Mats.Hex("#b99cff"), null, false, 0.25f, 2f, null, 0.6f, 0.9f);
            crystal.SetColor("_EmissionColor", Mats.Hex("#7a5cff") * 0.5f);
            for (int i = 0; i < 7; i++)
            {
                float a = DR(0, Mathf.PI * 2f);
                float d = R - DR(1.5f, 4f);
                var p = new Vector3(center.x + Mathf.Cos(a) * d, floorY, center.z + Mathf.Sin(a) * d);
                for (int k = 0; k < 4; k++)
                    ProceduralMeshes.Spawn("Crystal", ProceduralMeshes.Cone(), crystal, pit, p + new Vector3(DR(-0.6f, 0.6f), 0, DR(-0.6f, 0.6f)),
                        new Vector3(0.3f, DR(0.8f, 1.8f), 0.3f), Quaternion.Euler(DR(-25, 25), 0, DR(-25, 25)));
                var crystalLight = new GameObject("CrystalLight").AddComponent<Light>();
                crystalLight.transform.SetParent(pit, false);
                crystalLight.transform.position = p + Vector3.up * 1.2f;
                crystalLight.type = LightType.Point;
                crystalLight.color = Mats.Hex("#a585ff");
                crystalLight.range = 7f;
                crystalLight.intensity = 1.6f;
                crystalLight.shadows = LightShadows.None;
            }

            // light shaft down the open tower onto the dais
            var shaftMat = Mats.Fx("pitshaft", new Color(1f, 0.93f, 0.8f, 0.07f), BoboArt.Instance.softDot, true);
            ProceduralMeshes.Spawn("LightShaft", ProceduralMeshes.Lathe("shaftcone", new[] { new Vector2(1.2f, 0), new Vector2(4f, 1) }, 32), shaftMat, pit,
                startPoint, new Vector3(1.4f, 70f, 1.4f), null, false);
        }

        void BuildWallSection()
        {
            float y0 = wallTop;
            var z = ZoneAt(y0 + wallSection * 0.5f);
            var wallMat = Tint(Env.wall, z.wall, 0.3f, z.name);
            var wallFace = Tint(Env.wallCheap ? Env.wallCheap : Env.wall, z.wall, 0.3f, z.name);
            var go = ProceduralMeshes.Spawn("Wall", ProceduralMeshes.WallRing(R, R + wallThickness, wallSection, 96, 0.22f), wallFace, wallRoot,
                new Vector3(center.x, y0, center.z), Vector3.one, null, false);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;

            // ledge every section; the material's triplanar top layer adds the moss
            var ledgeProfile = new[]
            {
                new Vector2(1.0f, 0.6f),
                new Vector2(0.985f, 0.3f),
                new Vector2(0.982f, 0f),
                new Vector2(0.985f, -0.3f),
                new Vector2(1.0f, -0.6f),
            };
            ProceduralMeshes.Spawn("Ledge", ProceduralMeshes.Lathe("bandring", ledgeProfile, 128),
                wallMat, wallRoot, new Vector3(center.x, y0, center.z), new Vector3(R, 1f, R), null, false);

            // rocks wedged into the wall, shrubs along the ledge
            for (int i = 0; i < 6; i++)
            {
                float a = V(0, Mathf.PI * 2f);
                float y = y0 + V(2f, wallSection - 2f);
                var dirOut = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                ScanRock(wallRoot, new Vector3(center.x, y, center.z) + dirOut * (R + 0.2f), V(1.5f, 3.5f), Quaternion.Euler(V(0, 360), V(0, 360), V(0, 360)), false);
            }
            for (int i = 0; i < 5; i++)
            {
                float a = V(0, Mathf.PI * 2f);
                var dirOut = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                Prop(EnvArt.Pick(Env.bushes, visualRng), wallRoot, new Vector3(center.x, y0 + 0.6f, center.z) + dirOut * (R - 0.55f), V(0.25f, 0.4f), V(0, 360));
            }

            // glowing windows
            int count = visualRng.Next(2, 5);
            var win = GlowMat(Color.Lerp(z.accent, new Color(0.6f, 0.55f, 1f), 0.6f) * 1.8f);
            for (int i = 0; i < count; i++)
            {
                float a = V(0, Mathf.PI * 2f);
                float y = y0 + V(6f, wallSection - 6f);
                var dirOut = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var p = new Vector3(center.x, y, center.z) + dirOut * (R + 0.15f);
                ProceduralMeshes.Spawn("Window", ProceduralMeshes.RoundedBox(Vector3.one, 0.3f), win, wallRoot, p, new Vector3(0.7f, 3.2f, 0.4f), Quaternion.LookRotation(dirOut), false);
            }
            wallTop += wallSection;
            LayNew();
        }

        #endregion
    }
}
