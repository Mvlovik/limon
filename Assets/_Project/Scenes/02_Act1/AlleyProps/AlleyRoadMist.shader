Shader "GameLimon/Alley Road Mist"
{
    Properties
    {
        _MistColor ("Mist Color", Color) = (0.115, 0.135, 0.166, 1)
        _Density ("Density", Range(0, 1)) = 0.075
        _ClearBounds ("Clear Area (Min X, Min Z, Max X, Max Z)", Vector) = (-29.2, -87, 14, -14)
        _Feather ("Edge Width (West, South, East, North)", Vector) = (8, 12, 13, 10)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-20" "RenderType" = "Transparent" "DisableBatching" = "True" }
        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _MistColor;
                float _Density;
                float4 _ClearBounds;
                float4 _Feather;
            CBUFFER_END

            float4 Vert(float4 positionOS : POSITION) : SV_POSITION
            {
                // The scene mesh supplies conservative culling bounds. Its single triangle
                // is submitted directly in clip space to cover every camera pixel.
                return float4(positionOS.xy, 0.5, 1.0);
            }

            float LengthInside(float2 origin, float2 inverseDirection, float distance, float2 low, float2 high)
            {
                float2 t0 = (low - origin) * inverseDirection;
                float2 t1 = (high - origin) * inverseDirection;
                float2 nearPlane = min(t0, t1);
                float2 farPlane = max(t0, t1);
                float entry = max(0.0, max(nearPlane.x, nearPlane.y));
                float exit = min(distance, min(farPlane.x, farPlane.y));
                return max(0.0, exit - entry);
            }

            half4 Frag(float4 positionCS : SV_POSITION) : SV_Target
            {
                // Without scene depth, leave the scene's built-in distance fog in place.
                clip(min(_CameraDepthTexture_TexelSize.z, _CameraDepthTexture_TexelSize.w) - 4.0);

                float2 uv = GetNormalizedScreenSpaceUV(positionCS);
                float depth = SampleSceneDepth(uv);
                // Background pixels have no world surface. Keep the night haze uniform
                // above the playable area instead of projecting its clear footprint upward.
                if (depth == UNITY_RAW_FAR_CLIP_VALUE)
                    return half4(_MistColor.rgb, 1.0);

                #if !UNITY_REVERSED_Z
                    depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, depth);
                #endif

                float3 origin = GetCameraPositionWS();
                if (unity_OrthoParams.w > 0.5)
                    origin = ComputeWorldSpacePosition(uv, UNITY_NEAR_CLIP_VALUE, UNITY_MATRIX_I_VP);

                float3 endpoint = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                float3 travel = endpoint - origin;
                float lengthWS = length(travel);
                float distance = min(lengthWS, 320.0);
                float2 direction = travel.xz / max(lengthWS, 0.001);
                float2 inverseDirection = lerp(-1.0, 1.0, step(0.0, direction)) / max(abs(direction), 0.00001);

                // Averaging nested clear rectangles softens the perimeter while keeping
                // a uniform density once the ray leaves the transition.
                float clearLength = 0.0;
                const int layers = 6;
                UNITY_UNROLL
                for (int layer = 0; layer < layers; layer++)
                {
                    float edge = (layer + 0.5) / layers;
                    float2 low = _ClearBounds.xy - _Feather.xy * edge;
                    float2 high = _ClearBounds.zw + _Feather.zw * edge;
                    clearLength += LengthInside(origin.xz, inverseDirection, distance, low, high);
                }
                float fogLength = max(0.0, distance - clearLength / layers);
                return half4(_MistColor.rgb, 1.0 - exp(-_Density * fogLength));
            }
            ENDHLSL
        }
    }
}

