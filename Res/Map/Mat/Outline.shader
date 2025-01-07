Shader "Custom/SoftOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1,1,0,1)
        _OutlineWidth ("Outline Width", Range (.002, 1)) = .005
        _Softness ("Softness", Range (0, 5)) = 0.5
        _DepthBias ("Depth Bias", Range(0, 10)) = 1.0
        _ViewScale ("View Scale", Range(0, 2)) = 1.0
        _RimPower ("Rim Power", Range(0.1, 10)) = 3.0
        _RimThreshold ("Rim Threshold", Range(0, 1)) = 0.1
    }
    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent"}
        
//        // 背面Pass
//        Pass
//        {
//            Name "OUTLINE_BACK"
//            Cull Front
//            ZWrite Off
//            ZTest Greater
//            Blend SrcAlpha OneMinusSrcAlpha
//
//            CGPROGRAM
//            #pragma vertex vert
//            #pragma fragment frag
//            #include "UnityCG.cginc"
//
//            struct appdata_t
//            {
//                float4 vertex : POSITION;
//                float3 normal : NORMAL;
//            };
//
//            struct v2f
//            {
//                float4 pos : POSITION;
//                float4 color : COLOR;
//                float edgeFactor : TEXCOORD0;
//                float4 screenPos : TEXCOORD1;
//                float3 viewDir : TEXCOORD2;
//                float3 worldNormal : TEXCOORD3;
//                float3 worldPos : TEXCOORD4;
//            };
//
//            uniform float4 _OutlineColor;
//            uniform float _OutlineWidth;
//            uniform float _Softness;
//            uniform float _DepthBias;
//            uniform float _ViewScale;
//            uniform float _RimPower;
//            uniform float _RimThreshold;
//
//            v2f vert(appdata_t v)
//            {
//                v2f o;
//                
//                // 计算世界空间位置和法线
//                float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
//                float3 worldNormal = UnityObjectToWorldNormal(v.normal);
//                worldNormal = normalize(worldNormal);// 计算视角方向
//                float3 viewDir = normalize(WorldSpaceViewDir(v.vertex));
//                
//                // 计算视角因子
//                float viewFactor = abs(dot(viewDir, worldNormal));
//                viewFactor = lerp(1.0, viewFactor, _ViewScale);
//                
//                // 应用轮廓线偏移
//                worldPos.xyz += worldNormal * _OutlineWidth * viewFactor * _DepthBias * 0.5;
//                
//                // 输出计算结果
//                o.pos = UnityWorldToClipPos(worldPos);
//                o.color = _OutlineColor;
//                o.viewDir = viewDir;
//                o.worldNormal = worldNormal;
//                o.worldPos = worldPos.xyz;
//                o.screenPos = ComputeScreenPos(o.pos);
//                o.edgeFactor = saturate(viewFactor);
//                
//                return o;
//            }
//
//            half4 frag(v2f i) : COLOR
//            {
//                // 重新计算视角方向
//                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
//                
//                // 计算边缘光
//                float rim = 1.0 - abs(dot(viewDir, i.worldNormal));
//                rim = pow(rim, _RimPower);
//                
//                // 应用阈值和软化效果
//                float softness = _Softness * (1.0 + i.edgeFactor);
//                rim = smoothstep(_RimThreshold, 1.0, rim);
//                
//                // 计算最终透明度
//                half alpha = rim * pow(i.edgeFactor, softness * 3.0);
//                alpha = saturate(alpha);
//                
//                return half4(i.color.rgb, i.color.a * alpha);
//            }
//            ENDCG
//        }
        
        // 正面Pass
        Pass
        {
            Name "OUTLINE_FRONT"
            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : POSITION;
                float4 color : COLOR;
                float edgeFactor : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
                float3 worldNormal : TEXCOORD3;
                float3 worldPos : TEXCOORD4;
            };

            uniform float4 _OutlineColor;
            uniform float _OutlineWidth;
            uniform float _Softness;
            uniform float _DepthBias;
            uniform float _ViewScale;
            uniform float _RimPower;
            uniform float _RimThreshold;

            v2f vert(appdata_t v)
            {
                v2f o;
                
                // 计算世界空间位置和法线
                float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
                float3 worldNormal = UnityObjectToWorldNormal(v.normal);
                worldNormal = normalize(worldNormal);
                
                // 计算视角方向
                float3 viewDir = normalize(WorldSpaceViewDir(v.vertex));
                
                // 计算视角因子
                float viewFactor = abs(dot(viewDir, worldNormal));
                viewFactor = lerp(1.0, viewFactor, _ViewScale);
                
                // 应用轮廓线偏移
                worldPos.xyz += worldNormal * _OutlineWidth * viewFactor * _DepthBias * 0.5;
                
                // 输出计算结果
                o.pos = UnityWorldToClipPos(worldPos);
                o.color = _OutlineColor;
                o.viewDir = viewDir;
                o.worldNormal = worldNormal;
                o.worldPos = worldPos.xyz;
                o.screenPos = ComputeScreenPos(o.pos);
                o.edgeFactor = saturate(viewFactor);
                
                return o;
            }

            half4 frag(v2f i) : COLOR
            {
                // 重新计算视角方向
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                
                // 计算边缘光
                float rim = 1.0 - abs(dot(viewDir, i.worldNormal));
                rim = pow(rim, _RimPower);
                
                // 应用阈值和软化效果
                float softness = _Softness * (1.0 + i.edgeFactor);
                rim = smoothstep(_RimThreshold, 1.0, rim);
                
                // 计算最终透明度
                half alpha = rim * pow(i.edgeFactor, softness * 3.0);
                alpha = saturate(alpha);
                
                return half4(i.color.rgb, i.color.a * alpha);
            }
            ENDCG
        }
    }
    FallBack "Transparent/Diffuse"
}
