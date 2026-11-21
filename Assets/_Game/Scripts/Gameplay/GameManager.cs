using System.Collections;
using UnityEngine;

namespace BobosJourney
{
    /// <summary>Runs the climb: spawn via portal, height tracking, checkpoints, falls and the pause menu.</summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public TowerGenerator tower;
        public ThirdPersonCamera cam;
        public float fallLimit = 30f;

        public BoboController Bobo { get; private set; }
        HUD hud;
        Vector3 respawnPoint;
        int lastZone = -1;
        float best;
        bool paused;
        int checkpointIndex;
        int falls;
        Transform ambience;

        const string BestKey = "bobo_best_height";
        const string CheckpointKey = "bobo_checkpoint";

        void Awake()
        {
            Instance = this;
            // browsers pace frames themselves (requestAnimationFrame); a fixed target there causes stutter
            if (Application.platform != RuntimePlatform.WebGLPlayer) Application.targetFrameRate = 120;
            QualitySettings.lodBias = 0.7f; // the rest of the quality budget is set by GraphicsQuality
        }

        void Start()
        {
            best = PlayerPrefs.GetFloat(BestKey, 0);
            TrimValley(); // before Bobo is spawned, so he is never mistaken for scenery outside the walls
            // keep the cartoon cast readable under the scene's saturated lights
            Shader.SetGlobalFloat("_BoboLightDesat", 0.6f);
            Shader.SetGlobalFloat("_BoboLightCap", 1.2f);
            hud = new GameObject("HUD").AddComponent<HUD>();
            hud.ShowChapterCard("CHAPTER 1", "BEGINNING OF BOBO'S JOURNEY", "\"...you must... bounce back.\""); // covers the very first frame
            hud.OnResume = () => SetPaused(false);
            hud.OnRestart = () =>
            {
                SetPaused(false);
                Respawn(false);
            };
            hud.OnQuit = Quit;

            var go = new GameObject("Bobo");
            go.AddComponent<SphereCollider>();
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 1;
            Bobo = go.AddComponent<BoboController>();
            var visual = BallCharacter.Create(Bobo.radius, go.transform, Expression.Determined);
            visual.transform.localPosition = new Vector3(0, -Bobo.radius, 0);
            Bobo.visual = visual;
            TowerGenerator.SetLayer(visual.transform, TowerGenerator.TowerLayer);
            Bobo.cameraTransform = cam.transform;
            Bobo.OnElastic = combo => hud.Toast(combo >= 3 ? "MAX ELASTIC!" : "ELASTIC x" + combo, new Color(0.62f, 0.9f, 1f), 64 + combo * 8, 0.5f);
            Bobo.OnLanded = impact => { if (impact > 24f) cam.Shake(0.6f); };
            cam.target = go.transform;

            tower.player = go.transform;

            // continue from the last flag of a previous session
            int saved = PlayerPrefs.GetInt(CheckpointKey, 0);
            respawnPoint = tower.startPoint + Vector3.up * 0.6f;
            if (saved > 0)
            {
                var cp = tower.EnsureCheckpoint(saved);
                if (cp != null)
                {
                    checkpointIndex = saved;
                    cp.Ignite(false);
                    respawnPoint = cp.RespawnPoint;
                }
            }
            BuildAmbience();
            GraphicsQuality.Apply(cam.GetComponent<Camera>(), GraphicsQuality.Current);
            hud.SetGraphicsLabel(GraphicsQuality.Label);
            hud.OnGraphics = () =>
            {
                GraphicsQuality.Cycle(cam.GetComponent<Camera>());
                hud.SetGraphicsLabel(GraphicsQuality.Label);
            };
            StartCoroutine(PortalDrop(saved > 0));
        }

