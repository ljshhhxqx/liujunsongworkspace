Shader "UI/TransparentCircle"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Center ("Center", Vector) = (0, 0, 0, 0)   // 中心点（屏幕坐标）
        _Radius ("Radius", Range(0,1)) = 0.2            // 半径（相对屏幕比例）
        _Alpha ("Alpha", Range(0,1)) = 0.6             // 透明度
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 screenPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float2 _Center;
            float _Radius;
            float _Alpha;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 计算屏幕坐标
                float2 screenPos = i.screenPos.xy / i.screenPos.w;
                // 判断是否在圆内
                float distanceToCenter = distance(screenPos, _Center);
                float alpha = step(_Radius, distanceToCenter) * _Alpha; // 圆外半透明，圆内全透明
                return fixed4(0,0,0, alpha);
            }
            ENDCG
        }
    }
}