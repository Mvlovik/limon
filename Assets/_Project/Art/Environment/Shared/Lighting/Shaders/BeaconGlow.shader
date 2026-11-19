Shader "GameLimon/BeaconGlow"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 0.02, 0.02, 1)
        _Intensity ("Intensity", Float) = 3
        _CoreSize ("Core Size", Range(0.01, 1)) = 0.16
        _Softness ("Softness", Range(0, 1)) = 0.65
        _Alpha ("Alpha", Range(0, 1)) = 0.9
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "BeaconGlow"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Intensity;
                float _CoreSize;
                float _Softness;
                float _Alpha;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                float4x4 objectToWorld = GetObjectToWorldMatrix();
                float3 center = objectToWorld._m03_m13_m23;
                float scaleX = length(objectToWorld._m00_m10_m20);
                float scaleY = length(objectToWorld._m01_m11_m21);

                // Billboard the quad so the halo always faces the camera.
                float3 cameraRight = normalize(UNITY_MATRIX_I_V._m00_m10_m20);
                float3 cameraUp = normalize(UNITY_MATRIX_I_V._m01_m11_m21);
                float3 positionWS = center
                    + cameraRight * input.positionOS.x * scaleX
                    + cameraUp * input.positionOS.y * scaleY;

                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                float distanceFromCenter = length(p);

                float falloff = pow(saturate(1.0 - distanceFromCenter), lerp(4.0, 1.0, _Softness));
                float core = saturate(1.0 - smoothstep(0.0, max(0.001, _CoreSize), distanceFromCenter));

                half3 color = _Color.rgb * _Intensity * (falloff + core);
                half alpha = saturate(falloff * _Alpha);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
