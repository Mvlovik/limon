Shader "GameLimon/Skybox/NightClouds"
{
    Properties
    {
        _ZenithColor ("Zenith Color", Color) = (0.012, 0.018, 0.045, 1)
        _HorizonColor ("Horizon Color", Color) = (0.055, 0.07, 0.12, 1)
        _GroundColor ("Ground Color", Color) = (0.008, 0.01, 0.02, 1)
        _CloudColor ("Cloud Color", Color) = (0.09, 0.1, 0.14, 1)
        _CloudScale ("Cloud Scale", Float) = 2.8
        _CloudCoverage ("Cloud Coverage", Range(0, 1)) = 0.42
        _CloudOpacity ("Cloud Opacity", Range(0, 1)) = 0.9
        _CloudSpeed ("Cloud Speed", Float) = 0.008
        _MoonColor ("Moon Color", Color) = (0.8, 0.86, 1, 1)
        _MoonDirection ("Moon Direction", Vector) = (0.38, 0.41, 0.82, 0)
        _MoonSize ("Moon Size", Range(0.002, 0.1)) = 0.028
        _StarDensity ("Star Density", Range(0, 1)) = 0.9965
        _StarBrightness ("Star Brightness", Float) = 1.2
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            float4 _ZenithColor;
            float4 _HorizonColor;
            float4 _GroundColor;
            float4 _CloudColor;
            float4 _MoonColor;
            float4 _MoonDirection;
            float _CloudScale;
            float _CloudCoverage;
            float _CloudOpacity;
            float _CloudSpeed;
            float _MoonSize;
            float _StarDensity;
            float _StarBrightness;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float noise3(float3 p)
            {
                float3 cell = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = hash13(cell + float3(0, 0, 0));
                float n100 = hash13(cell + float3(1, 0, 0));
                float n010 = hash13(cell + float3(0, 1, 0));
                float n110 = hash13(cell + float3(1, 1, 0));
                float n001 = hash13(cell + float3(0, 0, 1));
                float n101 = hash13(cell + float3(1, 0, 1));
                float n011 = hash13(cell + float3(0, 1, 1));
                float n111 = hash13(cell + float3(1, 1, 1));

                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);

                float nxy0 = lerp(nx00, nx10, f.y);
                float nxy1 = lerp(nx01, nx11, f.y);

                return lerp(nxy0, nxy1, f.z);
            }

            float fbm(float3 p)
            {
                float value = 0.0;
                float amplitude = 0.5;

                for (int i = 0; i < 4; i++)
                {
                    value += amplitude * noise3(p);
                    p *= 2.03;
                    amplitude *= 0.5;
                }

                return value;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float height = saturate(d.y);

                float3 sky = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(height, 0.55));
                sky = lerp(_GroundColor.rgb, sky, smoothstep(-0.12, 0.02, d.y));

                // Stars
                float3 starPos = d * 420.0;
                float3 starCell = floor(starPos);
                float starHash = hash13(starCell);
                float3 starLocal = frac(starPos) - 0.5;
                float starShape = pow(saturate(1.0 - length(starLocal) * 2.2), 12.0);
                float stars = step(_StarDensity, starHash) * starShape * saturate(d.y * 3.0);
                sky += starShape * stars * _StarBrightness;

                // Moon
                float3 moonDir = normalize(_MoonDirection.xyz);
                float moonDot = dot(d, moonDir);
                float moonDisc = smoothstep(cos(_MoonSize), cos(_MoonSize * 0.75), moonDot);
                float moonGlow = pow(saturate(moonDot), 400.0) * 0.45;
                sky += _MoonColor.rgb * (moonDisc + moonGlow);

                // Clouds
                float3 cloudPos = d * _CloudScale + float3(_Time.y * _CloudSpeed, 0.0, _Time.y * _CloudSpeed * 0.5);
                float cloudNoise = fbm(cloudPos);
                float cloudCover = smoothstep(_CloudCoverage, _CloudCoverage + 0.28, cloudNoise);
                float horizonFade = smoothstep(0.0, 0.18, d.y);
                float clouds = cloudCover * horizonFade;

                float3 cloudTint = _CloudColor.rgb + _MoonColor.rgb * moonGlow * 0.6;
                float3 color = lerp(sky, cloudTint, clouds * _CloudOpacity);

                return fixed4(color, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
