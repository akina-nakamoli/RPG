Shader "RPG/ForestFog"
{
    Properties
    {
        _Color ("Fog Color", Color) = (0.003, 0.006, 0.005, 1)
        _Radius ("Clear Radius", Float) = 4.5
        _Feather ("Soft Edge", Float) = 1.8
        _Player ("Player Position", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color, _Player;
            float _Radius, _Feather;
            struct v2f { float4 position : SV_POSITION; float2 world : TEXCOORD0; };
            v2f vert(appdata_base v)
            {
                v2f o; o.position = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xy; return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 offset = i.world - _Player.xy;
                float angle = atan2(offset.y, offset.x);
                float waves = .48 * sin(angle * 3 + .18 * _Time.y) + .32 * sin(angle * 5 - .13 * _Time.y) + .22 * cos(angle * 7);
                float wisps = .22 * sin(i.world.x * 1.7 + _Time.y * .23) * cos(i.world.y * 1.3 - _Time.y * .16);
                float radius = max(_Radius * .72, _Radius + waves + wisps);
                float opacity = smoothstep(radius, radius + max(.01, _Feather), length(offset * float2(1, 1.12)));
                return fixed4(_Color.rgb, _Color.a * opacity);
            }
            ENDCG
        }
    }
}
