Shader "Ink/Trail"
{
    Properties
    {
        _MainTex ("Noise Texture", 2D) = "white" {}
        _ColorHead ("Head Color", Color) = (0.8, 0.1, 0.1, 1)
        _ColorTail ("Tail Color", Color) = (0.1, 0.1, 0.1, 0.5)
        _FlowSpeed ("Flow Speed", Float) = 1.0
        _NoiseScale ("Noise Scale", Float) = 2.0
        _EdgeJitter ("Edge Jitter", Float) = 0.05
        _HeadPos ("Head Position", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                float4 color : COLOR;
                float3 worldPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _ColorHead;
            fixed4 _ColorTail;
            float _FlowSpeed;
            float _NoiseScale;
            float _EdgeJitter;
            float4 _HeadPos;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color;
                OUT.worldPos = mul(unity_ObjectToWorld, IN.vertex).xyz;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                
                // 噪声扰动边缘
                float2 noiseUV = float2(uv.x * _NoiseScale + _Time.y * _FlowSpeed, uv.y * _NoiseScale);
                float noise = tex2D(_MainTex, noiseUV).r;
                
                float edgeNoise = tex2D(_MainTex, float2(uv.x * 3 + _Time.y * 0.5, 0)).r;
                float jitter = (edgeNoise - 0.5) * _EdgeJitter;
                
                // 宽度遮罩 + 尾部消散
                float widthMask = 1.0 - smoothstep(0.0, 0.5, abs(uv.y + jitter));
                float fade = 1.0 - smoothstep(0.6, 1.0, uv.x);
                
                // 颜色渐变
                fixed4 col = lerp(_ColorHead, _ColorTail, uv.x);
                
                // 头部高亮
                float headDist = distance(IN.worldPos.xy, _HeadPos.xy);
                float headGlow = exp(-headDist * 2);
                col.rgb += _ColorHead.rgb * headGlow * 0.5;
                
                col.a *= widthMask * fade;
                
                // 墨边：中心红，边缘黑
                float edgeDark = smoothstep(0.0, 0.15, abs(uv.y));
                // col.rgb = lerp(col.rgb, fixed3(0.05,0.05,0.05), edgeDark * 0.6);
                col.rgb = lerp(col.rgb, fixed3(0.05,0.05,0.05), 0);
                
                return col;
            }
            ENDCG
        }
    }
}