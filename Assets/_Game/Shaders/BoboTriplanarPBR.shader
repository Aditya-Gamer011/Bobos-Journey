// PBR triplanar surface for the tower: scanned rock, brick or wood projected in world space, with a second
// layer (moss / forest floor) on upward-facing surfaces. Needs no UVs, so it works on procedural meshes.
Shader "Bobo/TriplanarPBR"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Side Albedo", 2D) = "white" {}
        [Normal] _BumpMap ("Side Normal (GL)", 2D) = "bump" {}
        _ARM ("Side AO/Rough/Metal", 2D) = "white" {}
        _Scale ("Side Scale (1/m)", Float) = 0.33
        _BumpScale ("Normal Strength", Float) = 1

        _TopColor ("Top Tint", Color) = (1,1,1,1)
        _TopTex ("Top Albedo", 2D) = "white" {}
        [Normal] _TopBump ("Top Normal (GL)", 2D) = "bump" {}
        _TopARM ("Top AO/Rough/Metal", 2D) = "white" {}
        _TopScale ("Top Scale (1/m)", Float) = 0.4
        _TopAmount ("Top Coverage (-1 off .. 1)", Range(-1,1)) = 0.55
        _TopSharpness ("Top Edge Softness", Range(0.01,1)) = 0.12

        _Wetness ("Extra Smoothness", Range(0,1)) = 0
        [Toggle(_UVMODE)] _UVMode ("Use Mesh UVs (cheap, for big walls)", Float) = 0
        [Toggle(_NOTOP)] _NoTop ("No Top Layer (cheap)", Float) = 0
        _EmissionColor ("Emission", Color) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.5
        #pragma shader_feature_local _UVMODE
        #pragma shader_feature_local _NOTOP

        sampler2D _MainTex, _BumpMap, _ARM, _TopTex, _TopBump, _TopARM;
        fixed4 _Color, _TopColor, _EmissionColor;
        float _Scale, _BumpScale, _TopScale, _TopAmount, _TopSharpness, _Wetness;

        struct Input { float3 worldPos; float3 worldNormal; float2 uv_MainTex; INTERNAL_DATA };

        float3 WorldToTangentNormalVector(Input IN, float3 normal)
        {
            float3 t2w0 = WorldNormalVector(IN, float3(1,0,0));
            float3 t2w1 = WorldNormalVector(IN, float3(0,1,0));
            float3 t2w2 = WorldNormalVector(IN, float3(0,0,1));
            float3x3 t2w = float3x3(t2w0, t2w1, t2w2);
            return normalize(mul(t2w, normal));
        }

        // whiteout-blended triplanar normal (after Ben Golus)
        void Triplanar(sampler2D alb, sampler2D nrm, sampler2D arm, float3 p, float3 n, float scale,
                       out float3 albedo, out float3 wNormal, out float3 armv)
        {
            float3 w = pow(abs(n), 6.0); w /= (w.x + w.y + w.z);
            float3 s = n < 0 ? -1 : 1;
            float2 uvX = p.zy * scale; uvX.x *= s.x;
            float2 uvY = p.xz * scale; uvY.x *= s.y;
            float2 uvZ = p.xy * scale; uvZ.x *= -s.z;
            albedo = tex2D(alb, uvX).rgb * w.x + tex2D(alb, uvY).rgb * w.y + tex2D(alb, uvZ).rgb * w.z;
            armv = tex2D(arm, uvX).rgb * w.x + tex2D(arm, uvY).rgb * w.y + tex2D(arm, uvZ).rgb * w.z;
            float3 tX = UnpackScaleNormal(tex2D(nrm, uvX), _BumpScale);
            float3 tY = UnpackScaleNormal(tex2D(nrm, uvY), _BumpScale);
            float3 tZ = UnpackScaleNormal(tex2D(nrm, uvZ), _BumpScale);
            tX.x *= s.x; tY.x *= s.y; tZ.x *= -s.z;
            tX = float3(tX.xy + n.zy, abs(tX.z) * n.x);
            tY = float3(tY.xy + n.xz, abs(tY.z) * n.y);
            tZ = float3(tZ.xy + n.xy, abs(tZ.z) * n.z);
            wNormal = normalize(tX.zyx * w.x + tY.xzy * w.y + tZ.xyz * w.z);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
        #if _UVMODE
            // single planar lookup through the mesh UVs: a third of the texture reads of the triplanar path
            float2 uv = IN.uv_MainTex * (_Scale / 0.22);
            float3 a0 = tex2D(_ARM, uv).rgb;
            o.Albedo = tex2D(_MainTex, uv).rgb * _Color.rgb;
            o.Normal = UnpackScaleNormal(tex2D(_BumpMap, uv), _BumpScale);
            o.Occlusion = a0.r;
            o.Smoothness = saturate(1.0 - a0.g + _Wetness);
            o.Metallic = 0;
            o.Emission = _EmissionColor.rgb;
            return;
        #endif
            float3 n = WorldNormalVector(IN, float3(0,0,1));
            float3 alb, wn, armv;
            Triplanar(_MainTex, _BumpMap, _ARM, IN.worldPos, n, _Scale, alb, wn, armv);
            alb *= _Color.rgb;

        #if _NOTOP
            o.Albedo = alb;
            o.Normal = WorldToTangentNormalVector(IN, wn);
            o.Occlusion = armv.r;
            o.Smoothness = saturate(1.0 - armv.g + _Wetness);
            o.Metallic = 0;
            o.Emission = _EmissionColor.rgb;
            return;
        #endif
            // top layer, planar from above, with a ragged edge driven by the rock's own detail
            float2 tuv = IN.worldPos.xz * _TopScale;
            float3 talb = tex2D(_TopTex, tuv).rgb * _TopColor.rgb;
            float3 tarm = tex2D(_TopARM, tuv).rgb;
            float3 tn = UnpackScaleNormal(tex2D(_TopBump, tuv), _BumpScale);
            float3 twn = normalize(float3(tn.x, tn.z, tn.y) + float3(n.x, 0, n.z) * 0.5);
            float lum = dot(alb, float3(0.3, 0.59, 0.11));
            float mask = saturate((n.y - _TopAmount + (lum - 0.35) * 0.6) / _TopSharpness);
            mask *= step(-0.99, _TopAmount);

            o.Albedo = lerp(alb, talb, mask);
            float3 finalN = normalize(lerp(wn, twn, mask));
            o.Normal = WorldToTangentNormalVector(IN, finalN);
            float3 a = lerp(armv, tarm, mask);
            o.Occlusion = a.r;
            o.Smoothness = saturate(1.0 - a.g + _Wetness);
            o.Metallic = 0;
            o.Emission = _EmissionColor.rgb;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
