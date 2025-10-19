Shader "Custom/EarthOcean"
{
    Properties
    {
        _OceanCol("Ocean Color", 2D) = "white" {}
        _Noise ("Noise (R for Height)", 2D) = "white" {}

        _WaveNormalScale ("Wave Normal Scale", Float) = 0.0002524171
        _WaveStrength ("Wave Strength", Range(0, 1)) = 1
        _WaveSpeed ("Wave Speed", Float) = 2
        [NoScaleOffset] _WaveNormalA ("Wave Normal A", 2D) = "bump" {}
        [NoScaleOffset] _WaveNormalB ("Wave Normal B", 2D) = "bump" {}

        [Header(Displacement)]
        _Refraction ("Refraction", Float) = 15

        [Header(Lighting)]
        _ShadowColor("Shadow Color", Color) = (0.05, 0.1, 0.2, 1.0)
        _SpecularSmoothness ("Specular Smoothness", Range(0,1)) = 0.1
        _SpecularStrength("Specular Strength", Float) = 1
        _FresnelCol("Fresnel Col", Color) = (0,0,0,0)
        _FresnelWeight("Fresnel Weight", Float) = 1
        _FresnelPower("Fresnel Power", Float) = 5
        
        [Header(Crest Foam)]
        _FoamColor ("Foam Color", Color) = (1,1,1,1)
        _CrestFoamStrength("Crest Foam Strength", Range(0,2)) = 1.0
        _CrestFoamThreshold("Crest Foam Threshold", Range(0,1)) = 0.8
        // _FoamNoiseScale removed
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType"="Transparent" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            #include "/GeoMath.hlsl"
            #include "/Includes/Triplanar.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float2 uv           : TEXCOORD0;
                float4 positionCS   : SV_POSITION;
                float3 normalWS     : NORMAL;
                float3 positionWS   : TEXCOORD1;
                float3 positionOS   : TEXCOORD2;
                float4 shadowCoord  : TEXCOORD3;
            };

            TEXTURE2D(_OceanCol);       SAMPLER(sampler_OceanCol);
            TEXTURE2D(_Noise);          SAMPLER(sampler_Noise);
            TEXTURE2D(_WaveNormalA);    SAMPLER(sampler_WaveNormalA);
            TEXTURE2D(_WaveNormalB);    SAMPLER(sampler_WaveNormalB);

            CBUFFER_START(UnityPerMaterial)
                float4 _OceanCol_TexelSize;
                half4 _ShadowColor;
                half _SpecularSmoothness;
                half _WaveNormalScale, _WaveStrength, _WaveSpeed;
                half _Refraction;
                half _SpecularStrength;
                half4 _FresnelCol;
                half _FresnelWeight, _FresnelPower;
                half4 _FoamColor;
                half _CrestFoamStrength, _CrestFoamThreshold;
                // _FoamNoiseScale removed
            CBUFFER_END

            half3 calculateWaveNormals(float3 pos, half3 sphereNormal, out half3 tang) 
            {
                half noise = triplanar(_Noise, sampler_Noise, pos, sphereNormal, 0.15).r;
    
                half waveSpeed = 0.35 * _WaveSpeed;
                half2 waveOffsetA = _Time.xx * waveSpeed * half2(1.0, 0.8);
                half2 waveOffsetB = _Time.xx * waveSpeed * half2(-0.8, -0.5);

                float3 scaleA = float3(_WaveNormalScale, _WaveNormalScale, _WaveNormalScale);
                float3 scaleB = scaleA * 0.9;
                float3 scaleC = scaleA * 1.25;

                half3 waveA = triplanarNormal(_WaveNormalA, sampler_WaveNormalA, pos, sphereNormal, scaleA, waveOffsetA, _WaveStrength);
                half3 waveB = triplanarNormal(_WaveNormalA, sampler_WaveNormalA, pos, sphereNormal, scaleB, waveOffsetA + half2(0.3,0.7), _WaveStrength);
                half3 waveNormal = triplanarNormal(_WaveNormalB, sampler_WaveNormalB, pos, lerp(waveA, waveB, noise), scaleC, waveOffsetB, _WaveStrength, tang);

                return waveNormal;
            }

            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(v.normalOS);
                
                o.positionCS = positionInputs.positionCS;
                o.positionWS = positionInputs.positionWS;
                o.normalWS = normalInputs.normalWS;
                o.uv = v.uv;
                o.positionOS = v.positionOS.xyz;
                
                o.shadowCoord = GetShadowCoord(positionInputs);
                return o;
            }
            
            half calculateSpecular(half3 normal, half3 viewDir, half3 lightDir, half smoothness) 
            {
                half3 halfDir = normalize(lightDir + viewDir);
                half gloss = exp2(10 * smoothness + 1);
                half spec = pow(saturate(dot(normal, halfDir)), gloss);
                return spec;
            }

            half4 frag (Varyings i) : SV_Target
            {
                Light mainLight = GetMainLight(i.shadowCoord);
                half3 lightDir = mainLight.direction;
                half shadows = mainLight.shadowAttenuation;
                
                float3 pointOnUnitSphere_obj = normalize(i.positionOS);
                half3 sphereNormal_obj = (half3)pointOnUnitSphere_obj;
                
                half3 worldViewDir = SafeNormalize(_WorldSpaceCameraPos.xyz - i.positionWS);
                half heightNoise = triplanar(_Noise, sampler_Noise, i.positionOS, sphereNormal_obj, 0.5).r;

                half3 tang;
                half3 waveNormal_obj = calculateWaveNormals(i.positionOS, sphereNormal_obj, tang);
                half3 waveNormal = TransformObjectToWorldNormal(waveNormal_obj);
                half3 sphereNormal = i.normalWS;

                half2 refractionOffset = tang.xy * 0.0005 * _Refraction;
                half2 texCoord = pointToUV(pointOnUnitSphere_obj);
                half2 oceanTexCoord = texCoord + refractionOffset;
                
                half mipLevel = calculateGeoMipLevel(texCoord, _OceanCol_TexelSize.zw);
                half3 albedo = SAMPLE_TEXTURE2D_LOD(_OceanCol, sampler_OceanCol, oceanTexCoord, mipLevel).rgb;

                half specularIntensity = calculateSpecular(waveNormal, worldViewDir, lightDir, _SpecularSmoothness);
                specularIntensity *= heightNoise;
                half3 specularColor = specularIntensity * _SpecularStrength * mainLight.color;

                half NdotL_sphere = dot(sphereNormal, lightDir);
                half diffuseShading = NdotL_sphere * 0.5 + 0.5;
                diffuseShading *= diffuseShading;
                
                half ripple = saturate(smoothstep(-0.53, 0.54, dot(waveNormal, -worldViewDir)));
                albedo += ripple * 0.15;

                half3 litColor = albedo * diffuseShading + specularColor;
                half3 shadowedColor = albedo * _ShadowColor.rgb; 

                half foamNoise = triplanar(_Noise, sampler_Noise, i.positionOS, sphereNormal_obj, 1.5, 0).r;
                half crestDot = saturate(dot(waveNormal, sphereNormal));
                half noisyThreshold = _CrestFoamThreshold - (foamNoise * 0.2 - 0.1);
                half crestFoam = smoothstep(noisyThreshold, noisyThreshold - 0.2, crestDot);
                crestFoam *= _CrestFoamStrength;
                half foamBlendAmount = crestFoam * _FoamColor.a;

                litColor = lerp(litColor, _FoamColor.rgb, foamBlendAmount);
                shadowedColor = lerp(shadowedColor, _FoamColor.rgb, foamBlendAmount);

                half nightT = saturate(dot(sphereNormal, -lightDir));
                shadows = lerp(shadows, 0, smoothstep(0.2, 0.3, nightT));
                
                half3 oceanCol = lerp(shadowedColor, litColor, shadows);
                
                half fresnel = saturate(_FresnelWeight * pow(saturate(1 + dot(-worldViewDir, sphereNormal)), _FresnelPower));
                oceanCol += fresnel * _FresnelCol.rgb;
                
                return half4(oceanCol, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Lit"
}