Shader"Custom/BooleanDirectionalGradientShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _GradientColor ("Gradient Color", Color) = (1, 1, 1, 1)
        _IsUpward ("Downward", Float) = 1 // 1 = Upward, 0 = Downward
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
LOD 200

        Pass
        {
Cull off
ZWrite off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
#include "UnityCG.cginc"

struct appdata_t
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
float4 _GradientColor;
float _Downward; // 布尔型，1 表示向上，0 表示向下

v2f vert(appdata_t v)
{
    v2f o;
    o.vertex = UnityObjectToClipPos(v.vertex);
    o.uv = v.uv;
    return o;
}

fixed4 frag(v2f i) : SV_Target
{
                // 采样纹理
    fixed4 texColor = tex2D(_MainTex, i.uv);

                // 计算渐变因子
    float gradientFactor = (_Downward == 1.0) ? i.uv.y : 1.0 - i.uv.y;

                // 计算渐变颜色
    fixed4 gradientColor = _GradientColor * (0.3 + 0.7 * gradientFactor);

                // 返回最终颜色
    return texColor * gradientColor;
}
            ENDCG
        }
    }
FallBack"Diffuse"
}
