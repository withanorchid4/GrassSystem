Shader "Unlit/Voronoi"
{
    Properties
    {
        _NumClumpTypes("num clump types", Range(1, 40)) = 40  //草丛的种类数，一个种类对应一种配置
        _NumClumps("num clumps", Range(1, 100)) = 2  //草丛数量，即色块数
    }
    SubShader
    {

        cull off
        zwrite off
        ztest always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            int _NumClumpTypes;
            int _NumClumps;

            float2 Hash22(float2 p) //随机生成2维变量的函数，两个分量都在0~1之间
            {
                float3 a = frac(float3(p.x, p.y, p.x) * float3(123.34, 234.34, 345.65));
                a += dot(a, a + 34.45);
                return frac(float2(a.x * a.y, a.y * a.z));
            }


            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                //o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                //UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float minDist = 10000.0;
                float id = 0.0; //草丛的类型
                float2 clumpCentre; //最近的种子点坐标

                int clumpLimit = min(100, (int)_NumClumps);
                for(int j = 1; j < clumpLimit; j++)
                {
                    float2 ii = float2(float(j), float(j)); //生成一个二维向量，作为生成随机数的参数
                    float2 p = Hash22(ii); //得到了一个种子点
                    float d = distance(p, i.uv);
                    if(d < minDist)
                    {
                        minDist = d;
                        clumpCentre = p;
                        //id = i;  //不能直接把草丛的id作为草的类型id，因为草数大于种类数
                        id = fmod(j, _NumClumpTypes);
                    }
                }
                float3 col = float3(id, clumpCentre.x, clumpCentre.y);
                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }
}


// Shader "Tutorial402/Voronoi"
// {
//     Properties
//     {
//         _NumClumpTypes("Clump Types", Range(1, 40)) = 40
//         _NumClumps("Clump Count", Range(1, 100)) = 2
//     }

//     SubShader
//     {
//         Cull Off ZWrite Off ZTest Always

//         Pass
//         {
//             HLSLPROGRAM
//             #pragma vertex vert
//             #pragma fragment frag

//             #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

//             float _NumClumpTypes;
//             float _NumClumps;

//             struct appdata
//             {
//                 float4 vertex : POSITION;
//                 float2 uv : TEXCOORD0;
//             };

//             struct v2f
//             {
//                 float2 uv : TEXCOORD0;
//                 float4 vertex : SV_POSITION;
//             };

//             v2f vert (appdata v)
//             {
//                 v2f o;
//                 o.vertex = TransformObjectToHClip(v.vertex.xyz);
//                 o.uv = v.uv;
//                 return o;
//             }

//             float2 Hash22(float2 p)
//             {
//                 float3 a = frac(float3(p.x, p.y, p.x) * float3(123.34, 234.34, 345.65));
//                 a += dot(a, a + 34.45);
//                 return frac(float2(a.x * a.y, a.y * a.z));
//             }

//             float4 frag (v2f i) : SV_Target
//             {
//                 float minDist = 100000.0;
//                 float id = 0.0;
//                 float2 clumpCentre = float2(0.0, 0.0);

//                 int clumpLimit = min(100, (int)_NumClumps);

//                 for (int j = 1; j < clumpLimit; j++)
//                 {
//                     float2 jj = float2(float(j), float(j));
//                     float2 p = Hash22(jj);
//                     float d = distance(p, i.uv);

//                     if (d < minDist)
//                     {
//                         minDist = d;
//                         id = fmod(float(j), _NumClumpTypes);
//                         clumpCentre = p;
//                     }
//                 }

//                 float3 col = float3(id, clumpCentre.x, clumpCentre.y);
//                 return float4(col.xyz, 1.0);
//             }
//             ENDHLSL
//         }
//     }
// }