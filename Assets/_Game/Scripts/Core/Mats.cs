using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BobosJourney
{
    /// <summary>Runtime material factory. Materials are cached by key so identical props batch together.</summary>
    public static class Mats
    {
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        static BoboArt Art => BoboArt.Instance;

        public static Material Toon(string key, Color color, Texture tex = null, bool triplanar = false, float triScale = 0.25f,
            float outline = 2.2f, Color? shadow = null, float rim = 0.3f, float spec = 0.45f)
        {
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Art.toon) { name = "Toon_" + key };
            m.SetColor("_Color", color);
            if (tex != null) m.SetTexture("_MainTex", tex);
            if (triplanar)
            {
                m.EnableKeyword("_TRIPLANAR");
                m.SetFloat("_Triplanar", 1);
                m.SetFloat("_TriScale", triScale);
            }
            m.SetFloat("_OutlineWidth", outline);
            m.SetColor("_ShadowColor", shadow ?? new Color(0.62f, 0.55f, 0.82f));
            m.SetColor("_RimColor", new Color(1f, 0.95f, 1f, rim));
            m.SetColor("_SpecColor2", new Color(1f, 1f, 1f, spec));
            m.enableInstancing = true;
            cache[key] = m;
            return m;
        }

        /// <summary>Fresh (uncached) toon material, for things whose colour animates individually.</summary>
        public static Material ToonInstance(Color color, Texture tex = null, float outline = 2.2f)
        {
            var m = new Material(Art.toon) { name = "ToonInst" };
            m.SetColor("_Color", color);
            if (tex != null) m.SetTexture("_MainTex", tex);
            m.SetFloat("_OutlineWidth", outline);
            return m;
        }

        public static Material Fx(string key, Color color, Texture tex, bool additive, bool cached = true, float zBias = 0f)
        {
            if (cached && cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Art.fx) { name = "FX_" + key };
            m.SetColor("_Color", color);
            if (tex != null) m.SetTexture("_MainTex", tex);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZBias", zBias);
            if (cached) cache[key] = m;
            return m;
        }

        public static Material Sky(Color top, Color horizon, Color bottom, Color sun, Vector3 sunDir, float clouds, float stars, Color? cloudColor = null)
        {
            var m = new Material(Art.sky) { name = "Sky" };
            m.SetColor("_TopColor", top);
            m.SetColor("_HorizonColor", horizon);
            m.SetColor("_BottomColor", bottom);
            m.SetColor("_SunColor", sun);
            m.SetVector("_SunDir", sunDir.normalized);
            m.SetFloat("_CloudAmount", clouds);
            m.SetFloat("_Stars", stars);
            m.SetColor("_CloudColor", cloudColor ?? new Color(1, 1, 1, 0.65f));
            return m;
        }

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out var c);
            return c;
        }
    }
}
