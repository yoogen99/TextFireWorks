Shader "TextureFireWorks/Text Sparks"
{
    Properties
    {
        [HDR] _Color ("火花の色", Color) = (1,0.5,0.1,1)
        _Elapsed ("経過時間", Float) = -1
        _Durations ("上昇・展開・維持・消失", Vector) = (2,1.2,2,2)
        _LaunchPoint ("発射位置", Vector) = (0,0,0,0)
        _BurstPoint ("開花位置", Vector) = (0,15,0,0)
        _SparkSize ("火花の大きさ", Float) = 0.045
        _Gravity ("落下加速度", Float) = 1.5
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "DisableBatching"="True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            float4 _Color, _Durations, _LaunchPoint, _BurstPoint;
            float _Elapsed, _SparkSize, _Gravity;
            struct appdata
            {
                float4 vertex : POSITION;
                float2 corner : TEXCOORD0;
                float2 data : TEXCOORD1; // stable seed, particle kind
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float alpha : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float seed = v.data.x;
                float kind = v.data.y;
                float age = _Elapsed - _Durations.x;
                float spread = saturate(age / max(0.01, _Durations.y));
                float ease = 1 - pow(1 - spread, 3);
                float falling = max(0, age - _Durations.y - _Durations.z);
                float fade = 1 - saturate(falling / max(0.01, _Durations.w));
                float3 center = _BurstPoint.xyz + v.vertex.xyz * ease;
                center.y -= 0.5 * _Gravity * falling * falling;
                center.x += sin(seed * 83 + falling * 2) * falling * 0.07;
                float size = _SparkSize;
                float alpha = step(0, age) * step(0, _Elapsed) * fade * fade;
                alpha *= 0.72 + 0.28 * sin(seed * 731 + _Elapsed * (8 + seed * 9));
                if (kind > 0.5 && kind < 1.5)
                {
                    center = _BurstPoint.xyz + v.vertex.xyz * ease;
                    center.y -= 0.5 * _Gravity * max(age, 0) * max(age, 0);
                    alpha *= 1 - saturate(age / max(0.01, _Durations.y + 0.7));
                    size *= 0.7;
                }
                if (kind > 1.5 && kind < 3.5)
                {
                    float delay = kind > 2.5 ? seed * 0.45 : 0;
                    float travel = saturate((_Elapsed - delay) / max(0.01, _Durations.x));
                    center = lerp(_LaunchPoint.xyz, _BurstPoint.xyz, 1 - pow(1 - travel, 1.6));
                    center.x += sin(travel * 7) * 0.08;
                    size *= kind > 2.5 ? 0.5 + seed : 3;
                    alpha = step(delay, _Elapsed) * (1 - step(_Durations.x, _Elapsed));
                    if (kind > 2.5) alpha *= (1 - seed) * 0.7;
                }
                if (kind > 3.5)
                {
                    center = _BurstPoint.xyz;
                    float flash = saturate(age / 0.28);
                    size *= lerp(10, 50, flash);
                    alpha = step(0, age) * pow(1 - flash, 3);
                }
                float3 world = mul(unity_ObjectToWorld, float4(center, 1)).xyz;
                // Billboard only each spark; the text plane stays fixed in world space.
                float3 right = UNITY_MATRIX_I_V._m00_m10_m20;
                float3 up = UNITY_MATRIX_I_V._m01_m11_m21;
                world += (right * v.corner.x + up * v.corner.y) * size;
                o.vertex = UnityWorldToClipPos(world);
                o.uv = v.corner;
                o.alpha = alpha;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float radius = length(i.uv);
                float glow = pow(saturate(1 - radius), 2);
                return fixed4(_Color.rgb * 2, glow * i.alpha * _Color.a);
            }
            ENDCG
        }
    }
}
