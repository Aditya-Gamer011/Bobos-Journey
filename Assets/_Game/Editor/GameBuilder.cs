using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.PostProcessing;

namespace BobosJourney.EditorTools
{
    /// <summary>Builds the Chapter1 scene and registers it in Build Settings. Menu: Bobo > Build Scenes.</summary>
    public static class GameBuilder
    {
        const string SceneDir = "Assets/_Game/Scenes";
        const string GamePath = SceneDir + "/Chapter1.unity";
        const string IntroPath = SceneDir + "/Intro.unity";
        const string StoryFontPath = "Assets/_Game/Art/Fonts/PatrickHand-Regular.ttf";
        const string SourceScene = "Assets/Dreamscapes/Scenes/Demo02.unity";

        [MenuItem("Bobo/Build Everything")]
        public static void BuildAll()
        {
            if (AssetDatabase.LoadAssetAtPath<BoboArt>(ArtBaker.ArtAssetPath) == null) ArtBaker.BakeAll();
            BuildChapter1();
            BuildIntro();
            SetBuildScenes();
            EditorSceneManager.OpenScene(IntroPath);
            Debug.Log("[Bobo] Scene built. Press Play in Chapter1.");
        }

        [MenuItem("Bobo/Build Scenes Only")]
        public static void BuildScenes()
        {
            BuildChapter1();
            BuildIntro();
            SetBuildScenes();
        }

        [MenuItem("Bobo/Build Intro Scene")]
        public static void BuildIntro()
        {
            Directory.CreateDirectory(SceneDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.cullingMask = 0; // the story is all UI
            var story = new GameObject("Story").AddComponent<StoryIntro>();
            story.font = AssetDatabase.LoadAssetAtPath<Font>(StoryFontPath);
            story.nextScene = Path.GetFileNameWithoutExtension(GamePath);
            EditorSceneManager.SaveScene(scene, IntroPath);
        }

        static void BuildChapter1()
        {
            Directory.CreateDirectory(SceneDir);
            // Chapter 1 is built inside a copy of the Dreamscapes Demo02 valley, which becomes the pit below the tower
            if (File.Exists(GamePath)) AssetDatabase.DeleteAsset(GamePath);
            AssetDatabase.CopyAsset(SourceScene, GamePath);
            var scene = EditorSceneManager.OpenScene(GamePath, OpenSceneMode.Single);

            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "Tower_HollowCylinder") root.SetActive(false); // replaced by the endless generated tower

            // the valley keeps the demo's coloured lights; the tower layers get their own dim moonlight instead,
            // since the demo lights were made for a small glade and blow out the climb
            EnsureTowerLayer();
            int towerBit = (1 << TowerGenerator.TowerLayer) | (1 << TowerGenerator.DetailLayer);
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                light.cullingMask &= ~towerBit;
                // the demo's many point lights go per-vertex for cost; only the moonlight casts shadows
                if (light.type == LightType.Point) light.renderMode = LightRenderMode.ForceVertex;
                if (light.type == LightType.Directional) light.shadows = LightShadows.None;
            }
            var moonGo = new GameObject("Tower Moonlight");
            var moon = moonGo.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.74f, 0.68f, 0.98f);
            moon.intensity = 0.95f;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.8f;
            moon.cullingMask = towerBit;
            moonGo.transform.rotation = Quaternion.Euler(58f, -30f, 0f);

            // thin the demo's fog a touch so the next few jumps stay readable
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0105f;

            var cam = Camera.main;
            cam.farClipPlane = 700f;
            cam.nearClipPlane = 0.15f;
            cam.fieldOfView = 62f;
            var tpc = cam.GetComponent<ThirdPersonCamera>();
            if (tpc == null) tpc = cam.gameObject.AddComponent<ThirdPersonCamera>();
            SetupPostFx(cam);

            var terrain = Object.FindFirstObjectByType<Terrain>();
            Vector3 center = new Vector3(80, 0, 80);
            Vector3 start = new Vector3(80, 0, 57);
            if (terrain) start.y = terrain.SampleHeight(start) + terrain.transform.position.y + 0.05f;
            var game = new GameObject("Game");
            var tower = game.AddComponent<TowerGenerator>();
            tower.center = center;
            tower.innerRadius = 43f;
            tower.baseY = -2f;
            tower.buildPitFloor = false;
            tower.startPoint = start;
            var gm = game.AddComponent<GameManager>();
            gm.tower = tower;
            gm.cam = tpc;
            cam.transform.position = start + new Vector3(0, 4, -9);
            cam.transform.LookAt(start);

