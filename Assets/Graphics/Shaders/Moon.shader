// Shader "Custom/URP Blinn-Phong (Parity)"
// This shader replicates the visual output of a standard Built-in Render Pipeline
// Blinn-Phong shader, but is written to be fully compatible with the
// Universal Render Pipeline (URP). It correctly handles lighting and shadows
// using URP's single-pass architecture and Cascaded Shadow Maps.
Shader "Expert/URP Blinn-Phong (Parity)"
{
    // The Properties block defines the material properties that will be exposed
    // in the Unity Inspector. These are analogous to the BRP shader's properties.
    Properties
    {
        _BaseColor("Base Color", Color) = (1,1,1,1)
        _Shininess("Shininess", Range(0.1, 256)) = 20
    }

    // A SubShader block contains the rendering passes. A shader can have multiple
    // SubShaders for different hardware, but for URP, one is sufficient.
    SubShader
    {
        // The "RenderPipeline"="UniversalPipeline" tag is essential. It tells Unity
        // that this SubShader is designed specifically for URP.
        // "RenderType"="Opaque" is a hint to the pipeline that this shader does not
        // handle transparency, allowing for optimizations like z-prepass.
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }

        // This is the main rendering pass for URP's forward renderer.
        // It calculates the final lit and shadowed color of the object.
        Pass
        {
            // The "LightMode"="UniversalForward" tag identifies this as the main pass.
            // The pipeline will provide all necessary lighting data to this pass.
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // These #pragma directives are CRITICAL for enabling shadows.
            // They compile shader variants to handle different shadow configurations
            // (no shadows, simple shadows, cascaded shadows). Without them,
            // all shadow-related functions will be stripped from the shader.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT

            // Include URP's core and lighting shader libraries. This provides access
            // to all necessary functions and variables for transformations, lighting, and shadows.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // Material properties must be declared inside this specific CBUFFER
            // to be compatible with URP's SRP Batcher optimization.
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Shininess;
            CBUFFER_END

            // Input structure for the vertex shader.
            // It matches the mesh data provided by Unity (position, normal, UVs).
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            // Output structure for the vertex shader, which becomes the
            // input structure for the fragment shader. Data is interpolated
            // across the triangle surface.
            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                // This variable will hold the screen-space shadow coordinates,
                // which are essential for sampling the shadow map.
                float4 shadowCoord  : TEXCOORD2;
            };

            // The Vertex Shader
            Varyings vert(Attributes input)
            {
                Varyings output;

                // Transform vertex position and normal from object space to world space.
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);

                // Transform world space position to clip space for rendering.
                output.positionCS = TransformWorldToHClip(output.positionWS);

                // This is a key URP function. It computes the shadow coordinates for the
                // current vertex, handling all the complexity of cascades internally.
                output.shadowCoord = TransformWorldToShadowCoord(output.positionWS);

                return output;
            }

            // The Fragment Shader
            half4 frag(Varyings i) : SV_Target
            {
                // Normalize the interpolated normal vector to ensure it's a unit vector.
                i.normalWS = normalize(i.normalWS);

                // --- LIGHTING CALCULATIONS ---

                // 1. Get Main Light Data
                // This function call retrieves a 'Light' struct containing the main
                // light's color, direction, and attenuation.
                Light mainLight = GetMainLight(i.shadowCoord);

                // 2. Calculate Ambient Lighting
                // Sample the scene's spherical harmonics (from Light Probes or Skybox)
                // to get a high-quality, indirect ambient light term.
                half3 ambient = SampleSH(i.normalWS);

                // 3. Calculate Diffuse Lighting (Lambertian)
                half NdotL = saturate(dot(i.normalWS, mainLight.direction));
                half3 diffuse = NdotL * mainLight.color;

                // 4. Calculate Specular Lighting (Blinn-Phong)
                half3 viewDir = normalize(_WorldSpaceCameraPos.xyz - i.positionWS);
                half3 halfDir = normalize(mainLight.direction + viewDir);
                half spec = pow(saturate(dot(i.normalWS, halfDir)), _Shininess);
                half3 specular = spec * mainLight.color;

                // 5. Get Shadow Attenuation
                // This function samples the shadow map using the coordinates calculated
                // in the vertex shader. It returns 1.0 for lit areas and 0.0 for shadowed areas.
                half shadowAttenuation = mainLight.shadowAttenuation;

                // --- FINAL COLOR COMPOSITION ---

                // Combine diffuse and specular into a single "direct lighting" term.
                half3 directLighting = diffuse + specular;

                // Apply shadows ONLY to the direct lighting component.
                directLighting *= shadowAttenuation;

                // Combine ambient and direct lighting, modulated by the base color.
                half3 finalColor = (ambient * _BaseColor.rgb) + (directLighting * _BaseColor.rgb);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        // This is the ShadowCaster pass. It is required for the object to cast shadows
        // onto other objects. It renders the object's depth from the light's perspective.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // This directive ensures the shader compiles variants for different
            // shadow-casting scenarios (e.g., point light shadows).
            #pragma multi_compile_shadowcaster

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            // CBUFFER must match the main pass to maintain SRP Batcher compatibility,
            // especially if properties are used for alpha clipping or vertex displacement.
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Shininess;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
            };

            // This vertex shader for the ShadowCaster pass is simpler. Its only job
            // is to correctly position the vertices in the light's clip space.
            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                // ApplyShadowBias is a crucial function that applies a small offset
                // to the vertex position to prevent "shadow acne" artifacts.
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _MainLightPosition.xyz));
                return output;
            }

            // The fragment shader for the ShadowCaster pass is trivial.
            // It does not need to output a color, so it can be empty.
            half4 frag(Varyings i) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }
    }
}