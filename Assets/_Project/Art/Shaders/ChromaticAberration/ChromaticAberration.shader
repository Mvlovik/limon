Shader "Hidden/GameLimon/ChromaticAberration"
{
    Properties
    {
        _Intensity ("Intensity", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ChromaticAberration"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Intensity;

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 fromCenter = uv - 0.5;

                // Push the red and blue channels apart towards the screen edges.
                float2 offset = fromCenter * (_Intensity * 0.004);

                half red = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + offset).r;
                half4 center = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                half blue = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - offset).b;

                return half4(red, center.g, blue, center.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
