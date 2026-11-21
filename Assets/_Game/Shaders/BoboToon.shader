// Cel-shaded material used for every character and gameplay prop in Bobo's Journey.
// Two-tone ramp + crisp specular blob + rim band + inverted-hull ink outline.
Shader "Bobo/Toon"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ShadowColor ("Shadow Tint", Color) = (0.62,0.55,0.82,1)
        _ShadowThreshold ("Shadow Threshold", Range(-1,1)) = 0.0
        _ShadowSoftness ("Shadow Softness", Range(0.001,0.5)) = 0.035
        _RimColor ("Rim Color (A = strength)", Color) = (1,0.95,1,0.35)
        _RimThreshold ("Rim Threshold", Range(0,1)) = 0.62
        _SpecColor2 ("Spec Color (A = strength)", Color) = (1,1,1,0.55)
        _SpecSize ("Spec Size", Range(0,0.5)) = 0.04
        _EmissionColor ("Emission", Color) = (0,0,0,0)
        _EmissionMap ("Emission Mask", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (0.13,0.08,0.12,1)
        _OutlineWidth ("Outline Width (px)", Range(0,8)) = 2.2
        [Toggle(_TRIPLANAR)] _Triplanar ("World Triplanar", Float) = 0
        _TriScale ("Triplanar Scale", Float) = 0.25
    }

    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex; float4 _MainTex_ST;
    sampler2D _EmissionMap;
    fixed4 _Color, _ShadowColor, _RimColor, _SpecColor2, _EmissionColor, _OutlineColor;
    float _ShadowThreshold, _ShadowSoftness, _RimThreshold, _SpecSize, _OutlineWidth, _TriScale;
    // scene-wide grading for characters, set by the game: pull saturated / very bright scene lights toward
    // a soft neutral so the cartoon cast keeps its colours inside moody, coloured lighting (0 = off)
    float _BoboLightDesat, _BoboLightCap;
    float3 AdjustLight(float3 L)
    {
        float lum = dot(L, float3(0.3, 0.59, 0.11));
        L = lerp(L, lum.xxx, _BoboLightDesat);
        float m = max(max(L.r, L.g), L.b);
        if (_BoboLightCap > 0 && m > _BoboLightCap) L *= _BoboLightCap / m;
        return L;
    }

    fixed4 SampleAlbedo(float2 uv, float3 wp, float3 n)
    {
    #if _TRIPLANAR
        float3 w = pow(abs(n), 4.0);
        w /= (w.x + w.y + w.z + 1e-5);
        fixed4 cx = tex2D(_MainTex, wp.zy * _TriScale);
        fixed4 cy = tex2D(_MainTex, wp.xz * _TriScale);
        fixed4 cz = tex2D(_MainTex, wp.xy * _TriScale);
        return (cx * w.x + cy * w.y + cz * w.z) * _Color;
    #else
        return tex2D(_MainTex, uv) * _Color;
    #endif
    }
    ENDCG

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #pragma shader_feature_local _TRIPLANAR
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 wn : TEXCOORD1;
                float3 wp : TEXCOORD2;
                SHADOW_COORDS(3)
                UNITY_FOG_COORDS(4)
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.wn);
                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float3 v = normalize(_WorldSpaceCameraPos - i.wp);
                UNITY_LIGHT_ATTENUATION(atten, i, i.wp);

                fixed4 alb = SampleAlbedo(i.uv, i.wp, n);
                float ndl = dot(n, l);
                float lit = smoothstep(_ShadowThreshold - _ShadowSoftness, _ShadowThreshold + _ShadowSoftness, ndl);
                lit *= smoothstep(0.35, 0.65, atten);

                float3 ambient = ShadeSH9(float4(n, 1));
                float3 L = AdjustLight(_LightColor0.rgb);
                ambient = AdjustLight(ambient);
                float3 litCol = alb.rgb * (L + ambient);
                float3 shadeCol = alb.rgb * _ShadowColor.rgb * (ambient + L * 0.32);
                float3 col = lerp(shadeCol, litCol, lit);

                float3 h = normalize(l + v);
                float spec = smoothstep(1.0 - _SpecSize - 0.006, 1.0 - _SpecSize + 0.006, dot(n, h)) * lit;
                col += spec * _SpecColor2.rgb * _SpecColor2.a * L;

                float rim = 1.0 - saturate(dot(n, v));
                float rimBand = smoothstep(_RimThreshold, _RimThreshold + 0.03, rim) * (0.35 + 0.65 * lit);
                col += rimBand * _RimColor.rgb * _RimColor.a;

                col += tex2D(_EmissionMap, i.uv).rgb * _EmissionColor.rgb;

                fixed4 c = fixed4(col, 1);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }

        Pass
        {
            Name "FORWARD_ADD"
            Tags { "LightMode"="ForwardAdd" }
            Blend One One
            ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdadd
            #pragma multi_compile_fog
            #pragma shader_feature_local _TRIPLANAR
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 wn : TEXCOORD1;
                float3 wp : TEXCOORD2;
                SHADOW_COORDS(3)
                UNITY_FOG_COORDS(4)
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.wn);
                float3 l = normalize(_WorldSpaceLightPos0.xyz - i.wp * _WorldSpaceLightPos0.w);
                UNITY_LIGHT_ATTENUATION(atten, i, i.wp);
                fixed4 alb = SampleAlbedo(i.uv, i.wp, n);
                float lit = smoothstep(0.0, 0.08, dot(n, l));
                float band = smoothstep(0.02, 0.12, atten) * 0.55 + atten * 0.45;
                fixed4 c = fixed4(alb.rgb * AdjustLight(_LightColor0.rgb) * lit * band, 0);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, c, fixed4(0,0,0,0));
                return c;
            }
            ENDCG
        }

        Pass
        {
            Name "OUTLINE"
            Tags { "LightMode"="Always" }
            Cull Front
            ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            struct v2f
            {
                float4 pos : SV_POSITION;
                UNITY_FOG_COORDS(0)
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                float4 pos = UnityObjectToClipPos(v.vertex);
                float3 wn = UnityObjectToWorldNormal(v.normal);
                float2 cn = mul((float3x3)UNITY_MATRIX_VP, wn).xy;
                float len = length(cn);
                cn = len > 1e-5 ? cn / len : float2(0, 0);
                // constant pixel width, slightly thinner far away so distant props don't turn to ink blobs
                float fade = saturate(1.6 - pos.w * 0.012);
                pos.xy += cn * (_OutlineWidth * (0.45 + 0.55 * fade)) * (2.0 / _ScreenParams.xy) * pos.w;
                o.pos = pos;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = _OutlineColor;
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }

        Pass
        {
            Name "SHADOWCASTER"
            Tags { "LightMode"="ShadowCaster" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            struct v2f { V2F_SHADOW_CASTER; };
            v2f vert(appdata_base v) { v2f o; TRANSFER_SHADOW_CASTER_NORMALOFFSET(o) return o; }
            float4 frag(v2f i) : SV_Target { SHADOW_CASTER_FRAGMENT(i) }
            ENDCG
        }
    }
    FallBack Off
}
