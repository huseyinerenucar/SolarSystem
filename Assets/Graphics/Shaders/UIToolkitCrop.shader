Shader "Hidden/UI Toolkit Crop"
{
    Properties
    {
        _MainTex ("Source Texture", 2D) = "white" {}
        _ForegroundColor ("Foreground Color", Color) = (1, 1, 1, 0.5)
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };
            
            struct Varyings
            {
                float4 vertex : SV_POSITION;
                half2 uv      : TEXCOORD0;
            };
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            half4 _CropViewportRect;
            half4 _ForegroundColor;
            
            Varyings Vert(Attributes input)
            {
                Varyings output;
                
                output.vertex = float4(
                    (input.vertexID << 1) & 2 ? 3.0 : -1.0,
                    input.vertexID & 2        ? 3.0 : -1.0,
                    0.0, 1.0
                );
                
                output.uv = output.vertex.xy * 0.5 + 0.5;
                
                return output;
            }
            
            half4 Frag(Varyings i) : SV_Target
            {
                half2 sourceUV = _CropViewportRect.xy + (i.uv * _CropViewportRect.zw);
                half4 blurredColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sourceUV);
                
                // Blend foreground color over blurred background using alpha blending
                half3 finalColor = lerp(blurredColor.rgb, _ForegroundColor.rgb, _ForegroundColor.a);
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}