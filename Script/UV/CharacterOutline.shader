Shader "Hidden/CharacterOutline"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (1,0,0,1)
        _HighlightColor ("Highlight Color", Color) = (1,1,1,1)
        _OutlineWidth ("Outline Width", Float) = 3
        _HighlightStrength ("Highlight Strength", Range(0, 1)) = 0.6
        _ScrollSpeed ("Scroll Speed", Float) = 2
        _WaveFrequency ("Wave Frequency", Float) = 24
        _PulseIntensity ("Pulse Intensity", Float) = 1
    }
    
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        
        // Pass 1: 渲染角色到临时 RT（将角色 alpha 写入 R 通道）
        Pass
        {
            Name "SILHOUETTE"
            ColorMask R
            ZWrite Off
            ZTest Always
            Cull Off
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            
            sampler2D _MainTex;
            
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                float a = tex2D(_MainTex, i.uv).a;
                return float4(a, 0, 0, 1);
            }
            ENDCG
        }
        
        // Pass 2: 后处理描边
        Pass
        {
            Name "OUTLINE"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };
            
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _OutlineColor;
            float4 _HighlightColor;
            float _OutlineWidth;
            float _HighlightStrength;
            float _ScrollSpeed;
            float _WaveFrequency;
            float _PulseIntensity;
            
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                float2 offset = _MainTex_TexelSize.xy * _OutlineWidth;
                
                // 采样周围 8 方向
                float alpha = 0;
                alpha += tex2D(_MainTex, i.uv + float2(-1, -1) * offset).r;
                alpha += tex2D(_MainTex, i.uv + float2( 0, -1) * offset).r;
                alpha += tex2D(_MainTex, i.uv + float2( 1, -1) * offset).r;
                alpha += tex2D(_MainTex, i.uv + float2(-1,  0) * offset).r;
                alpha += tex2D(_MainTex, i.uv + float2( 1,  0) * offset).r;
                alpha += tex2D(_MainTex, i.uv + float2(-1,  1) * offset).r;
                alpha += tex2D(_MainTex, i.uv + float2( 0,  1) * offset).r;
                alpha += tex2D(_MainTex, i.uv + float2( 1,  1) * offset).r;
                
                // 当前透明但周围有像素 = 边缘
                float center = tex2D(_MainTex, i.uv).r;
                float edge = saturate(alpha) * (1 - center);
                edge = smoothstep(0.02, 0.6, edge);

                float phase = (i.uv.x + i.uv.y) * _WaveFrequency - _Time.y * _ScrollSpeed;
                float roll = 0.5 + 0.5 * sin(phase);
                float highlight = saturate(roll * _HighlightStrength);

                float pulse = max(0.0, _PulseIntensity);
                float3 finalColor = lerp(_OutlineColor.rgb, _HighlightColor.rgb, highlight) * pulse;
                float finalAlpha = saturate(_OutlineColor.a + highlight * _HighlightColor.a) * edge;
                return float4(finalColor * edge, finalAlpha);
            }
            ENDCG
        }
    }
}