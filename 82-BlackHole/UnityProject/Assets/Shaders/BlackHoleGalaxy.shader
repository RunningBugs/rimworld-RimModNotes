// BlackHole 黑洞着色器
// 银河缓慢旋转 + 程序化十字星闪烁，全部 GPU 计算，单 Pass。
// 时间源 _RealTimeSecs 由 C# 侧每帧传入（Time.realtimeSinceStartup），
// 纯 FPS 驱动，不受游戏 TPS / 暂停 / timeScale 影响；
// 未传入时退化为 Unity 内置 _Time.y。
Shader "BlackHole/GalaxyTwinkle"
{
    Properties
    {
        _MainTex ("Galaxy Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _SpinSpeed ("Spin Speed (rad/s)", Float) = 0.35
        _StarScale ("Star Size Scale", Float) = 1.0
        _StarSpeed ("Star Cycle Speed", Float) = 0.55
        _MaxStars ("Max Simultaneous Stars", Float) = 4
        _StarSlots ("Star Slots", Float) = 8
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "ForceNoShaderCasting"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Fog { Mode Off }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _SpinSpeed;
            float _StarScale;
            float _StarSpeed;
            float _MaxStars;
            float _StarSlots;
            float _RealTimeSecs;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }

            float hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // 链式随机：p_{n+1} = A·p_n + c (mod 1)，从固定种子迭代 steps 步
            // 纯确定性函数，无系统随机数，位置序列不重复套路
            float2 ChainPos(float slot, int steps)
            {
                float2 p = float2(hash12(float2(slot * 7.31 + 0.13, slot * 3.77 + 0.71)),
                                  hash12(float2(slot * 13.7 + 0.29, slot * 5.13 + 0.47)));
                float a1 = 12.9898 + hash12(float2(slot, 1.7)) * 7.0;
                float a2 = 78.233  + hash12(float2(slot, 9.3)) * 5.0;
                float c1 = 0.5431 + hash12(float2(slot, 4.2)) * 0.3;
                float c2 = 0.2177 + hash12(float2(slot, 6.8)) * 0.3;
                const int CYCLE = 97;
                for (int i = 0; i < CYCLE; i++)
                {
                    if (i < steps)
                    {
                        p = frac(float2(a1 * p.x + 0.7531 * p.y + c1,
                                        a2 * p.y + 0.6181 * p.x + c2));
                    }
                }
                float r = 0.07 + 0.41 * sqrt(frac(p.x * 3.71));
                float a = p.y * 6.2831853;
                return float2(0.5 + cos(a) * r, 0.5 + sin(a) * r);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _RealTimeSecs > 0.0 ? _RealTimeSecs : _Time.y;

                // ---- 1) 银河缓慢旋转（越界不环绕采样，直接透明，消灭四角残臂）----
                float2 c = i.uv - 0.5;
                float ang = t * _SpinSpeed;
                float sn = sin(ang), cs = cos(ang);
                float2 ruv = float2(c.x * cs - c.y * sn, c.x * sn + c.y * cs) + 0.5;
                if (ruv.x < 0.0 || ruv.x > 1.0 || ruv.y < 0.0 || ruv.y > 1.0)
                {
                    return fixed4(0, 0, 0, 0);
                }
                fixed4 col = tex2D(_MainTex, ruv);

                // ---- 2) 程序化十字星（链式随机位置，各槽独立节奏，互不同步）----
                float ns = max(_StarSlots, 1.0);
                float maxs = clamp(_MaxStars, 1.0, ns);
                float star = 0.0;
                for (int k = 0; k < 16; k++)
                {
                    if (k >= (int)ns) break;
                    // 每槽独立时间轴（相位错开，永不集体同步呼吸）
                    float tk = t * _StarSpeed + hash12(float2(k, 2.7)) * 8.0;
                    float segK = floor(tk);
                    float ftK = frac(tk);
                    // 可见窗口：黄金比例错列，任意时刻约 maxs 颗亮着
                    float on = frac(segK * 0.61803399 + k * 0.3731) < maxs / ns ? 1.0 : 0.0;
                    if (on < 0.5) continue;
                    float2 sp = ChainPos(k, (int)(segK % 97.0 + 97.0) % 97);
                    float2 d = i.uv - sp;
                    float dx = abs(d.x), dy = abs(d.y);
                    float sc = _StarScale;
                    // 大号十字星：亮核 + 光晕 + 四道长光芒
                    float core  = exp(-(dx * dx + dy * dy) * 900.0 / sc);
                    float halo  = exp(-(dx * dx + dy * dy) * 110.0 / sc) * 0.35;
                    float spike = (exp(-dx * 34.0 / sc) * exp(-dy * dy * 700.0 / sc)
                                +  exp(-dy * 34.0 / sc) * exp(-dx * dx * 700.0 / sc)) * 0.9;
                    float tw = 0.72 + 0.28 * sin(t * (6.0 + 6.0 * hash12(float2(k, 3.3))) + hash12(float2(k, 9.1)) * 6.28);
                    float env = pow(sin(3.14159265 * ftK), 0.7);
                    star += (core * 1.3 + halo + spike) * env * tw;
                }
                star = saturate(star);

                col.rgb += star * fixed3(1.0, 1.0, 0.98);
                col.a = saturate(col.a + star * 0.95);
                col *= _Color;
                return col;
            }
            ENDCG
        }
    }
    Fallback "Transparent/Diffuse"
}
