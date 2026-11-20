Shader "GameLimon/GraffitiGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _Color ("Glow Color", Color) = (1, 0.03, 0.03, 1)
        _Intensity ("Core Intensity", Float) = 1
        _GlowIntensity ("Glow Intensity", Float) = 0.1
        _GlowRadius ("Glow Radius", Range(0, 0.1)) = 0.012
        _GlowSoftness ("Glow Softness", Range(0.1, 4)) = 0.9
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        // Additive: the black background contributes nothing, the red text and
        // its halo add light on top of the scene. No fog is applied, so the glow
        // stays visible through fog, like the beacon glow.
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "GraffitiGlow"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float _Intensity;
                float _GlowIntensity;
                float _GlowRadius;
                float _GlowSoftness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            half AlphaAt(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
            }

            half SampleHalo(float2 uv, float radius)
            {
                float2 axis = float2(radius, 0);
                float diagonal = radius * 0.70710678;

                half sum = 0;
                sum += AlphaAt(uv + axis);
                sum += AlphaAt(uv - axis);
                sum += AlphaAt(uv + float2(0, radius));
                sum += AlphaAt(uv - float2(0, radius));
                sum += AlphaAt(uv + float2(diagonal, diagonal));
                sum += AlphaAt(uv + float2(-diagonal, diagonal));
                sum += AlphaAt(uv + float2(diagonal, -diagonal));
                sum += AlphaAt(uv + float2(-diagonal, -diagonal));
                return sum * 0.125;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half core = tex.a * input.color.a;

                half inner = SampleHalo(input.uv, _GlowRadius);
                half middle = SampleHalo(input.uv, _GlowRadius * 2.5);
                half outer = SampleHalo(input.uv, _GlowRadius * 5.0);

                half glow = saturate(inner * 0.45 + middle * 0.35 + outer * 0.25);
                glow = pow(glow, _GlowSoftness);

                half3 color = _Color.rgb * (_Intensity * core + _GlowIntensity * glow);

                // Pure additive blend; alpha is not used by Blend One One.
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
