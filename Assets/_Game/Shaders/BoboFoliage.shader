// Alpha-tested, two-sided grass / flower cards with a gentle wind sway (top of the card moves, root stays put).
Shader "Bobo/Foliage"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Card (RGBA)", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.4
        _Wind ("Wind Strength", Float) = 0.08
        _Translucency ("Back Light", Range(0,1)) = 0.35
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" }
        Cull Off
        CGPROGRAM
        #pragma surface surf Lambert vertex:vert alphatest:_Cutoff addshadow
        #pragma target 3.0
        sampler2D _MainTex; fixed4 _Color; float _Wind, _Translucency;
        struct Input { float2 uv_MainTex; float facing : VFACE; };
        void vert(inout appdata_full v)
        {
            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            float sway = sin(_Time.y * 1.7 + wp.x * 0.6 + wp.z * 0.45) + 0.5 * sin(_Time.y * 3.1 + wp.z * 1.3);
            v.vertex.xyz += mul((float3x3)unity_WorldToObject, float3(sway, 0, sway * 0.6)) * _Wind * v.texcoord.y;
        }
        void surf(Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb * (0.75 + 0.25 * IN.uv_MainTex.y); // darker at the root
            o.Alpha = c.a;
            o.Normal = float3(0, 0, IN.facing > 0 ? 1 : -1);
            o.Emission = c.rgb * _Translucency * 0.15;
        }
        ENDCG
    }
    FallBack "Legacy Shaders/Transparent/Cutout/VertexLit"
}
