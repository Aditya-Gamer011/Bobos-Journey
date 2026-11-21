using UnityEngine;

namespace BobosJourney
{
    public enum Expression
    {
        Happy = 0,
        Excited = 1,
        Determined = 2,
        Scared = 3,
        Sad = 4,
        Blink = 5,
        Hurt = 6,
        Angry = 7,
    }

    /// <summary>Generated textures and shaders, built by Editor/ArtBaker.cs. Referencing the shaders here keeps them in builds.</summary>
    [CreateAssetMenu(menuName = "Bobo/Art Library")]
    public class BoboArt : ScriptableObject
    {
        public const int ExpressionCount = 8;

        [Header("Shaders")]
        public Shader toon;
        public Shader fx;
        public Shader portal;
        public Shader sky;

        [Header("Bobo's skin, one per Expression")]
        public Texture2D[] boboFaces = new Texture2D[ExpressionCount];

        [Header("Surfaces (tileable, tinted by the material)")]
        public Texture2D stone;
        public Texture2D wood;
        public Texture2D grass;
        public Texture2D jelly;

        [Header("FX")]
        public Texture2D blob;
        public Texture2D softDot;
        public Texture2D star;

        [Header("UI")]
        public Sprite inkBox;      // white fill, so the tint sets the fill colour while the ink stays dark
        public Sprite paperTorn;
        public Sprite brush;
        public Sprite splat;
        public Sprite inkRing;
        public Sprite circle;      // clean disc, used for masks
        public Texture2D inkLine;  // tiles vertically
        public Font font;

        static BoboArt instance;

        /// <summary>Loaded from Resources/BoboArt.</summary>
        public static BoboArt Instance
        {
            get
            {
                if (instance == null) instance = Resources.Load<BoboArt>("BoboArt");
                return instance;
            }
        }
    }
}
