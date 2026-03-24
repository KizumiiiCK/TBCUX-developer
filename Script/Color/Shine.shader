Shader "Hidden/Shine" {
    Properties {
        _MainTex ("Texture", 2D) = "white" { }
        _ShineColor ("Shine Color", Color) = (1, 1, 1, 1)
        _ShineRotate ("Rotate Angle(radians)", Range(0, 6.2831)) = 0
        _ShineWidth ("Shine Width", Range(0.05, 1)) = 0.1
        _ShineGlow ("Shine Glow", Range(0, 100)) = 1

        _ShineSpeed ("Shine Speed", Range(0.1, 10)) = 1       // NEW
        _TimeGap ("Time Gap Between Shines", Range(0, 5)) = 1 // NEW
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
            half4 _ShineColor;
            half _ShineRotate, _ShineWidth, _ShineGlow;
            half _ShineSpeed, _TimeGap;

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
                o.uv = v.uv;
                o.color=v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target {
                fixed4 col = tex2D(_MainTex, i.uv);

                //----------------------------------------
                // AUTO SHINE: calculate shine location
                //----------------------------------------
                float cycle = 1.0 / _ShineSpeed;              // a shine sweep takes this long
                float totalCycle = cycle + _TimeGap;          // sweep + pause
                float t = fmod(_Time.y, totalCycle);          // cycle timer

                // If in pause time, disable shine
                float shineLoc = 999.0; // off-screen
                if (t < cycle)
                    shineLoc = t / cycle; // 0 ¡ú 1 sweep
                //----------------------------------------

                half2 uvShine = i.uv;
                half cosA = cos(_ShineRotate);
                half sinA = sin(_ShineRotate);
                half2x2 rot = half2x2(cosA, -sinA, sinA, cosA);

                uvShine -= half2(0.5, 0.5);
                uvShine = mul(rot, uvShine);
                uvShine += half2(0.5, 0.5);

                half proj = (uvShine.x + uvShine.y) * 0.5;
                half intensity = 1 - abs(proj - shineLoc) / _ShineWidth;

                intensity *= max(sign(proj - (shineLoc - _ShineWidth)), 0.0)
                           * max(sign((shineLoc + _ShineWidth) - proj), 0.0);

                col.rgb += col.a * intensity * _ShineGlow * _ShineColor;
                col.rgb *= i.color.rgb;
                return col;
            }
            ENDCG
        }
    }
}
