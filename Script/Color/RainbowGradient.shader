Shader "Hidden/RainbowGradient" {
    Properties {
        _MainTex ("Texture", 2D) = "white" { }
        _HueSpeed ("Hue Speed", Float) = 0.35
        _HueDensity ("Hue Density", Float) = 2.0
        _Saturation ("Saturation", Range(0, 1)) = 0.85
    }
    SubShader {
        Tags { "Queue" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha

        Pass {
            Cull Off
            ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _HueSpeed;
            float _HueDensity;
            float _Saturation;

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            // HSV: h,s,v in [0,1]
            float3 hsv2rgb(float3 c) {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            fixed4 frag(v2f i) : SV_Target {
                fixed4 texCol = tex2D(_MainTex, i.uv);
                fixed4 col = texCol * i.color;

                // Phase moves right over time: same hue appears at larger uv.x later.
                float h = frac(i.uv.x * _HueDensity - _Time.y * _HueSpeed);
                float3 rainbow = hsv2rgb(float3(h, saturate(_Saturation), 1.0));

                col.rgb *= rainbow;
                return col;
            }
            ENDCG
        }
    }
}
