// Standard lighting for photoscanned models that ship an ARM map (R = AO, G = roughness, B = metal).
Shader "Bobo/ScannedPBR"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        [Normal] _BumpMap ("Normal (GL)", 2D) = "bump" {}
        _ARM ("AO/Rough/Metal", 2D) = "white" {}
        _BumpScale ("Normal Strength", Float) = 1
        _MossColor ("Moss Tint From Above", Color) = (1,1,1,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        sampler2D _MainTex, _BumpMap, _ARM;
        fixed4 _Color, _MossColor;
        float _BumpScale;
        struct Input { float2 uv_MainTex; float3 worldNormal; INTERNAL_DATA };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            o.Normal = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_MainTex), _BumpScale);
            float3 alb = tex2D(_MainTex, IN.uv_MainTex).rgb * _Color.rgb;
            float up = saturate(WorldNormalVector(IN, o.Normal).y);
            alb = lerp(alb, alb * _MossColor.rgb, _MossColor.a * up);
            float3 a = tex2D(_ARM, IN.uv_MainTex).rgb;
            o.Albedo = alb;
            o.Occlusion = a.r;
            o.Smoothness = 1.0 - a.g;
            o.Metallic = a.b;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
