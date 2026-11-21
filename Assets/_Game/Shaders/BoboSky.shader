// Painterly gradient skybox with a soft sun, banded clouds and optional stars.
Shader "Bobo/Sky"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.55,0.78,0.95,1)
        _HorizonColor ("Horizon", Color) = (0.98,0.92,0.82,1)
        _BottomColor ("Bottom", Color) = (0.75,0.85,0.7,1)
        _SunColor ("Sun", Color) = (1,0.97,0.85,1)
        _SunDir ("Sun Direction", Vector) = (0.3,0.35,0.9,0)
        _SunSize ("Sun Size", Range(0,0.2)) = 0.06
        _CloudColor ("Cloud Color", Color) = (1,1,1,0.6)
        _CloudAmount ("Cloud Amount", Range(0,1)) = 0.5
        _Stars ("Stars", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _TopColor, _HorizonColor, _BottomColor, _SunColor, _CloudColor;
            float4 _SunDir; float _SunSize, _CloudAmount, _Stars;
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };
            v2f vert(appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.dir = v.vertex.xyz; return o; }

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1,0)), f.x), lerp(hash(i + float2(0,1)), hash(i + float2(1,1)), f.x), f.y);
            }
            float fbm(float2 p) { float s = 0, a = 0.5; for (int k = 0; k < 4; k++) { s += noise(p) * a; p *= 2.03; a *= 0.5; } return s; }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float y = d.y;
                float3 col = y > 0 ? lerp(_HorizonColor.rgb, _TopColor.rgb, pow(saturate(y), 0.55))
                                   : lerp(_HorizonColor.rgb, _BottomColor.rgb, saturate(-y * 3.0));

                // posterised cloud bands (keeps a painted, not photographic, look)
                float2 cp = d.xz / (abs(y) + 0.25) * 1.3 + _Time.y * 0.01;
                float c = fbm(cp * 1.4);
                float cloud = smoothstep(1.0 - _CloudAmount * 0.75, 1.05 - _CloudAmount * 0.75, c) * saturate(y * 4.0);
                float cloudShade = smoothstep(0.55, 0.75, fbm(cp * 1.4 + 0.35));
                col = lerp(col, lerp(_CloudColor.rgb, _CloudColor.rgb * 0.82 + _TopColor.rgb * 0.1, cloudShade), cloud * _CloudColor.a);

                float3 sd = normalize(_SunDir.xyz);
                float sdot = dot(d, sd);
                float sun = smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.9, sdot);
                float halo = pow(saturate(sdot), 18.0) * 0.35;
                col = lerp(col, _SunColor.rgb, saturate(sun + halo));

                float2 sp = float2(atan2(d.z, d.x) * 40.0, y * 90.0);
                float star = step(0.985, hash(floor(sp))) * smoothstep(0.0, 0.3, y);
                col += star * _Stars * (0.6 + 0.4 * sin(_Time.y * 3.0 + hash(floor(sp)) * 20.0));
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
