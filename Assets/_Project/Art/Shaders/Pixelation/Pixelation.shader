Shader "Hidden/GameLimon/Pixelation"
{
    Properties
    {
        _PixelationSize ("Pixel Size", Float) = 4
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
            Name "Pixelation"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _PixelationSize;

            half4 Frag(Varyings input) : SV_Target
            {
                float2 screenSize = max(_ScreenParams.xy, float2(1.0, 1.0));
                float block = max(1.0, _PixelationSize);

                // Snap to the centre of the low-resolution block.
                float2 blockUv = floor(input.texcoord * screenSize / block) * block / screenSize;
                blockUv += (block * 0.5) / screenSize;
                blockUv = saturate(blockUv);

                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, blockUv);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
