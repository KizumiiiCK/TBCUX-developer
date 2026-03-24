Shader "UI/QuadWarp"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _WarpStrength ("Warp Strength", Range(0, 2)) = 1

        // 四个角点偏移（局部空间，单位同 RectTransform）
        _CornerBL ("Corner BL Offset (UV 0,0)", Vector) = (0, 0, 0, 0)
        _CornerBR ("Corner BR Offset (UV 1,0)", Vector) = (0, 0, 0, 0)
        _CornerTL ("Corner TL Offset (UV 0,1)", Vector) = (0, 0, 0, 0)
        _CornerTR ("Corner TR Offset (UV 1,1)", Vector) = (0, 0, 0, 0)

        // 若使用 SpriteAtlas，可填写该图片在图集中的 UV 范围后得到正确 0~1 角点权重
        _SourceUVMin ("Source UV Min", Vector) = (0, 0, 0, 0)
        _SourceUVMax ("Source UV Max", Vector) = (1, 1, 0, 0)

        // UI Mask / Stencil 兼容参数（与 UI/Default 对齐）
        [HideInInspector]_StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector]_Stencil ("Stencil ID", Float) = 0
        [HideInInspector]_StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector]_StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector]_StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector]_ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Stencil
            {
                Ref [_Stencil]
                Comp [_StencilComp]
                Pass [_StencilOp]
                ReadMask [_StencilReadMask]
                WriteMask [_StencilWriteMask]
            }

            Cull Off
            ZWrite Off
            ZTest [unity_GUIZTestMode]
            ColorMask [_ColorMask]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _WarpStrength;

            float4 _CornerBL;
            float4 _CornerBR;
            float4 _CornerTL;
            float4 _CornerTR;
            float4 _SourceUVMin;
            float4 _SourceUVMax;
            float4 _ClipRect;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;

                float2 uvDenom = max(_SourceUVMax.xy - _SourceUVMin.xy, float2(1e-5, 1e-5));
                float2 uv01 = saturate((v.uv - _SourceUVMin.xy) / uvDenom);
                float u = uv01.x;
                float vCoord = uv01.y;

                // 先按 UV 对四角偏移做双线性插值，再加到原始 UI 顶点上
                float3 bottomOffset = lerp(_CornerBL.xyz, _CornerBR.xyz, u);
                float3 topOffset = lerp(_CornerTL.xyz, _CornerTR.xyz, u);
                float3 offset = lerp(bottomOffset, topOffset, vCoord) * _WarpStrength;
                float3 warpedPos = v.vertex.xyz + offset;

                float4 localPos = float4(warpedPos, 1.0);
                o.worldPosition = localPos;
                o.vertex = UnityObjectToClipPos(localPos);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
