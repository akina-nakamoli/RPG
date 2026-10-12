Shader "My/LightCone"
{
    Properties
    {
        _Color ("Light Color", Color) = (0.3, 0.7, 1, 1)
        _Intensity ("Intensity", Range(0, 3)) = 1
        _EdgeSoftness ("Edge Softness", Range(0.1, 5)) = 2
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            float4 _Color;
            float _Intensity;
            float _EdgeSoftness;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 越靠近光源越亮，越靠近末端越暗
                float fade = 1.0 - i.uv.y;

                fade = pow(fade, _EdgeSoftness);

                fixed4 col = _Color;

                col.rgb *= _Intensity;
                col.a *= fade;

                return col;
            }

            ENDCG
        }
    }
}