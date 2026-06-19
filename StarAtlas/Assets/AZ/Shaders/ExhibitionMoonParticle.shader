Shader "AZ/Exhibition Moon Particle"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _EdgeSoftness ("Edge Softness", Range(0.01, 0.5)) = 0.16
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_particles
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            fixed4 _Color;
            float _EdgeSoftness;

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.color = input.color * _Color;
                output.uv = input.uv;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 centeredUv = input.uv * 2.0 - 1.0;
                float radius = length(centeredUv);
                float alpha = 1.0 - smoothstep(
                    1.0 - _EdgeSoftness,
                    1.0,
                    radius);
                clip(alpha - 0.001);

                float sphereDepth = sqrt(saturate(1.0 - radius * radius));
                float3 normal = normalize(float3(centeredUv.x, -centeredUv.y, sphereDepth));
                float3 lightDirection = normalize(float3(-0.35, 0.45, 0.82));
                float lighting = 0.48 + 0.52 * saturate(dot(normal, lightDirection));

                fixed4 color = input.color;
                color.rgb *= lighting;
                color.a *= alpha;
                return color;
            }
            ENDCG
        }
    }
}
