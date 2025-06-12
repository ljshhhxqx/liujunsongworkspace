Shader "Custom/CharacterStatusEffects"
{
    Properties
    {
        // 基础属性
        [Header(Base Properties)]
        _MainTex ("Base Texture", 2D) = "white" {}
        _Color ("Base Color", Color) = (1,1,1,1)
        _AlphaCutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        
        // 透明效果
        [Header(Transparency)]
        _Transparency ("Transparency", Range(0,1)) = 0
        _FresnelPower ("Fresnel Power", Range(0,10)) = 5
        _FresnelColor ("Fresnel Color", Color) = (1,1,1,0.5)
        
        // 冰冻效果
        [Header(Ice Effect)]
        _IceAmount ("Ice Amount", Range(0,1)) = 0
        _IceColor ("Ice Color", Color) = (0.5,0.8,1,0.5)
        _IceNoise ("Ice Noise", 2D) = "white" {}
        _IceThickness ("Ice Thickness", Range(0,0.2)) = 0.05
        _IceSmoothness ("Ice Smoothness", Range(0,1)) = 0.9
        
        // 雪/减速效果
        [Header(Snow Effect)]
        _SnowAmount ("Snow Amount", Range(0,1)) = 0
        _SnowColor ("Snow Color", Color) = (1,1,1,0.8)
        _SnowDirection ("Snow Direction", Vector) = (0,1,0)
        _SnowCoverage ("Snow Coverage", Range(0,1)) = 0.5
        _SnowRoughness ("Snow Roughness", Range(0,1)) = 0.7
        
        // 石化效果
        [Header(Petrify Effect)]
        _StoneAmount ("Stone Amount", Range(0,1)) = 0
        _StoneColor ("Stone Color", Color) = (0.3,0.3,0.3,1)
        _StoneDetail ("Stone Detail", 2D) = "gray" {}
        _CrackAmount ("Crack Amount", Range(0,1)) = 0.5
        _CrackColor ("Crack Color", Color) = (0.1,0.1,0.1,1)
    }

    SubShader
    {
        Tags { 
            "Queue"="Transparent" 
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }
        LOD 300
        
        // 透明渲染通道
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                float3 viewDir : TEXCOORD3;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            // 基础属性
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _AlphaCutoff;
            
            // 透明效果
            float _Transparency;
            float _FresnelPower;
            float4 _FresnelColor;
            
            // 冰冻效果
            float _IceAmount;
            float4 _IceColor;
            sampler2D _IceNoise;
            float4 _IceNoise_ST;
            float _IceThickness;
            float _IceSmoothness;
            
            // 雪效果
            float _SnowAmount;
            float4 _SnowColor;
            float3 _SnowDirection;
            float _SnowCoverage;
            float _SnowRoughness;
            
            // 石化效果
            float _StoneAmount;
            float4 _StoneColor;
            sampler2D _StoneDetail;
            float4 _StoneDetail_ST;
            float _CrackAmount;
            float4 _CrackColor;
            
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                
                // 轻微膨胀效果（用于冰冻等效果）
                float iceExpand = _IceAmount * _IceThickness;
                v.vertex.xyz += v.normal * iceExpand;
                
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = normalize(_WorldSpaceCameraPos - o.worldPos);
                return o;
            }
            
            // 修复的伪随机数生成函数
            float GenerateRandomPattern(float2 uv)
            {
                // 使用点积创建伪随机数
                float random = dot(uv, float2(12.9898, 78.233));
                // 使用正弦函数创建变化
                random = sin(random);
                // 取小数部分确保在0-1范围
                random = frac(random);
                return random;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // 基础颜色和透明度
                fixed4 baseColor = tex2D(_MainTex, i.uv) * _Color;
                float alpha = baseColor.a;
                
                // 透明度处理
                clip(alpha - _AlphaCutoff);
                
                // 菲涅尔效果（用于透明效果）
                float fresnel = 1.0 - saturate(dot(i.viewDir, normalize(i.worldNormal)));
                fresnel = pow(fresnel, _FresnelPower);
                float4 fresnelColor = _FresnelColor * fresnel;
                
                // 雪效果（基于法线方向）
                float snowDot = dot(normalize(i.worldNormal), normalize(_SnowDirection));
                float snowMask = saturate(snowDot * _SnowAmount * 2);
                snowMask = step(1.0 - _SnowCoverage, snowMask);
                fixed4 snowColor = _SnowColor * snowMask;
                
                // 冰冻效果（噪声纹理+颜色混合）
                float2 iceUV = i.worldPos.xz * _IceNoise_ST.xy + _IceNoise_ST.zw * _Time.y;
                float iceNoise = tex2D(_IceNoise, iceUV).r;
                fixed4 iceColor = _IceColor * saturate(_IceAmount * (1.2 + iceNoise * 0.8));
                
                // 石化效果（细节纹理+颜色覆盖）
                fixed4 stoneDetail = tex2D(_StoneDetail, i.uv * _StoneDetail_ST.xy);
                fixed4 stoneColor = lerp(baseColor, _StoneColor * stoneDetail, _StoneAmount);
                
                // 裂缝效果 - 使用修复的随机函数
                float crackPattern = GenerateRandomPattern(i.uv * 50.0);
                crackPattern = step(0.95, crackPattern) * _CrackAmount * _StoneAmount;
                fixed4 crackColor = _CrackColor * crackPattern;
                
                // 最终颜色混合
                fixed4 finalColor = baseColor;
                
                // 效果叠加优先级（石化 > 冰冻 > 雪）
                finalColor.rgb = lerp(finalColor.rgb, stoneColor.rgb, _StoneAmount);
                finalColor.rgb = lerp(finalColor.rgb, finalColor.rgb * 0.7 + iceColor.rgb, _IceAmount);
                finalColor.rgb = lerp(finalColor.rgb, finalColor.rgb * 0.8 + snowColor.rgb, snowMask);
                
                // 添加裂缝
                finalColor.rgb = lerp(finalColor.rgb, crackColor.rgb, crackPattern);
                
                // 添加菲涅尔效果
                finalColor.rgb += fresnelColor.rgb * _Transparency;
                
                // 透明度混合
                float finalAlpha = lerp(alpha, 1.0 - _Transparency * fresnel, _Transparency);
                finalAlpha = saturate(finalAlpha);
                
                // 平滑度控制（冰增加光滑度，雪增加粗糙度）
                float smoothness = lerp(0.3, _IceSmoothness, _IceAmount);
                smoothness = lerp(smoothness, _SnowRoughness, snowMask);
                
                // 返回最终颜色
                return fixed4(finalColor.rgb, finalAlpha);
            }
            ENDCG
        }
        
        // 阴影投射通道（确保透明物体能投射阴影）
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"
            
            struct v2f_shadow {
                V2F_SHADOW_CASTER;
                float2 uv : TEXCOORD1;
            };
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _AlphaCutoff;
            
            v2f_shadow vert (appdata_base v)
            {
                v2f_shadow o;
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }
            
            float4 frag (v2f_shadow i) : SV_Target
            {
                fixed4 texColor = tex2D(_MainTex, i.uv);
                clip(texColor.a - _AlphaCutoff);
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
    FallBack "Transparent/Cutout/Diffuse"
}