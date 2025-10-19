Shader "Custom/EarthTerrain"
{
    // Properties block with the shadow parameters removed
    Properties
    {
        [Header(Color Texture Tiles 4x2 Grid)]
        _ColA1("Colour A1 (Top-Left)", 2D) = "white" {}
        _ColB1("Colour B1", 2D) = "white" {}
        _ColC1("Colour C1", 2D) = "white" {}
        _ColD1("Colour D1 (Top-Right)", 2D) = "white" {}
        _ColA2("Colour A2 (Bottom-Left)", 2D) = "white" {}
        _ColB2("Colour B2", 2D) = "white" {}
        _ColC2("Colour C2", 2D) = "white" {}
        _ColD2("Colour D2 (Bottom-Right)", 2D) = "white" {}
        _NormalMapWest("Normal Map West", 2D) = "white" {}
        _NormalMapEast("Normal Map East", 2D) = "white" {}
        _LightMap("Light Map", 2D) = "white" {}
        _LakeMask("Lake Mask", 2D) = "white" {}
        [Header(Lighting)]
        _NightColor("Night Color", Color) = (0.05, 0.05, 0.1, 1) 
        _FresnelCol("Fresnel Col", Color) = (0,0,0,0)
        _Contrast ("Contrast", Float) = 1
        _BrightnessAdd("Brightness Add", Float) = 0
        _BrightnessMul("Brightness Mul", Float) = 1
        [Header(City Lights)]
        _CityLightColor("City Light Color", Color) = (1, 0.85, 0.7, 1)
        _CityLightPower("City Light Power", Range(1, 5)) = 2.0
        _TerminatorGlowBoost("Terminator Glow Boost", Range(0, 5)) = 1.5
        [Header(Lakes)]
        _LakeShadowColor("Lake Shadow Color", Color) = (0.04, 0.06, 0.1, 1)
        [NoScaleOffset] _WaveNormalA ("Wave Normal A", 2D) = "bump" {}
        _WaveNormalScale ("Wave Normal Scale", Float) = 1
        _WaveStrength ("Wave Strength", Range(0, 1)) = 1
        _LakeCol("Lake Color", Color) = (0.08, 0.30, 0.45, 1)
        _LakeParams("Lake Params", Vector) = (0.1, 1, 0.5, 0.2) // x = Gloss, y = Spec Strength, z = Tint, w = Fresnel
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "RenderType" = "Opaque" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            #include "/GeoMath.hlsl"
            #include "/Includes/Triplanar.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
                float3 normalWS     : NORMAL;
                float3 positionOS   : TEXCOORD2;
                float3 normalOS     : TEXCOORD3;
                float4 shadowCoord  : TEXCOORD4;
            };

            TEXTURE2D(_ColA1);      SAMPLER(sampler_ColA1);
            TEXTURE2D(_ColB1);      SAMPLER(sampler_ColB1);
            TEXTURE2D(_ColC1);      SAMPLER(sampler_ColC1);
            TEXTURE2D(_ColD1);      SAMPLER(sampler_ColD1);
            TEXTURE2D(_ColA2);      SAMPLER(sampler_ColA2);
            TEXTURE2D(_ColB2);      SAMPLER(sampler_ColB2);
            TEXTURE2D(_ColC2);      SAMPLER(sampler_ColC2);
            TEXTURE2D(_ColD2);      SAMPLER(sampler_ColD2);
            TEXTURE2D(_NormalMapWest); SAMPLER(sampler_NormalMapWest);
            TEXTURE2D(_NormalMapEast); SAMPLER(sampler_NormalMapEast);
            TEXTURE2D(_LightMap);   SAMPLER(sampler_LightMap);
            TEXTURE2D(_LakeMask);   SAMPLER(sampler_LakeMask);
            TEXTURE2D(_WaveNormalA); SAMPLER(sampler_WaveNormalA);

            CBUFFER_START(UnityPerMaterial)
                float4 _ColA1_TexelSize;
                float _WaveNormalScale, _WaveStrength;
                float _BrightnessAdd, _BrightnessMul, _Contrast;
                float4 _NightColor, _CityLightColor, _FresnelCol; 
                float _CityLightPower, _TerminatorGlowBoost;
                float4 _LakeParams;
                float4 _LakeCol, _LakeShadowColor;
            CBUFFER_END
            
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = positionInputs.positionCS;
                OUT.positionWS = positionInputs.positionWS;
                OUT.normalWS = normalInputs.normalWS;
                OUT.uv = IN.uv;
                OUT.positionOS = IN.positionOS.xyz;
                OUT.normalOS = IN.normalOS;
                
                OUT.shadowCoord = GetShadowCoord(positionInputs);
                
                return OUT;
            }

            float calculateSpecular(float3 normal, float3 viewDir, float3 dirToSun, float shininess)
            {
                float3 halfVector = normalize(dirToSun + viewDir);
                float spec = pow(saturate(dot(normal, halfVector)), shininess);
                return spec;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float3 objRadial = normalize(i.positionOS);
                float2 texCoord = pointToUV(objRadial);
                float lakeMask = SAMPLE_TEXTURE2D(_LakeMask, sampler_LakeMask, texCoord).r;

                float3 landAlbedo;
                if (texCoord.y >= 0.5) { // Top Row
                    if (texCoord.x < 0.25) { // A1
                        float2 tileTexCoord = float2(texCoord.x * 4.0, (texCoord.y - 0.5) * 2.0);
                        float mipLevel = calculateGeoMipLevel(tileTexCoord, _ColA1_TexelSize.zw);
                        landAlbedo = SAMPLE_TEXTURE2D_LOD(_ColA1, sampler_ColA1, tileTexCoord, mipLevel).rgb;
                    } else if (texCoord.x < 0.5) { // B1
                        float2 tileTexCoord = float2((texCoord.x - 0.25) * 4.0, (texCoord.y - 0.5) * 2.0);
                        float mipLevel = calculateGeoMipLevel(tileTexCoord, _ColA1_TexelSize.zw);
                        landAlbedo = SAMPLE_TEXTURE2D_LOD(_ColB1, sampler_ColB1, tileTexCoord, mipLevel).rgb;
                    } else if (texCoord.x < 0.75) { // C1
                        float2 tileTexCoord = float2((texCoord.x - 0.5) * 4.0, (texCoord.y - 0.5) * 2.0);
                        float mipLevel = calculateGeoMipLevel(tileTexCoord, _ColA1_TexelSize.zw);
                        landAlbedo = SAMPLE_TEXTURE2D_LOD(_ColC1, sampler_ColC1, tileTexCoord, mipLevel).rgb;
                    } else { // D1
                        float2 tileTexCoord = float2((texCoord.x - 0.75) * 4.0, (texCoord.y - 0.5) * 2.0);
                        float mipLevel = calculateGeoMipLevel(tileTexCoord, _ColA1_TexelSize.zw);
                        landAlbedo = SAMPLE_TEXTURE2D_LOD(_ColD1, sampler_ColD1, tileTexCoord, mipLevel).rgb;
                    }
                } else { // Bottom Row
                    if (texCoord.x < 0.25) { // A2
                        float2 tileTexCoord = float2(texCoord.x * 4.0, texCoord.y * 2.0);
                        float mipLevel = calculateGeoMipLevel(tileTexCoord, _ColA1_TexelSize.zw);
                        landAlbedo = SAMPLE_TEXTURE2D_LOD(_ColA2, sampler_ColA2, tileTexCoord, mipLevel).rgb;
                    } else if (texCoord.x < 0.5) { // B2
                        float2 tileTexCoord = float2((texCoord.x - 0.25) * 4.0, texCoord.y * 2.0);
                        float mipLevel = calculateGeoMipLevel(tileTexCoord, _ColA1_TexelSize.zw);
                        landAlbedo = SAMPLE_TEXTURE2D_LOD(_ColB2, sampler_ColB2, tileTexCoord, mipLevel).rgb;
                    } else if (texCoord.x < 0.75) { // C2
                        float2 tileTexCoord = float2((texCoord.x - 0.5) * 4.0, texCoord.y * 2.0);
                        float mipLevel = calculateGeoMipLevel(tileTexCoord, _ColA1_TexelSize.zw);
                        landAlbedo = SAMPLE_TEXTURE2D_LOD(_ColC2, sampler_ColC2, tileTexCoord, mipLevel).rgb;
                    } else { // D2
                        float2 tileTexCoord = float2((texCoord.x - 0.75) * 4.0, texCoord.y * 2.0);
                        float mipLevel = calculateGeoMipLevel(tileTexCoord, _ColA1_TexelSize.zw);
                        landAlbedo = SAMPLE_TEXTURE2D_LOD(_ColD2, sampler_ColD2, tileTexCoord, mipLevel).rgb;
                    }
                }

                float3 packedNormal;
                if (texCoord.x < 0.5) {
                    float2 tileTexCoord = float2(texCoord.x * 2.0, texCoord.y);
                    packedNormal = SAMPLE_TEXTURE2D(_NormalMapWest, sampler_NormalMapWest, tileTexCoord).xyz;
                } else {
                    float2 tileTexCoord = float2((texCoord.x - 0.5) * 2.0, texCoord.y);
                    packedNormal = SAMPLE_TEXTURE2D(_NormalMapEast, sampler_NormalMapEast, tileTexCoord).xyz;
                }
                
                Light mainLight = GetMainLight(i.shadowCoord);
                float3 dirToSun = mainLight.direction;
                
                float3 viewDir = SafeNormalize(i.positionWS - _WorldSpaceCameraPos);
                float3 detailNormal = normalize(packedNormal * 2.0 - 1.0);
                float3 meshNormalWorld = normalize(i.normalWS);
                float3 finalNormal = normalize(meshNormalWorld * 2.0 + detailNormal * 1.25);
                float3 objDirToSun = normalize(TransformWorldToObjectDir(dirToSun));
                float sunDot = dot(objRadial, objDirToSun);
                float dayFactor = smoothstep(-0.25, 0.25, sunDot);
                float nightFactor = 1.0 - dayFactor;
                float3 objNormal = normalize(i.normalOS);
                float curvature = pow(saturate(dot(objNormal, objRadial)), 3);
                
                float3 nightLandBase = _NightColor.rgb * curvature;
                
                float3 nightWaterBase = _LakeShadowColor.rgb * curvature;
                float3 nightColor = lerp(nightLandBase, nightWaterBase, lakeMask);
                float terminatorGlow = smoothstep(-0.1, 0.15, sunDot) * smoothstep(0.4, 0.15, sunDot);
                float cityLightIntensity = pow(saturate(SAMPLE_TEXTURE2D(_LightMap, sampler_LightMap, texCoord).r), _CityLightPower);
                float totalLightEmission = cityLightIntensity * (nightFactor + terminatorGlow * _TerminatorGlowBoost);
                nightColor += totalLightEmission * _CityLightColor.rgb * (1.0 - saturate(lakeMask));
                float baseFresnel = saturate(1.5 * pow(1.0 + dot(viewDir, finalNormal), 5));
                nightColor += baseFresnel * _FresnelCol.rgb;
                float surfaceShading = saturate(saturate(dot(finalNormal, dirToSun) + _BrightnessAdd)) * _BrightnessMul;
                float shadowAttenuation = mainLight.shadowAttenuation;

                // --- MODIFIED SECTION ---
                // Replaced the old shadow logic with a simplified, standard approach.
                float castShadowFactor = shadowAttenuation;
                float3 dayLandColor = landAlbedo * surfaceShading * castShadowFactor;
                // --- END MODIFIED SECTION ---

                float3 waveNormalObj = triplanarNormal(_WaveNormalA, sampler_WaveNormalA, i.positionOS, objRadial, _WaveNormalScale, 0, _WaveStrength);
                float3 waveNormalWorld = normalize(TransformObjectToWorldDir(waveNormalObj));

                float lakeGloss = _LakeParams.x;
                float lakeSpecStrength = _LakeParams.y;
                float lakeTintStrength = saturate(_LakeParams.z);
                float lakeFresnelStrength = _LakeParams.w;

                float lakeShininess = 256.0 / max(1e-4, lakeGloss * lakeGloss);

                float specular = calculateSpecular(waveNormalWorld, -viewDir, dirToSun, lakeShininess) * lakeSpecStrength;
                float fresnel = pow(1.0 - saturate(dot(viewDir, finalNormal)), 5) * lakeFresnelStrength;

                float3 unlitWaterColor = lerp(landAlbedo, _LakeCol.rgb, lakeTintStrength);
                float3 litWater = unlitWaterColor + specular + fresnel * _FresnelCol.rgb;
                float totalLightness = surfaceShading * castShadowFactor;
                float3 shadedWater = litWater * totalLightness;
                float shadowTintAmount = (1.0 - totalLightness) * _LakeShadowColor.a;
                float3 dayWaterColor = lerp(shadedWater, _LakeShadowColor.rgb, shadowTintAmount);

                float3 dayColor = lerp(dayLandColor, dayWaterColor, lakeMask);
                dayColor = lerp(0.5, dayColor, _Contrast);
                dayColor *= lerp(curvature, 1.0, 0.5);
                float3 finalColor = lerp(nightColor, dayColor * mainLight.color, dayFactor);

                return float4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Lit"
}