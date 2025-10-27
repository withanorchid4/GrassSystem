Shader "Grass/BezierGrass"
{
    Properties
    {
        [Header(Shape)]
        // _Height ("Height", Float) = 1  //草叶长度
        // _Tilt ("Tilt", Range(0,1)) = 0.9  //取值0~1，用于×草叶长度，得到弯曲后的草尖到地面的垂直高度
        // _BladeWidth ("BladeWidth", Float) = 0.1  //草叶底部宽度（下宽上窄） 
        _TaperAmount ("Taper Amount", Range(0,1)) = 0   //草叶宽度从底部到顶部的衰减速度
        _CurveNormalAmount ("Curve Normal Amount", Range(0,5)) = 1
        _p1Offset ("p1Offset", Range(0,1)) = 1
        _p2Offset ("p2Offset", Range(0,1)) = 1

        [Header(Shading)]
        _TopColor ("Top Color", Color) = (.25, .5, .5, 1)
        _BottomColor ("Bottom Color", Color) = (.25, .5, .5, 1)
        _GrassAlbedo("Grass albedo", 2D) = "white" {}
        _GrassGloss("Grass gloss", 2D) = "white" {}

        [Header(Wind Animation)]
        _WaveAmplitude("Wave Amplitude", Float) = 1  //P2  P3两个控制点的额外摆动幅度
        _WaveSpeed("Wave Speed", Float) = 1  //额外的摆动速度
        _SinOffsetRange("Phase Variation", Range(0, 10)) = 0.3  //相位差，P2 P3摆动不一致
        _PushTipForward("Push Tip Forward", Range(0, 2)) = 0  //P3的移动属性
    }
    SubShader
    {
        Tags { 
            "RenderType"="Opaque" 
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "Bezier Grass"
            Tags {"LightMode"="UniversalForward"}
            cull off

            HLSLPROGRAM

            #pragma perfer hlslcc opengl
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOW_SOFT
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "CubicBezier.hlsl"

            struct GrassBlade
            {
                float3 position;
                float rotAngle;
                float hash;
                float height;
                float width;
                float tilt;
                float bend;
                float3 surfaceNorm;
                float windForce;
                float sideBend;
            };
            StructuredBuffer<GrassBlade> grassBladeBuffer;
            StructuredBuffer<int> trianglesBuffer;
            //此处没有用到position buffer是因为在渲染的时候不会用到原来mesh中的顶点pos,而是在vert中生成
            StructuredBuffer<float4> colorBuffer;
            StructuredBuffer<float2> uvBuffer;

            //shape
            // float _Height;
            // float _Tilt;
            // float _BladeWidth;
            float _TaperAmount;
            float _p1Offset;
            float _p2Offset;
            float _CurveNormalAmount;

            //shading
            float4 _TopColor;
            float4 _BottomColor;

            //wind sine
            float _WaveAmplitude;
            float _WaveSpeed;
            float _SinOffsetRange;
            float _PushTipForward;

            TEXTURE2D(_GrassAlbedo);
            SAMPLER(sampler_GrassAlbedo);
            TEXTURE2D(_GrassGloss);
            SAMPLER(sampler_GrassGloss);

            struct appdata
            {
                // float4 vertex : POSITION;
                // float4 color : COLOR;
                // float2 uv : TEXCOORD0;
                uint vertexID : SV_VertexID;
                uint instanceID : SV_InstanceID;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 curvedNormal : TEXCOORD1;
                float3 originalNormal : TEXCOORD2;
                float2 uv : TEXCOORD3;
                float t : TEXCOORD4;

            };


            //计算p0 p1 p2 p3四个控制点的位置

            float3 GetP0() //p0直接在原点位置
            {
                return float3(0,0,0);
            }

            float3 GetP3(float height, float tilt) //先假设P0P1P2P3是倾斜直线，然后tile就是直角三角形的tan
            {
                float p3y = tilt * height;
                float p3x = sqrt(height * height - p3y * p3y);
                return float3(-p3x, p3y, 0);
            }

            //风效 sine  输出：P1 P2 P3
            void GetP1P2P3(float3 p0, inout float3 p3, float bend, float hash, float windForce, out float3 p1, out float3 p2)
            {
                p1 = lerp(p0, p3, 0.33);
                p2 = lerp(p0, p3, 0.66);

                float3 bladeDir = normalize(p3 - p0);
                float3 bezCtrlOffsetDir = normalize(cross(bladeDir, float3(0,0,1)));

                p1 += bezCtrlOffsetDir * bend * _p1Offset;
                p2 += bezCtrlOffsetDir * bend * _p2Offset;

                float p2WindEffect = sin((_Time.y + hash * 2 * PI) * _WaveSpeed + 0.66 * 2 * PI * _SinOffsetRange) * windForce;
                p2WindEffect *= 0.66 * _WaveAmplitude;

                float p3WindEffect = sin((_Time.y + hash * 2 * PI) * _WaveSpeed + 1.0 * 2 * PI * _SinOffsetRange) * windForce + _PushTipForward * (1 - bend);
                p3WindEffect *= _WaveAmplitude;

                p2 += bezCtrlOffsetDir * p2WindEffect;
                p3 += bezCtrlOffsetDir * p3WindEffect;
            }

            float3x3 RotAxis3x3(float angle, float3 axis) //根据旋转轴和旋转角求旋转矩阵
            {
                axis = normalize(axis);
                
                float s, c;
                sincos(angle, s, c);
                
                // 1 - cos(angle)
                float t = 1.0 - c;
                
                // 轴的分量
                float x = axis.x;
                float y = axis.y;
                float z = axis.z;
                
                float xy = x * y;
                float xz = x * z;
                float yz = y * z;
                float xs = x * s;
                float ys = y * s;
                float zs = z * s;
                
                float m00 = t * x * x + c;
                float m01 = t * xy - zs;
                float m02 = t * xz + ys;
                
                float m10 = t * xy + zs;
                float m11 = t * y * y + c;
                float m12 = t * yz - xs;
                
                float m20 = t * xz - ys;
                float m21 = t * yz + xs;
                float m22 = t * z * z + c;
                
                return float3x3(
                    m00, m01, m02,
                    m10, m11, m12,
                    m20, m21, m22
                );
            }


            v2f vert (appdata v)
            {
                v2f o;

                GrassBlade blade = grassBladeBuffer[v.instanceID];
                float bend = blade.bend;
                float height = blade.height;
                float tilt = blade.tilt;

                float hash = blade.hash;
                float windForce = blade.windForce;

                //生成四个控制点
                float3 p0 = GetP0();
                float3 p3 = GetP3(height, tilt);
                float3 p1 = float3(0,0,0);
                float3 p2 = float3(0,0,0);
                GetP1P2P3(p0, p3, bend, hash, windForce, p1, p2);

                //从compute buffer中获取顶点
                int positionIndex = trianglesBuffer[v.vertexID];
                //float3 position = blade.position;
                float4 vertColor = colorBuffer[positionIndex];
                float2 vertUV = uvBuffer[positionIndex];

                //GrassMesh中的color属性的r通道从0~1变化，表示此顶点在曲线中的位置，此处用其插值bezier曲线得到对应位置
                float t = vertColor.r; 

                float3 centerPos = CubicBezier(p0, p1, p2, p3, t); //此位置在草叶的中轴线上
                //计算顶点还需要草叶宽度
                float width = blade.width * (1 - t * _TaperAmount); //从下往上衰减，最下面的地方t=0，宽度就是BladeWidth
                //此顶点是center左边顶点还是右边顶点用color.g区分，0为左，1为右，尖端处传入的是0.5
                float side = vertColor.g * 2 - 1; //从0~1映射到-1~1
                //side×上宽度放在z通道上就可以啦（bezier曲线是z=0的xy上的一条曲线，作为中轴线，向+/-z上偏移宽度得到草叶形状）
                float3 positionOS = float3(centerPos.x, centerPos.y, side * width);

                //下面用于计算法线
                float3 tangent = CubicBezierTangent(p0, p1, p2, p3, t);
                float3 normal = normalize(cross(tangent, float3(0,0,1)));//这是模型空间的法线，还要转到世界空间
                //弯曲一下法线，使得表面看起来圆润
                float3 curvedNorm = normal;
                curvedNorm.b += side * _CurveNormalAmount;
                curvedNorm = normalize(curvedNorm);

                float angle = blade.rotAngle;
                float sideBend = blade.sideBend;

                float3x3 rotMat = RotAxis3x3(-angle, float3(0,1,0));

                float3x3 sideRot = RotAxis3x3(sideBend, normalize(tangent));

                //因为边缘要bend，当前的point就在center左右侧的width处，弯一下
                float3 position = positionOS - centerPos;  //边缘顶点相对于center的位置
                normal = mul(sideRot, normal);
                curvedNorm = mul(sideRot, curvedNorm);
                position = mul(sideRot, position);   //转一下

                positionOS = position + centerPos;   //用新的相对位置更新绝对位置

                //绕y轴旋转
                normal = mul(rotMat, normal);
                curvedNorm = mul(rotMat, curvedNorm);
                positionOS = mul(rotMat, positionOS);

                float3 worldPos = positionOS + blade.position;
                
                o.curvedNormal = curvedNorm;
                o.originalNormal = normal;
                o.uv = vertUV;
                o.t = t;
                //收集一下世界空间坐标，传给fs做光照
                o.positionWS = worldPos;
                //MVP变换
                o.positionCS = TransformWorldToHClip(worldPos);

                return o;
            }

            // 简单光照计算
            // float4 frag (v2f i) : SV_Target
            // {
                
            //     Light mainLight = GetMainLight();
            //     float3 N = normalize(i.curvedNormal);
            //     float3 L = normalize(mainLight.direction);
            //     float3 V = normalize(GetCameraPositionWS() - i.positionWS);
                
            //     float3 H = normalize(L+V);
                
            //     //计算漫反射
            //     float diffuse = saturate(dot(N, L));
            //     //高光
            //     float specular = pow(saturate(dot(N, H)), 128) * mainLight.color;
            //     //环境光
            //     float3 ambient = SampleSH(N) * 0.1;

            //     float3 color = ambient + mainLight.color * float3(0,1,0) * diffuse + specular * float3(1,1,1); 

            //     return float4(color, 1);
            // }

            // float4 frag (v2f input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            // {
            //     float3 n = isFrontFace ? input.curvedNormal : -reflect(-normalize(input.curvedNormal), normalize(input.originalNormal));

            //     Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));

            //     float3 v = normalize(GetCameraPositionWS() - input.positionWS);

            //     //纹理上的颜色
            //     float3 grassAlbedo = saturate(_GrassAlbedo.Sample(sampler_GrassAlbedo, input.uv));
            //     //头尾插值出来一个颜色
            //     float4 grassCol = lerp(_BottomColor, _TopColor, input.t);
            //     //两色相乘
            //     float3 albedo = grassAlbedo * grassCol.rgb;

            //     float gloss =(1 - _GrassGloss.Sample(sampler_GrassGloss, input.uv)) * 0.2;

            //     half3 GI = SampleSH(n);

            //     BRDFData brdfData;
            //     half alpha = 1;

            //     InitializeBRDFData(albedo, 0, half3(1, 1, 1), gloss, alpha, brdfData);
            //     float3 directBRDF = DirectBRDF(brdfData, n, mainLight.direction, v) * mainLight.color;

            //     // Final color calculation
            //     float3 finalColor = GI * albedo + directBRDF * (mainLight.shadowAttenuation * mainLight.distanceAttenuation);

            //     float4 col;
            //     col = float4(finalColor, grassCol.a); // Alpha from grassCol

            //     return half4(col);
            // }

            //todo：看懂这个函数，弄清楚新加的宏的含义
            half4 frag(v2f i, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                // Calculate normal
                float3 n = isFrontFace ? normalize(i.curvedNormal) : -reflect(-normalize(i.curvedNormal), normalize(i.originalNormal));

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float3 v = normalize(GetCameraPositionWS() - i.positionWS);

                float3 grassAlbedo = saturate(_GrassAlbedo.Sample(sampler_GrassAlbedo, i.uv));

                float4 grassCol = lerp(_BottomColor, _TopColor, i.t);

                float3 albedo = grassCol.rgb * grassAlbedo;

                float gloss = (1 - _GrassGloss.Sample(sampler_GrassGloss, i.uv).r) * 0.2;

                half3 GI = SampleSH(n);

                BRDFData brdfData;
                half alpha = 1;

                InitializeBRDFData(albedo, 0, half3(1, 1, 1), gloss, alpha, brdfData);
                float3 directBRDF = DirectBRDF(brdfData, n, mainLight.direction, v) * mainLight.color;

                // Final color calculation
                float3 finalColor = GI * albedo + directBRDF * (mainLight.shadowAttenuation * mainLight.distanceAttenuation);

                float4 col;
                col = float4(finalColor, grassCol.a); // Alpha from grassCol

                return half4(col);
            }
            ENDHLSL
        }
    }
}
