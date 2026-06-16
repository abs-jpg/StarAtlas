Shader "Unlit/Skybox"
{
     Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        // 【核心代码】：剔除正面，渲染背面
        Cull Front 

        CGPROGRAM
        // 基于物理的标准光照模型
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;

        struct Input
        {
            float2 uv_MainTex;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Alpha = c.a;
            
            // 【可选】：如果你想要光照法线也反向，取消下面这行的注释
            // o.Normal = -o.Normal; 
        }
        ENDCG
    }
    FallBack "Diffuse"
}
