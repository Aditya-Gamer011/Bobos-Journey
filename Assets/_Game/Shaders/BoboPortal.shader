// Swirling dimensional portal. Drawn on a disc/quad with UVs 0..1.
Shader "Bobo/Portal"
{
    Properties
    {
        _ColorA ("Inner", Color) = (1,0.95,1,1)
        _ColorB ("Mid", Color) = (0.75,0.35,1,1)
        _ColorC ("Edge", Color) = (0.25,0.1,0.55,1)
        _Arms ("Spiral Arms", Float) = 5
        _Twist ("Twist", Float) = 7
        _Speed ("Speed", Float) = 1.4
        _Open ("Open (0-1)", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _ColorA, _ColorB, _ColorC; float _Arms, _Twist, _Speed, _Open;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord.xy; return o; }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * 2.0;
                float r = length(p) / max(_Open, 0.001);
                float a = atan2(p.y, p.x);
                float t = _Time.y * _Speed;
                float spiral = sin(a * _Arms + r * _Twist - t * 3.0);
                float bands = smoothstep(-0.15, 0.15, spiral);
                float3 col = lerp(_ColorB.rgb, _ColorA.rgb, saturate(1.0 - r * 1.15));
                col = lerp(col, _ColorC.rgb, bands * smoothstep(0.15, 0.9, r) * 0.8);
                // hand-inked rim
                float rim = smoothstep(0.86, 0.9, r) * (1.0 - smoothstep(0.96, 1.0, r));
                col = lerp(col, float3(0.1, 0.04, 0.16), rim);
                float alpha = 1.0 - smoothstep(0.97, 1.0, r);
                float glow = (1.0 - smoothstep(1.0, 1.35, r)) * 0.45 * step(1.0, r);
                return fixed4(lerp(col, _ColorB.rgb, step(1.0, r)), max(alpha, glow));
            }
            ENDCG
        }
    }
}