            // the demo camera carries the pack's post-processing scripts, which aren't imported
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, GamePath);
        }

        const string ProfilePath = "Assets/_Game/Art/ChapterGrade.asset";

        /// <summary>Adds the post-processing layer, a fresh profile (bloom, grading, vignette, AO, grain) and a global volume.</summary>
        static void SetupPostFx(Camera cam)
        {
            cam.allowHDR = true;
            cam.allowMSAA = false; // SMAA below; MSAA + HDR post is wasteful in forward
            var layer = cam.GetComponent<PostProcessLayer>();
            if (layer == null) layer = cam.gameObject.AddComponent<PostProcessLayer>();
            layer.Init(AssetDatabase.LoadAssetAtPath<PostProcessResources>("Packages/com.unity.postprocessing/PostProcessing/PostProcessResources.asset"));
            layer.volumeLayer = ~0;
            layer.volumeTrigger = cam.transform;
            layer.antialiasingMode = PostProcessLayer.Antialiasing.FastApproximateAntialiasing;
            layer.fastApproximateAntialiasing.fastMode = true;

            if (AssetDatabase.LoadAssetAtPath<PostProcessProfile>(ProfilePath) != null) AssetDatabase.DeleteAsset(ProfilePath);
            var profile = ScriptableObject.CreateInstance<PostProcessProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);

            var bloom = profile.AddSettings<Bloom>();
            bloom.enabled.Override(true);
            bloom.intensity.Override(2.2f);
            bloom.threshold.Override(1.05f);
            bloom.softKnee.Override(0.6f);
            bloom.diffusion.Override(5.5f);
            bloom.color.Override(new Color(1f, 0.9f, 1f));
            bloom.fastMode.Override(true);

            var grade = profile.AddSettings<ColorGrading>();
            grade.enabled.Override(true);
            grade.gradingMode.Override(GradingMode.HighDefinitionRange);
            grade.tonemapper.Override(Tonemapper.Neutral);
            grade.postExposure.Override(0.15f);
            grade.temperature.Override(-6f);
            grade.tint.Override(6f);
            grade.saturation.Override(-8f);
            grade.contrast.Override(14f);
            grade.lift.Override(new Vector4(0.95f, 0.92f, 1.05f, -0.03f)); // cool, slightly crushed shadows
            grade.gain.Override(new Vector4(1.04f, 0.99f, 0.96f, 0f)); // warm highlights

            var vignette = profile.AddSettings<Vignette>();
            vignette.enabled.Override(true);
            vignette.intensity.Override(0.36f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(new Color(0.08f, 0.03f, 0.12f));

            var ao = profile.AddSettings<AmbientOcclusion>();
            ao.enabled.Override(false); // too costly on modest GPUs; the scans' baked AO maps carry the contact shading
            ao.mode.Override(AmbientOcclusionMode.ScalableAmbientObscurance);
            ao.quality.Override(AmbientOcclusionQuality.Lowest);
            ao.intensity.Override(0.6f);
            ao.radius.Override(0.6f);
            ao.color.Override(new Color(0.08f, 0.04f, 0.12f));

            var grain = profile.AddSettings<Grain>();
            grain.enabled.Override(false); // ~5 ms on modest GPUs; turn on in ChapterGrade.asset if you have headroom
            grain.intensity.Override(0.18f);
            grain.size.Override(1.1f);
            grain.colored.Override(false);

            foreach (var settings in profile.settings)
            {
                settings.name = settings.GetType().Name;
                AssetDatabase.AddObjectToAsset(settings, profile);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var volGo = new GameObject("Grade Volume");
            var vol = volGo.AddComponent<PostProcessVolume>();
            vol.isGlobal = true;
            vol.priority = 1;
            vol.sharedProfile = profile;
        }

        static void EnsureTowerLayer()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            layers.GetArrayElementAtIndex(TowerGenerator.TowerLayer).stringValue = "Tower";
            layers.GetArrayElementAtIndex(TowerGenerator.DetailLayer).stringValue = "TowerDetail";
            tagManager.ApplyModifiedProperties();
        }

        static void SetBuildScenes()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(IntroPath, true), // the story plays first, then hands over to Chapter 1
                new EditorBuildSettingsScene(GamePath, true),
            };
        }
    }
}
