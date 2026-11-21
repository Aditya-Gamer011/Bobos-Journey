using UnityEditor;
using UnityEditor.SceneManagement;

namespace BobosJourney.EditorTools
{
    /// <summary>
    /// Makes Play start from the story intro, like the build does. Untick Bobo > Play From Story Intro to play the open scene.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayFromIntro
    {
        const string Menu = "Bobo/Play From Story Intro";
        const string Pref = "Bobo.PlayFromIntro";
        const string IntroPath = "Assets/_Game/Scenes/Intro.unity";

        static bool Enabled
        {
            get => EditorPrefs.GetBool(Pref, true);
            set => EditorPrefs.SetBool(Pref, value);
        }

        static PlayFromIntro()
        {
            EditorApplication.delayCall += Apply;
            // set it again right as Play is pressed, so it never depends on the editor having ticked first
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode) Apply();
            };
        }

        static void Apply()
        {
            EditorSceneManager.playModeStartScene = Enabled ? AssetDatabase.LoadAssetAtPath<SceneAsset>(IntroPath) : null;
            UnityEditor.Menu.SetChecked(Menu, Enabled);
        }

        [MenuItem(Menu)]
        static void Toggle()
        {
            Enabled = !Enabled;
            Apply();
        }

        [MenuItem(Menu, true)]
        static bool ToggleValidate()
        {
            UnityEditor.Menu.SetChecked(Menu, Enabled);
            return true;
        }
    }
}
