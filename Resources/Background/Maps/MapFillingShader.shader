Shader "Custom/MapFillingShader"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}

        _SpriteHeight ("Sprite Height (UV)", Range(0.1, 2)) = 1
        _ScaleX ("X Tile Scale", Range(0.1, 10)) = 1

        _GroundY ("Ground Line (0-1)", Range(0,1)) = 0.5
        _FadeStrength ("Fade Strength", Range(0.1,5)) = 2
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            float _SpriteHeight;
            float _ScaleX;
            float _GroundY;
            float _FadeStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // --------------------------------------------------
                // X axis tiling
                // --------------------------------------------------
                float tiledX = frac(i.uv.x * _ScaleX);

                // --------------------------------------------------
                // Ground-anchored vertical mapping
                // --------------------------------------------------
                float spriteBottom = _GroundY;
                float spriteTop    = spriteBottom + _SpriteHeight;

                float spriteUVy = (i.uv.y - spriteBottom) / _SpriteHeight;

                // --------------------------------------------------
                // INSIDE SPRITE
                // --------------------------------------------------
                if (spriteUVy >= 0 && spriteUVy <= 1)
                {
                    return tex2D(_MainTex, float2(tiledX, spriteUVy));
                }

                // --------------------------------------------------
                // TOP FILL (fade upward)
                // --------------------------------------------------
                if (spriteUVy > 1)
                {
                    // sample specific pixel (2,1) as requested
                    fixed4 topColor = tex2D(_MainTex, float2(2, 1));
                    float fade = exp(-(spriteUVy - 1) * _FadeStrength);
                    topColor.rgb *= fade;
                    topColor.a = 1;
                    return topColor;
                }

                // --------------------------------------------------
                // BOTTOM FILL (fade downward)
                // --------------------------------------------------
                fixed4 bottomColor = tex2D(_MainTex, float2(1, 0));
                float fade = exp(-abs(spriteUVy) * _FadeStrength);
                bottomColor.rgb *= fade;
                bottomColor.a = 1;
                return bottomColor;
            }
            ENDCG
        }
    }
}
