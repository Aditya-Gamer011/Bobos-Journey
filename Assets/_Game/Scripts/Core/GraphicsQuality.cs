using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

namespace BobosJourney
{
    /// <summary>Fast / Balanced / Fancy graphics presets, saved in PlayerPrefs.</summary>
    public static class GraphicsQuality
    {
        public enum Level { Fast = 0, Balanced = 1, Fancy = 2 }

        const string Key = "bobo_graphics";

        public static Level Current { get; private set; } = DefaultLevel();

        /// <summary>The saved level, otherwise Fast on WebGL and integrated GPUs and Fancy elsewhere.</summary>
        public static Level DefaultLevel()
        {
            if (PlayerPrefs.HasKey(Key)) return (Level)Mathf.Clamp(PlayerPrefs.GetInt(Key), 0, 2);
            if (Application.platform == RuntimePlatform.WebGLPlayer) return Level.Fast; // browsers start light, players can raise it
            string gpu = SystemInfo.graphicsDeviceName.ToLowerInvariant();
            bool integrated = gpu.Contains("intel") || gpu.Contains("uhd") || gpu.Contains("iris") || gpu.Contains("vega") && !gpu.Contains("rx");
            return integrated ? Level.Fast : Level.Fancy;
        }

        public static string Label => Current.ToString();

        public static void Cycle(Camera cam) => Apply(cam, (Level)(((int)Current + 1) % 3));

        public static void Apply(Camera cam, Level level)
        {
            Current = level;
            PlayerPrefs.SetInt(Key, (int)level);

            // render scale is the big lever: the world is drawn smaller and upscaled, the overlay HUD stays crisp
            float scale = level == Level.Fast ? 0.6f : level == Level.Balanced ? 0.8f : 1f;
            var scaler = cam.GetComponent<RenderScaler>();
            if (scaler == null) scaler = cam.gameObject.AddComponent<RenderScaler>();
            scaler.scale = scale;

            QualitySettings.shadowDistance = level == Level.Fancy ? 45f : 30f;
            QualitySettings.shadowResolution = level == Level.Fancy ? ShadowResolution.Medium : ShadowResolution.Low;
            QualitySettings.shadowCascades = level == Level.Fancy ? 2 : 1;
            QualitySettings.pixelLightCount = level == Level.Fast ? 1 : 2;
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional && light.name.Contains("Moon"))
                    light.shadows = level == Level.Fast ? LightShadows.None : level == Level.Balanced ? LightShadows.Hard : LightShadows.Soft;
            }

            // grass and small props only near the camera on the cheaper presets
            var cull = cam.layerCullDistances;
            if (cull == null || cull.Length != 32) cull = new float[32];
            cull[TowerGenerator.TowerLayer] = level == Level.Fast ? 85f : 115f;
            cull[TowerGenerator.DetailLayer] = level == Level.Fast ? 22f : level == Level.Balanced ? 35f : 60f;
            cam.layerCullDistances = cull;
            cam.layerCullSpherical = true;

            var terrain = Terrain.activeTerrain;
            if (terrain)
            {
                terrain.detailObjectDistance = level == Level.Fast ? 30f : 55f;
                terrain.detailObjectDensity = level == Level.Fast ? 0.4f : 0.65f;
                terrain.heightmapPixelError = level == Level.Fast ? 12f : 5f;
            }

            // big overlapping fog / light-shaft sprites are pure overdraw on integrated GPUs
            foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            {
                string psName = ps.name.ToLowerInvariant();
                if (!(psName.Contains("fog") || psName.Contains("sunshaft"))) continue;
                var psRenderer = ps.GetComponent<ParticleSystemRenderer>();
                if (psRenderer) psRenderer.enabled = level != Level.Fast;
            }

            var layer = cam.GetComponent<PostProcessLayer>();
            if (layer)
            {
                layer.antialiasingMode = level == Level.Fancy
                    ? PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing
                    : PostProcessLayer.Antialiasing.FastApproximateAntialiasing;
            }
        }
    }

    /// <summary>Renders the camera into a scaled texture and stretches it to the screen.</summary>
    [RequireComponent(typeof(Camera))]
    public class RenderScaler : MonoBehaviour
    {
        [Range(0.4f, 1f)] public float scale = 1f;

        Camera cam;
        Camera presenter;
        RenderTexture rt;
        CommandBuffer blit;

        void OnEnable()
        {
            cam = GetComponent<Camera>();
            var go = new GameObject("Upscale Presenter");
            go.transform.SetParent(transform, false);
            presenter = go.AddComponent<Camera>();
            presenter.cullingMask = 0;
            presenter.clearFlags = CameraClearFlags.Nothing;
            presenter.depth = cam.depth + 1;
            presenter.allowHDR = false;
            presenter.allowMSAA = false;
            presenter.useOcclusionCulling = false;
            blit = new CommandBuffer { name = "Upscale" };
            presenter.AddCommandBuffer(CameraEvent.AfterEverything, blit);
        }

        void OnDisable()
        {
            if (cam) cam.targetTexture = null;
            if (presenter) Destroy(presenter.gameObject);
            Release();
        }

        void Release()
        {
            if (rt)
            {
                rt.Release();
                Destroy(rt);
                rt = null;
            }
        }

        void LateUpdate()
        {
            if (scale >= 0.999f)
            {
                if (cam.targetTexture == rt && rt != null)
                {
                    cam.targetTexture = null;
                    Release();
                }
                presenter.enabled = false;
                return;
            }

            presenter.enabled = true;
            int w = Mathf.Max(64, Mathf.RoundToInt(Screen.width * scale));
            int h = Mathf.Max(64, Mathf.RoundToInt(Screen.height * scale));
            if (rt == null || rt.width != w || rt.height != h)
            {
                cam.targetTexture = null;
                Release();
                rt = new RenderTexture(w, h, 24, RenderTextureFormat.Default) { name = "ScaledScene", filterMode = FilterMode.Bilinear };
                cam.targetTexture = rt;
                blit.Clear();
                blit.Blit(rt, BuiltinRenderTextureType.CameraTarget);
            }
        }
    }
}
