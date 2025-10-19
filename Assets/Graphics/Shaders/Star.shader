// Celestial/StarURP.shader
Shader "Celestial/StarURP"
{
    Properties
    {
        // Properties are not strictly needed since the script sets them,
        // but can be useful for debugging in the material inspector.
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        Pass
        {
            // URP specific pass tags and render states
            Tags { "LightMode" = "UniversalForward" }
            LOD 100
            ZWrite Off
            Lighting Off
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // --- URP Change: Include URP Core library ---
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // This struct defines the data passed from the C# script to the vertex shader.
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            // This struct defines the data passed from the vertex shader to the fragment shader.
            struct Varyings
            {
                float4 positionCS     : SV_POSITION;
                float4 screenPos      : TEXCOORD0; // For screen space UVs
                float  brightness     : TEXCOORD1;
                float  spectrumLookup : TEXCOORD2;
            };
            
            // --- URP Change: Declare textures for URP ---
            // _CameraColorTexture contains the rendered scene color.
            TEXTURE2D(_CameraColorTexture);
            SAMPLER(sampler_CameraColorTexture);

            TEXTURE2D(_Spectrum);
            SAMPLER(sampler_Spectrum);
            
            TEXTURE2D(_OceanMask);
            SAMPLER(sampler_OceanMask);

            // C# script will set this value.
            float daytimeFade;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                // --- URP Change: Use TransformObjectToHClip for vertex transformation ---
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.screenPos = ComputeScreenPos(OUT.positionCS);

                // Pass UV data to the fragment shader
                OUT.brightness = IN.uv.x;
                OUT.spectrumLookup = IN.uv.y;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                // --- URP Change: All background sampling logic moved to the fragment shader ---
                float2 screenSpaceUV = IN.screenPos.xy / IN.screenPos.w;

                // Sample the textures using URP's macros
                float4 backgroundCol = SAMPLE_TEXTURE2D(_CameraColorTexture, sampler_CameraColorTexture, screenSpaceUV);
                float oceanMask = SAMPLE_TEXTURE2D(_OceanMask, sampler_OceanMask, screenSpaceUV).r;

                // Calculate star visibility based on background brightness and ocean mask
                float backgroundBrightness = saturate(dot(backgroundCol.rgb, 0.333) * daytimeFade);
                float starVisibility = (1 - backgroundBrightness) * (1 - oceanMask);

                // Sample the star's color from the spectrum gradient
                float4 starCol = SAMPLE_TEXTURE2D(_Spectrum, sampler_Spectrum, float2(IN.spectrumLookup, 0.5));
                
                // Calculate the brightness falloff from the center of the star (procedural quad)
                float brightnessFalloff = saturate(IN.brightness + 0.1);
                brightnessFalloff *= brightnessFalloff; // Square for a sharper falloff

                float finalAlpha = starVisibility * brightnessFalloff;
                
                return float4(starCol.rgb, finalAlpha);
            }
            ENDHLSL
        }
    }
}