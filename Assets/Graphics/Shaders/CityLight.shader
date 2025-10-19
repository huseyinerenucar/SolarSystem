Shader "Instanced/CityLight_MaterialDriven"
{
    Properties
    {
        _MainTex ("Light Texture (Soft Dot)", 2D) = "white" {}

        // tweak these in the Material inspector
        colourDim            ("Dim Colour", Color) = (0.4,0.4,0.5,1)
        colourBright         ("Bright Colour", Color) = (1,0.95,0.85,1)
        brightnessMultiplier ("Brightness Multiplier", Float) = 1
        sizeMin              ("Size Min", Float) = 0.25
        sizeMax              ("Size Max", Float) = 1.0
        turnOnTimeVariation  ("Turn-On Time Variation", Float) = 0.25
        turnOnTime           ("Turn-On Time (0=sunrise,1=midnight)", Float) = 0.6
        pixelScale           ("Pixel Scale (world units)", Float) = 0.02
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend One One

            CGPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            struct CityLight
            {
                float3 pointOnSphere; // normalized outward
                float  height;        // radius at this point
                float  intensity;     // 0..1
                float  randomT;       // 0..1
            };

            // Per-draw (set from script)
            StructuredBuffer<CityLight> CityLights;
            int      bufferOffset;
            float4x4 _PlanetLocalToWorld;
            float3   dirToSun;

            // From Material
            float4 colourDim;
            float4 colourBright;
            float  brightnessMultiplier;
            float  sizeMin;
            float  sizeMax;
            float  turnOnTimeVariation;
            float  turnOnTime;
            float  pixelScale;

            struct appdata { float4 vertex : POSITION; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 col : TEXCOORD0;
                float2 uv  : TEXCOORD1;
            };

            void CameraFacingBasis(float3 camPos, float3 worldCentre, out float3 right, out float3 up)
            {
                float3 viewDir = normalize(camPos - worldCentre);
                float3 upRef   = abs(viewDir.y) > 0.95 ? float3(0,0,1) : float3(0,1,0);
                right = normalize(cross(upRef, viewDir));
                up    = normalize(cross(viewDir, right));
            }

            v2f vert(appdata v, uint instanceID : SV_InstanceID)
            {
                CityLight L = CityLights[bufferOffset + instanceID];

                // Day-night gate (simple model: sunDot=+1 noon, -1 midnight)
                float sunDot    = dot(L.pointOnSphere, dirToSun);
                float appearT   = turnOnTime + (L.randomT - 0.5) * turnOnTimeVariation;
                float nightGate = saturate((appearT - sunDot) / 0.1);

                float size = lerp(sizeMin, sizeMax, L.intensity) * nightGate;

                // planet local -> world
                float3 localCentre = L.pointOnSphere * L.height;
                float3 worldCentre = mul(_PlanetLocalToWorld, float4(localCentre, 1)).xyz;

                // Billboard quad in world space
                float3 right, up;
                CameraFacingBasis(_WorldSpaceCameraPos.xyz, worldCentre, right, up);
                float2 q = v.vertex.xy; // quad in [-0.5..0.5]
                float3 worldPos = worldCentre + (q.x * right + q.y * up) * (size * pixelScale);

                // subtle lift to prevent z-fighting
                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - worldCentre);
                worldPos += viewDir * (0.01 + L.intensity * 0.02);

                v2f o;
                o.pos = mul(UNITY_MATRIX_VP, float4(worldPos, 1));

                // subtle flicker
                float flicker = sin((_Time.y * 2.0) + L.randomT * 30.0);
                flicker = (flicker * 0.5 + 0.5) * 0.1 + 0.95;

                o.col = lerp(colourDim, colourBright * brightnessMultiplier, L.intensity) * flicker;
                o.uv  = v.vertex.xy + 0.5; // map [-0.5..0.5] -> [0..1]
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float alpha = tex2D(_MainTex, i.uv).r; // use red channel for a soft dot
                return float4(i.col.rgb * alpha, 1);
            }
            ENDCG
        }
    }
}
