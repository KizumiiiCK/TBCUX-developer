Shader "UI/DialogueCgFill"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ContainerAspect ("Container Aspect", Float) = 1

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _MainTex_TexelSize;
            float4 _ClipRect;
            float _ContainerAspect;

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.worldPosition = IN.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 SampleCenteredCg(float2 uv)
            {
                float textureAspect = _MainTex_TexelSize.z / max(_MainTex_TexelSize.w, 0.0001);
                float containerAspect = max(_ContainerAspect, 0.0001);
                float visibleWidth = saturate(textureAspect / containerAspect);

                if (visibleWidth >= 0.9999)
                {
                    return tex2D(_MainTex, uv);
                }

                float sidePadding = (1.0 - visibleWidth) * 0.5;
                float leftBoundary = sidePadding;
                float rightBoundary = 1.0 - sidePadding;

                float2 topLeftPixelUv = float2(0.5 * _MainTex_TexelSize.x, 1.0 - 0.5 * _MainTex_TexelSize.y);
                float2 bottomRightPixelUv = float2(1.0 - 0.5 * _MainTex_TexelSize.x, 0.5 * _MainTex_TexelSize.y);

                if (uv.x < leftBoundary)
                {
                    return tex2D(_MainTex, topLeftPixelUv);
                }

                if (uv.x > rightBoundary)
                {
                    return tex2D(_MainTex, bottomRightPixelUv);
                }

                float centeredX = saturate((uv.x - leftBoundary) / visibleWidth);
                return tex2D(_MainTex, float2(centeredX, uv.y));
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 color = SampleCenteredCg(IN.texcoord) * IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
