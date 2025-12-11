Shader "Custom/DisintegrationShader"
{
    Properties
    {
        // 基础属性
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        
        // 肢解效果
        _Disintegration ("肢解强度", Range(0, 1)) = 0
        _DisintegrationAmount ("分散度", Range(0, 5)) = 1
        _DisintegrationSpeed ("分散速度", Range(0, 5)) = 1
        _DisintegrationDirection ("分散方向", Vector) = (0,1,0,0)
        
        // 扭曲效果
        _Distortion ("混乱度", Range(0, 1)) = 0
        _DistortionSpeed ("扭曲速度", Range(0, 5)) = 1
        _DistortionScale ("扭曲缩放", Range(0, 10)) = 5
        
        // 闪光效果
        _FlashIntensity ("闪光强度", Range(0, 10)) = 0
        _FlashSpeed ("闪光速度", Range(0, 5)) = 1
        _FlashColor ("闪光颜色", Color) = (1,1,1,1)
        
        // 高级控制
        _NoiseTex ("噪波贴图", 2D) = "white" {}
        _EdgeWidth ("边缘宽度", Range(0, 0.2)) = 0.05
        _EdgeColor ("边缘颜色", Color) = (1,0,0,1)
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        Cull Off // 允许背面渲染以观察内部
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            
            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
                float4 color : COLOR;
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD1;
                float3 normal : TEXCOORD2;
                float4 color : COLOR;
                UNITY_FOG_COORDS(3)
            };
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _NoiseTex;
            float4 _NoiseTex_ST;
            
            float4 _Color;
            float _Disintegration;
            float _DisintegrationAmount;
            float _DisintegrationSpeed;
            float4 _DisintegrationDirection;
            float _Distortion;
            float _DistortionSpeed;
            float _DistortionScale;
            float _FlashIntensity;
            float _FlashSpeed;
            float4 _FlashColor;
            float _EdgeWidth;
            float4 _EdgeColor;
            
            // 伪随机函数
            float rand(float2 co)
            {
                return frac(sin(dot(co.xy, float2(12.9898, 78.233))) * 43758.5453);
            }
            
            // 噪声函数
            float noise(float2 uv)
            {
                return tex2Dlod(_NoiseTex, float4(uv, 0, 0)).r;
            }
            
            // 3D噪声
            float noise3D(float3 p)
            {
                float2 uv = (p.xy + p.z) * 0.1;
                return tex2Dlod(_NoiseTex, float4(uv, 0, 0)).r;
            }
            
            v2f vert (appdata v)
            {
                v2f o;
                
                // 获取噪声值用于各种效果
                float noiseValue = noise3D(v.vertex.xyz + _Time.y * _DistortionSpeed);
                float2 noiseUV = v.uv * _DistortionScale + _Time.y * _DistortionSpeed;
                float distortionNoise = tex2Dlod(_NoiseTex, float4(noiseUV, 0, 0)).r;
                
                // 1. UV扭曲效果
                float2 distortedUV = v.uv;
                if (_Distortion > 0)
                {
                    // 使用噪声扭曲UV
                    distortedUV.x += (distortionNoise - 0.5) * _Distortion * 0.5;
                    distortedUV.y += (noise(float2(distortionNoise, v.uv.y)) - 0.5) * _Distortion * 0.5;
                }
                o.uv = TRANSFORM_TEX(distortedUV, _MainTex);
                
                // 顶点位置
                float4 vertexPos = v.vertex;
                
                // 2. 肢解效果
                if (_Disintegration > 0)
                {
                    // 基于顶点位置生成随机偏移
                    float vertexID = dot(v.vertex.xyz, float3(1, 1, 1));
                    float randomOffset = rand(float2(vertexID, 0));
                    
                    // 计算肢解偏移
                    float disintegrationFactor = saturate(_Disintegration);
                    float3 offsetDirection = normalize(_DisintegrationDirection.xyz + v.normal);
                    float timeOffset = _Time.y * _DisintegrationSpeed;
                    
                    // 应用肢解偏移
                    float3 disintegrationOffset = offsetDirection * 
                                                  _DisintegrationAmount * 
                                                  disintegrationFactor * 
                                                  (randomOffset + 0.5) * 
                                                  (sin(timeOffset + vertexID) * 0.5 + 0.5);
                    
                    vertexPos.xyz += disintegrationOffset;
                    
                    // 添加一些旋转
                    float angle = disintegrationFactor * _DisintegrationAmount * randomOffset;
                    float3x3 rotationMatrix = float3x3(
                        cos(angle), 0, sin(angle),
                        0, 1, 0,
                        -sin(angle), 0, cos(angle)
                    );
                    vertexPos.xyz = mul(rotationMatrix, vertexPos.xyz);
                }
                
                // 应用顶点变换
                o.vertex = UnityObjectToClipPos(vertexPos);
                o.worldPos = mul(unity_ObjectToWorld, vertexPos).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.color = v.color;
                
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // 采样纹理
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                
                // 3. 闪光效果
                if (_FlashIntensity > 0)
                {
                    // 创建闪烁效果
                    float flash = sin(_Time.y * _FlashSpeed) * 0.5 + 0.5;
                    float flashIntensity = _FlashIntensity * flash;
                    
                    // 添加闪光
                    col.rgb += _FlashColor.rgb * flashIntensity;
                    
                    // 提高整体亮度
                    col.rgb *= (1 + flashIntensity * 0.5);
                }
                
                // 边缘发光效果（根据肢解程度）
                if (_Disintegration > 0 && _EdgeWidth > 0)
                {
                    float edgeNoise = noise3D(i.worldPos * 10 + _Time.y);
                    float edge = saturate(_Disintegration - edgeNoise);
                    if (edge < _EdgeWidth)
                    {
                        float edgeFactor = 1 - (edge / _EdgeWidth);
                        col.rgb = lerp(col.rgb, _EdgeColor.rgb, edgeFactor);
                        col.rgb *= 1 + edgeFactor * 2;
                    }
                }
                
                // 添加光照
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float ndotl = max(0, dot(i.normal, lightDir));
                col.rgb *= ndotl * _LightColor0.rgb + unity_AmbientSky;
                
                // 应用雾效
                UNITY_APPLY_FOG(i.fogCoord, col);
                
                return col;
            }
            ENDCG
        }
    }
    
    Fallback "Diffuse"
}