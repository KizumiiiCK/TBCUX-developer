Shader "Hidden/Shine" {
    Properties {
        _MainTex ("Texture", 2D) = "white" { }
        _ShineColor ("Shine Color", Color) = (1, 1, 1, 1)
        _ShineRotate ("Rotate Angle(radians)", Range(0, 6.2831)) = 0
        _ShineWidth ("Shine Width", Range(0.05, 1)) = 0.1
        _ShineGlow ("Shine Glow", Range(0, 100)) = 1

        _ShineSpeed ("Shine Speed", Range(0.1, 10)) = 1       // NEW
        _TimeGap ("Time Gap Between Shines", Range(0, 5)) = 1 // NEW

        // UI 遮罩用的标准属性组。MaskableGraphic 在运行时会把遮罩状态写到这些属性上，
        // 材质里没有对应槽位的话这些值就无处可去，遮罩自然不生效。
        // 默认值全是「不裁剪」语义：_StencilComp = 8 即 Always，_ColorMask = 15 即 RGBA。
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader {
        Tags {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }
        Blend SrcAlpha OneMinusSrcAlpha

        // UI 的 Mask 不裁几何体，走的是模板缓冲：Mask 先在自己矩形内往 stencil 写标记，
        // 子图形再拿下面这组属性做模板测试，测不过的像素被丢弃——这才是「被遮罩裁掉」。
        // 缺了这个块，图标会无视遮罩整个画出来，在滚动列表里就是溢出到视口外面。
        // 因为默认值是 Always + 全通道，不在遮罩里的既有用法行为完全不变。
        Stencil {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        ColorMask [_ColorMask]

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
                    shineLoc = t / cycle; // 0 �� 1 sweep
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

                // 顶点色的 alpha 先乘进来，CanvasGroup 淡入淡出才带得动这个材质。
                // 放在加光之前是刻意的：加光量本身就按 col.a 缩放，这样淡出时高光一起淡。
                col.a *= i.color.a;
                col.rgb += col.a * intensity * _ShineGlow * _ShineColor;
                col.rgb *= i.color.rgb;
                return col;
            }
            ENDCG
        }
    }
}