        /// <summary>Stop drawing scenery hidden outside the tower walls and trim the terrain's detail distance.</summary>
        void TrimValley()
        {
            var terrain = Terrain.activeTerrain;
            if (terrain)
            {
                terrain.detailObjectDistance = Mathf.Min(terrain.detailObjectDistance, 55f);
                terrain.detailObjectDensity = Mathf.Min(terrain.detailObjectDensity, 0.65f);
                terrain.treeDistance = Mathf.Min(terrain.treeDistance, 120f);
                terrain.basemapDistance = Mathf.Min(terrain.basemapDistance, 120f);
            }
            float limit = tower.innerRadius + 4f;
            var towerCenter = new Vector2(tower.center.x, tower.center.z);
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                if (root == gameObject || root.GetComponent<Camera>() || root.GetComponent<Terrain>()) continue;
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    var boundsCenter = r.bounds.center;
                    if (Vector2.Distance(new Vector2(boundsCenter.x, boundsCenter.z), towerCenter) > limit) r.enabled = false;
                }
                foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var p = ps.transform.position;
                    if (Vector2.Distance(new Vector2(p.x, p.z), towerCenter) > limit) ps.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>Ambient particle effects that follow Bobo up the tower.</summary>
        void BuildAmbience()
        {
            var env = EnvArt.Instance;
            if (env == null || env.ambientFx == null) return;
            ambience = new GameObject("Ambience").transform;
            ambience.position = new Vector3(tower.center.x, respawnPoint.y, tower.center.z);
            foreach (var fx in env.ambientFx)
            {
                if (fx == null) continue;
                bool fog = fx.name.StartsWith("fog");
                // one bank of mist below Bobo so the drop fades away; everything else gets two rings around him
                int copies = fog ? 1 : 2;
                for (int ring = 0; ring < copies; ring++)
                {
                    float angle = ring * 2.1f + fx.name.Length;
                    float dist = fog ? 0f : 8f + ring * 11f;
                    var go = Instantiate(fx, ambience);
                    go.transform.localPosition = new Vector3(Mathf.Cos(angle) * dist, fog ? -14f : ring * 4f - 2f, Mathf.Sin(angle) * dist);
                }
            }
        }

        void LateUpdate()
        {
            if (ambience && Bobo)
            {
                var p = ambience.position;
                p.y = Mathf.Lerp(p.y, Bobo.transform.position.y, 1f - Mathf.Exp(-0.8f * Time.deltaTime));
                ambience.position = p;
            }
        }

        IEnumerator PortalDrop(bool continuing)
        {
            Bobo.InputEnabled = false;
            cam.inputEnabled = false;
            Vector3 top = respawnPoint + Vector3.up * 14f;
            Bobo.Teleport(top);
            Bobo.GetComponent<Rigidbody>().isKinematic = true;
            FaceAlongPath();

            yield return hud.HoldChapterCard(continuing ? 1.8f : 3.2f);
            StartCoroutine(hud.FadeChapterCard());

            var portalMat = new Material(BoboArt.Instance.portal);
            var portal = ProceduralMeshes.Spawn("Portal", ProceduralMeshes.FlatQuad(), portalMat, null, top + Vector3.up * 0.8f, Vector3.one * 4.2f, null, false);
            Sfx.Play(Sfx.Id.Whoosh, 0.8f);
            for (float t = 0; t < 0.6f; t += Time.deltaTime)
            {
                portalMat.SetFloat("_Open", Mathf.SmoothStep(0, 1, t / 0.6f));
                yield return null;
            }
            portalMat.SetFloat("_Open", 1);
            yield return new WaitForSeconds(0.25f);
            Bobo.GetComponent<Rigidbody>().isKinematic = false;
            Bobo.visual.SetExpression(Expression.Scared);
            float timer = 0;
            while (!Bobo.Grounded && timer < 3f)
            {
                timer += Time.deltaTime;
                yield return null;
            }
            Bobo.visual.SetExpression(Expression.Determined);
            Bobo.InputEnabled = true;
            cam.inputEnabled = true;
            if (lastZone < 0)
            {
                lastZone = tower.ZoneIndexAt(Bobo.transform.position.y);
                var zone = tower.ZoneAt(Bobo.transform.position.y);
                hud.ShowZone(lastZone, continuing ? zone.name + " (continued)" : zone.name);
            }
            for (float t = 0; t < 0.8f; t += Time.deltaTime)
            {
                portalMat.SetFloat("_Open", 1f - Mathf.SmoothStep(0, 1, t / 0.8f));
                yield return null;
            }
            Destroy(portal);
            Destroy(portalMat);
        }

