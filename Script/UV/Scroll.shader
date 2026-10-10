Shader "Hidden/Scroll" {
    Properties {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" { }
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _TextureScrollXSpeed ("Speed X Axis", Range(-5, 5)) = 1
        _TextureScrollYSpeed ("Speed Y Axis", Range(-5, 5)) = 0
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

        Pass {
            Cull off
            Lighting Off
            ZWrite off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                float4 vertex : SV_POSITION;
            };
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            half _TextureScrollXSpeed, _TextureScrollYSpeed;

            v2f vert(appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                // SpriteRenderer passes its Color through the vertex stream. Carrying it
                // to the fragment stage is what makes tint and alpha (fade in / out) work;
                // without the COLOR semantic above the renderer's alpha is silently dropped.
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target {
                // frac() the scrolled uv so sampling always stays inside [0,1): the texture
                // tiles and wraps around instead of the edge texel being smeared by Clamp.
                // The offset is frac()'d first because _Time.y grows without bound - feeding
                // a large value into the uv loses float precision after minutes of play.
                float2 offset = frac(float2(_Time.y * _TextureScrollXSpeed,
                                            _Time.y * _TextureScrollYSpeed));
                fixed4 col = tex2D(_MainTex, frac(i.uv + offset));
                return col * i.color;
            }
            ENDCG
        }
    }
}
