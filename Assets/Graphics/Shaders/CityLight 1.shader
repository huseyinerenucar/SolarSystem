Shader "Custom/CityLights_Material"
{
    Properties
    {
        _CityLightsMap ("City Lights Map (R=Intensity)", 2D) = "black" {}
        _NoiseTex ("Noise (for flicker)", 2D) = "white" {}

        [Header(Appearance)]
        _ColourDim ("Dim Colour", Color) = (1, 0.7, 0.3, 1)
        _ColourBright ("Bright Colour", Color) = (1, 1, 0.9, 1)
        _BrightnessMultiplier ("Overall Brightness", Float) = 1.5

        [Header(Night Transition)]
        _TurnOnTime ("Night Threshold", Range(-1, 1)) = -0.1
        _TurnOnTimeVariation ("Night Variation (Noise)", Range(0, 1)) = 0.2
        _TerminatorSoftness ("Terminator Softness", Range(0.01, 1)) = 0.1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }

        Pass
        {
            // Additive blending, no depth write, no backface culling
            ZWrite Off
            Cull Off
            Blend One One

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "UnityCG.cginc"
            // Include the helper file for converting positions to UVs
            #include "/GeoMath.hlsl"

            // Structs
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : NORMAL;
                float3 objPos : TEXCOORD0; // Object space position
            };

            // Properties
            sampler2D _CityLightsMap;
            sampler2D _NoiseTex;
            float4 _ColourDim;
            float4 _ColourBright;
            float _BrightnessMultiplier;
            float _TurnOnTime;
            float _TurnOnTimeVariation;
            float _TerminatorSoftness;


            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.objPos = v.vertex.xyz; // Pass object space vertex position to fragment shader
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 1. Get light intensity from the texture map
                // We use the object-space position to create spherical UVs
                float3 pointOnSphere = normalize(i.objPos);
                float2 uv = pointToUV(pointOnSphere);
                float cityIntensity = tex2D(_CityLightsMap, uv).r;

                // If there are no lights here, discard the pixel early
                if (cityIntensity < 0.01) {
                    clip(-1);
                }

                // 2. Re-implement the Night Gating logic from the original shader
                float3 dirToSun = _WorldSpaceLightPos0.xyz;
                float sunDot = dot(i.worldNormal, dirToSun); // +1 noon, -1 midnight

                // Use a noise texture to simulate the random variation
                float randomT = tex2D(_NoiseTex, uv * 50.0).r;
                float appearT = _TurnOnTime + (randomT - 0.5) * _TurnOnTimeVariation;

                // Calculate the night-side mask
                float nightGate = saturate((appearT - sunDot) / _TerminatorSoftness);
                
                // If it's fully day, exit
                if (nightGate <= 0) {
                    return fixed4(0,0,0,0);
                }

                // 3. Calculate the final light color
                // Interpolate between dim and bright based on the map's intensity
                float4 baseColour = lerp(_ColourDim, _ColourBright, cityIntensity);

                // Combine everything for the final emissive color
                float3 finalColour = baseColour.rgb * _BrightnessMultiplier * cityIntensity * nightGate;

                // For additive blending, we output the color to be added
                return fixed4(finalColour, 1.0);
            }
            ENDCG
        }
    }
    Fallback "Transparent/VertexLit"
}