        void Update()
        {
            if (Bobo == null) return;
            if (GameInput.PauseDown) SetPaused(!paused);
            // browsers drop the mouse lock on their own (Esc, alt-tab); a click takes it back
            if (!paused && Cursor.lockState != CursorLockMode.Locked && GameInput.ClickDown)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            if (paused) return;

            float height = Bobo.transform.position.y - tower.startPoint.y;
            if (height > best && Bobo.InputEnabled) best = height;
            float nextY = tower.NextCheckpointHeightAbove(Bobo.transform.position.y);
            float prevY = checkpointIndex > 0 && checkpointIndex <= tower.checkpoints.Count
                ? tower.checkpoints[checkpointIndex - 1].transform.position.y
                : tower.startPoint.y;
            float progress = Mathf.InverseLerp(prevY, nextY, Bobo.transform.position.y);
            hud.SetHeight(height, best, nextY - Bobo.transform.position.y, progress);

            int zone = tower.ZoneIndexAt(Bobo.transform.position.y);
            if (lastZone >= 0 && zone != lastZone && Bobo.Grounded)
            {
                if (zone > lastZone)
                {
                    hud.ShowZone(zone, tower.ZoneAt(Bobo.transform.position.y).name);
                    Sfx.Play(Sfx.Id.Zone, 0.7f);
                }
                lastZone = zone;
            }

            if (GameInput.ResetDown) Respawn(false);
            if (Bobo.transform.position.y < respawnPoint.y - fallLimit) Respawn(true);
        }

        public void ReachCheckpoint(Checkpoint cp)
        {
            if (cp.index <= checkpointIndex) return;
            checkpointIndex = cp.index;
            respawnPoint = cp.RespawnPoint;
            PlayerPrefs.SetInt(CheckpointKey, checkpointIndex);
            SaveBest();
            Sfx.Play(Sfx.Id.Checkpoint, 0.8f);
            hud.Toast("FLAG " + cp.index + "!", UIKit.Gold);
            Bobo.visual.SetExpression(Expression.Excited);
            CancelInvoke(nameof(BackToDetermined));
            Invoke(nameof(BackToDetermined), 1.4f);
        }

        void BackToDetermined() { if (Bobo) Bobo.visual.SetExpression(Expression.Determined); }

        void Respawn(bool fell)
        {
            falls += fell ? 1 : 0;
            SaveBest();
            Bobo.Teleport(respawnPoint);
            FaceAlongPath();
            hud.Flash(new Color(0.85f, 0.78f, 1f));
            Sfx.Play(Sfx.Id.Respawn, 0.7f);
            if (fell)
            {
                string[] lines = { "BOUNCE BACK!", "AGAIN, BOBO!", "NOT TODAY!", "UP WE GO!" };
                hud.Toast(lines[falls % lines.Length], new Color(1f, 0.6f, 0.75f), 78, 0.7f);
            }
            FxFactory.Ring(respawnPoint - Vector3.up * 0.5f, 2.2f, Mats.Hex("#c9a7ff"));
        }

        /// <summary>Point Bobo and the camera at the next platform so every (re)spawn starts facing the climb.</summary>
        void FaceAlongPath()
        {
            var dir = tower.HeadingFrom(respawnPoint);
            Bobo.Face(dir);
            cam.SnapBehind(dir);
            cam.pitch = 18f;
        }

        void SaveBest()
        {
            if (best > PlayerPrefs.GetFloat(BestKey, 0)) PlayerPrefs.SetFloat(BestKey, best);
            PlayerPrefs.Save();
        }

        void SetPaused(bool pause)
        {
            paused = pause;
            Time.timeScale = pause ? 0 : 1;
            hud.SetPaused(pause);
            Cursor.lockState = pause ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = pause;
        }

        void OnApplicationFocus(bool focus)
        {
            if (focus && !paused)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void OnDestroy()
        {
            SaveBest();
            Time.timeScale = 1;
            if (Instance == this) Instance = null;
        }

        void Quit()
        {
            SaveBest();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
