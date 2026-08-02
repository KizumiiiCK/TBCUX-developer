Shader "Ink/Body"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width", Float) = 0.003
        
        _AttackIntensity ("Attack Intensity", Range(0,1)) = 0
        _ChargeLevel ("Charge Level", Range(0,1)) = 0
        _DashGhost ("Dash Ghost", Range(0,1)) = 0
        _IsBack ("Is Back", Range(0,1)) = 0
        _BreathPhase ("Breath Phase", Range(0,1)) = 0
        _HitFlash ("Hit Flash", Range(0,1)) = 0
        
        _BreathAmp ("Breath Amplitude", Float) = 0.02
        _ChargeDarken ("Charge Darken", Color) = (0.3,0.3,0.3,1)
        
        // === Shadow Ink Diffusion ===
        _EdgeColor ("Ink Flame Color", Color) = (0.02,0.02,0.02,0.95)
        _SpreadRange ("Spread Range (UV)", Float) = 0.42
        _SpreadDirection ("Spread Direction", Vector) = (0,-1,0,0)
        _Directionality ("Directionality", Range(0,1)) = 0.82
        _WobbleSpeed ("Wobble Speed", Float) = 2.0
        _WobbleAmp ("Wobble Amplitude", Float) = 1.6
        _NoiseScale ("Noise Scale", Float) = 4.2
        _EdgeSoftness ("Edge Softness", Float) = 1.35
        _ConeTightness ("Cone Tightness", Float) = 1.8
        _ColorInheritance ("Color Inheritance", Range(0,1)) = 0.62
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

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
            float4 _MainTex_TexelSize;
            sampler2D _NoiseTex;
            float4 _NoiseTex_ST;
            fixed4 _Color;
            fixed4 _OutlineColor;
            float _OutlineWidth;
            
            float _AttackIntensity;
            float _ChargeLevel;
            float _DashGhost;
            float _IsBack;
            float _BreathPhase;
            float _HitFlash;
            
            float _BreathAmp;
            fixed4 _ChargeDarken;
            
            // Params for Shadow Ink
            fixed4 _EdgeColor;
            float _SpreadRange;
            float4 _SpreadDirection;
            float _Directionality;
            float _WobbleSpeed;
            float _WobbleAmp;
            float _NoiseScale;
            float _EdgeSoftness;
            float _ConeTightness;
            float _ColorInheritance;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                float4 v = IN.vertex;
                
                // Character Moves, including breathe, crouch, atk vibration
                float breath = sin(_BreathPhase * 6.28318) * _BreathAmp;
                v.y += breath;
                
                float chargeSquash = lerp(1.0, 0.9, _ChargeLevel);
                v.y *= chargeSquash;
                
                float shake = _AttackIntensity * 0.015 * sin(_Time.y * 60);
                v.x += shake;
                v.y += shake * 0.5;
                
                OUT.vertex = UnityObjectToClipPos(v);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                OUT.worldPos = mul(unity_ObjectToWorld, v).xyz;
                return OUT;
            }

            // Aux Sampling
            float SampleAlpha(float2 uv)
            {
                return tex2D(_MainTex, uv).a;
            }

            float2 SafeNormalize(float2 v, float2 fallbackDir)
            {
                float lenSq = dot(v, v);
                if (lenSq < 1e-5)
                {
                    return fallbackDir;
                }
                return v * rsqrt(lenSq);
            }

            fixed3 ApplyCombatColor(fixed3 baseColor)
            {
                fixed3 tinted = baseColor;
                fixed3 attackRed = fixed3(0.8, 0.1, 0.1);
                tinted = lerp(tinted, attackRed, _AttackIntensity * 0.6);
                tinted = lerp(tinted, fixed3(1,1,1), _HitFlash);
                tinted *= lerp(1.0, 0.85, _IsBack);
                return tinted;
            }

            float ApplyCombatAlpha(float alpha)
            {
                return alpha * lerp(1.0, 0.5, _DashGhost);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float4 mainTex = tex2D(_MainTex, uv);
                float centerAlpha = mainTex.a;
                
                // ==================== Transparent Shadow Ink Area ====================

                if (centerAlpha < 0.5)
                {
                    // 从当前透明像素沿扩散方向反向追踪，寻找角色边缘像素并继承其颜色。
                    // 在追踪轨迹上叠加摆动噪声，形成烟雾和墨点的外扩感。
                    float2 windDir = SafeNormalize(_SpreadDirection.xy, float2(0, -1));
                    float2 fallbackDir = SafeNormalize(uv - float2(0.5, 0.5), float2(0, -1));
                    float2 spreadDir = SafeNormalize(lerp(fallbackDir, windDir, _Directionality), windDir);
                    float2 sideDir = float2(-spreadDir.y, spreadDir.x);

                    float rangeBoost = 1.6 + _WobbleAmp * 0.35 + _AttackIntensity * 0.25;
                    float maxRange = max(0.001, _SpreadRange * rangeBoost);

                    float bestDist = 9999.0;
                    float2 bestEdgeUV = uv;
                    bool foundEdge = false;

                    const int STEPS = 24;
                    float timePhase = _Time.y * _WobbleSpeed;
                    float lateralScale = _MainTex_TexelSize.x * 40.0 * _WobbleAmp;

                    for (int i = 1; i <= STEPS; i++)
                    {
                        float t = float(i) / float(STEPS);
                        float backDist = maxRange * t;

                        float2 wobbleUV = (uv - spreadDir * backDist) * _NoiseScale + float2(timePhase * 0.8, timePhase * 0.45);
                        float noiseA = tex2D(_NoiseTex, wobbleUV).r * 2.0 - 1.0;
                        float noiseB = tex2D(_NoiseTex, wobbleUV * 1.71 + 3.13).g * 2.0 - 1.0;
                        float wave = sin(timePhase * 1.8 + t * 11.0 + noiseB * 2.2);

                        float lateral = (noiseA * 0.7 + wave * 0.3) * lateralScale * (0.3 + t * 1.4);
                        float2 sampleUV = uv - spreadDir * backDist + sideDir * lateral;

                        float insideAlpha = SampleAlpha(sampleUV);
                        float outsideAlpha = SampleAlpha(sampleUV + spreadDir * (_MainTex_TexelSize.x * 2.0));

                        bool edgeHit = insideAlpha > 0.08 && outsideAlpha < insideAlpha * 0.65;
                        if (edgeHit)
                        {
                            bestDist = backDist;
                            bestEdgeUV = sampleUV;
                            foundEdge = true;
                            break;
                        }
                    }

                    if (foundEdge)
                    {
                        float normalizedDist = saturate(bestDist / maxRange);
                        float tail = 1.0 - normalizedDist;
                        float alphaFalloff = pow(tail, max(0.25, _EdgeSoftness));

                        float2 detailUV = uv * (_NoiseScale * 2.5) + float2(timePhase * 0.55, -timePhase * 0.42);
                        float smokeNoise = tex2D(_NoiseTex, detailUV).r;
                        float smokeMask = saturate(0.25 + smokeNoise * 1.1);

                        float speckNoise = tex2D(_NoiseTex, detailUV * 2.3 + 5.77).g;
                        float tipMask = saturate((normalizedDist - 0.35) / 0.65);
                        float inkDots = step(0.82 - tipMask * 0.2, speckNoise) * tipMask;

                        float cone = pow(saturate(dot(SafeNormalize(uv - bestEdgeUV, spreadDir), spreadDir)), _ConeTightness);
                        float spreadAlpha = _EdgeColor.a * alphaFalloff * smokeMask * cone;
                        spreadAlpha = saturate(spreadAlpha + inkDots * 0.28);
                        spreadAlpha = ApplyCombatAlpha(spreadAlpha);

                        if (spreadAlpha > 0.003)
                        {
                            fixed3 edgeSrc = tex2D(_MainTex, bestEdgeUV).rgb;
                            fixed3 inheritColor = lerp(_EdgeColor.rgb, edgeSrc, _ColorInheritance);
                            fixed3 smokeDark = lerp(inheritColor * 0.75, inheritColor, smokeNoise);
                            fixed3 finalSpreadColor = ApplyCombatColor(smokeDark);
                            finalSpreadColor = lerp(finalSpreadColor, fixed3(0.005, 0.005, 0.005), _ChargeLevel * 0.6);
                            finalSpreadColor *= spreadAlpha;
                            return fixed4(finalSpreadColor, spreadAlpha);
                        }
                    }

                    return fixed4(0,0,0,0);
                }
                
                // ==================== Character Main ====================
                
                // Outlines
                float2 uvR = uv + float2(_OutlineWidth, 0);
                float2 uvL = uv - float2(_OutlineWidth, 0);
                float2 uvU = uv + float2(0, _OutlineWidth);
                float2 uvD = uv - float2(0, _OutlineWidth);
                
                float outline = 0;
                outline += tex2D(_MainTex, uvR).a;
                outline += tex2D(_MainTex, uvL).a;
                outline += tex2D(_MainTex, uvU).a;
                outline += tex2D(_MainTex, uvD).a;
                outline = saturate(outline);
                
                fixed4 c = mainTex * IN.color;
                float alpha = c.a;
                
                if (alpha < 0.1 && outline > 0.1)
                {
                    c.rgb = _OutlineColor.rgb;
                    c.a = _OutlineColor.a;
                }
                
                // Skill: Ink charge
                fixed4 finalColor = lerp(c, c * _ChargeDarken, _ChargeLevel * 0.7);
                
                // Attack: Red shade
                finalColor.rgb = ApplyCombatColor(finalColor.rgb);
                
                // Dash Ghost
                finalColor.a = ApplyCombatAlpha(finalColor.a);
                
                finalColor.rgb *= finalColor.a;
                return finalColor;
            }
            ENDCG
        }
    }
}