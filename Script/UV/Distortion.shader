Shader "Hidden/Distortion" {
    Properties {
        _MainTex ("Texture", 2D) = "white" { }
        _DistortTex ("Distortion Texture", 2D) = "white" { }//Ť��������ͼ
        _DistortAmount ("Distortion Amount", Range(0, 2)) = 0.5 //Ť���ĳ̶�
        _DistortTexXSpeed ("Scroll speed X", Range(-50, 50)) = 5 //x���ٶ�
        _DistortTexYSpeed ("Scroll speed Y", Range(-50, 50)) = 5 //y���ٶ�

    }
    SubShader {
        Tags { "Queue" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha

        Pass {
            Cull off
            ZWrite off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _DistortTex;
            half4 _DistortTex_ST;
            half _DistortTexXSpeed, _DistortTexYSpeed, _DistortAmount;

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float2 uvDistTex : TEXCOORD3;
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.uvDistTex = TRANSFORM_TEX(v.uv, _DistortTex);
                o.color=v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target {
                i.uvDistTex.x += (_Time * _DistortTexXSpeed) % 1;//����ʱ������˶�Ť������Ĳ���λ�á�ͨ��ȡģ���㣬ȷ���˲���λ����[0, 1]��Χ��
                i.uvDistTex.y += (_Time * _DistortTexYSpeed) % 1;//ͬ��
                ////���ݴ�����ͼ��r����Ť��ǿ�ȵļ��� -0.5��Ҫ��[0,1]ӳ�䵽[-0.5, 0.5]
                half distortAmnt = (tex2D(_DistortTex, i.uvDistTex).r - 0.5) * 0.2 * _DistortAmount;
                i.uv.x += distortAmnt;//���ݼ������Ť��ǿ����i.uv������ƫ��
                i.uv.y += distortAmnt;//ͬ��
                fixed4 col = tex2D(_MainTex, i.uv);//����
                col.a *= i.color.a;
                return col;
            }
            ENDCG
        }
    }
}