#ifndef TRIPLANAR_INCLUDED
#define TRIPLANAR_INCLUDED

// --- URP-COMPATIBLE VERSION ---
// TEXTURE2D and SAMPLER are now passed as separate arguments.
// tex2D() is replaced with the SAMPLE_TEXTURE2D() macro.
float4 triplanar(TEXTURE2D( tex), SAMPLER(sampler_tex),
float3 vertPos, float3 normal, float3 scale, float2 offset = 0)
{
float3 scaledPos = vertPos / scale;
    // Use the URP texture sampling macro
float4 colX = SAMPLE_TEXTURE2D(tex, sampler_tex, scaledPos.zy + offset);
float4 colY = SAMPLE_TEXTURE2D(tex, sampler_tex, scaledPos.xz + offset);
float4 colZ = SAMPLE_TEXTURE2D(tex, sampler_tex, scaledPos.xy + offset);
	
	// Blending logic is unchanged
float3 blendWeight = normal * normal;
    blendWeight /= dot(blendWeight, 1);
    return colX * blendWeight.x + colY * blendWeight.y + colZ * blendWeight.
z;
}

// Reoriented Normal Mapping - No changes needed as it's pure math.
float3 blend_rnm(float3 n1, float3 n2)
{
    n1.z += 1;
    n2.xy = -n2.xy;

    return n1 * dot(n1, n2) / n1.z - n2;
}

// Custom normal unpacking - No changes needed as it operates on the result of a texture sample.
float3 unpackScaleNormal(float4 packednormal, float normalScale)
{
    float3 normal;
    normal.xy = (packednormal.wy * 2 - 1); // Assumes normal is packed in Alpha and Green channels
    normal.xy *= normalScale;
    normal.z = sqrt(1.0 - saturate(dot(normal.xy, normal.xy)));
    return normal;
}

// --- URP-COMPATIBLE VERSION ---
// Updated function signature and sampling calls.
float3 triplanarNormal(TEXTURE2D( normalMap), SAMPLER(sampler_normalMap),
float3 pos, float3 normal, float3 scale, float2 offset, float strength = 1)
{
float3 absNormal = abs(normal);

float3 blendWeight = saturate(pow(normal, 4));
    blendWeight /= dot(blendWeight, 1);

float2 uvX = pos.zy * scale.x + offset;
float2 uvY = pos.xz * scale.y + offset;
float2 uvZ = pos.xy * scale.z + offset;

    // Use the URP texture sampling macro
float3 tangentNormalX = unpackScaleNormal(SAMPLE_TEXTURE2D(normalMap, sampler_normalMap, uvX), strength);
float3 tangentNormalY = unpackScaleNormal(SAMPLE_TEXTURE2D(normalMap, sampler_normalMap, uvY), strength);
float3 tangentNormalZ = unpackScaleNormal(SAMPLE_TEXTURE2D(normalMap, sampler_normalMap, uvZ), strength);

    // Blending and swizzling logic is unchanged
    tangentNormalX = blend_rnm(half3(normal.zy, absNormal.x), tangentNormalX);
    tangentNormalY = blend_rnm(half3(normal.xz, absNormal.y), tangentNormalY);
    tangentNormalZ = blend_rnm(half3(normal.xy, absNormal.z), tangentNormalZ);

float3 axisSign = sign(normal);
    tangentNormalX.z *= axisSign.
x;
    tangentNormalY.z *= axisSign.
y;
    tangentNormalZ.z *= axisSign.
z;

float3 outputNormal = normalize(
		tangentNormalX.zyx * blendWeight.x +
		tangentNormalY.xzy * blendWeight.y +
		tangentNormalZ.xyz * blendWeight.z
	);

    return
outputNormal;
}


// --- URP-COMPATIBLE VERSION of the second overload ---
// Updated function signature and sampling calls.
float3 triplanarNormal(TEXTURE2D( normalMap), SAMPLER(sampler_normalMap),
float3 pos, float3 normal, float3 scale, float2 offset, float strength, out
float3 tangentNormal)
{
float3 absNormal = abs(normal);

float3 blendWeight = saturate(pow(normal, 4));
    blendWeight /= dot(blendWeight, 1);

float2 uvX = pos.zy * scale.x + offset;
float2 uvY = pos.xz * scale.y + offset;
float2 uvZ = pos.xy * scale.z + offset;

    // Use the URP texture sampling macro
float3 tangentNormalX = unpackScaleNormal(SAMPLE_TEXTURE2D(normalMap, sampler_normalMap, uvX), strength);
float3 tangentNormalY = unpackScaleNormal(SAMPLE_TEXTURE2D(normalMap, sampler_normalMap, uvY), strength);
float3 tangentNormalZ = unpackScaleNormal(SAMPLE_TEXTURE2D(normalMap, sampler_normalMap, uvZ), strength);

    tangentNormal = tangentNormalX * blendWeight.x + tangentNormalY * blendWeight.y + tangentNormalZ * blendWeight.
z;

    // Blending and swizzling logic is unchanged
    tangentNormalX = blend_rnm(half3(normal.zy, absNormal.x), tangentNormalX);
    tangentNormalY = blend_rnm(half3(normal.xz, absNormal.y), tangentNormalY);
    tangentNormalZ = blend_rnm(half3(normal.xy, absNormal.z), tangentNormalZ);

float3 axisSign = sign(normal);
    tangentNormalX.z *= axisSign.
x;
    tangentNormalY.z *= axisSign.
y;
    tangentNormalZ.z *= axisSign.
z;

float3 outputNormal = normalize(
		tangentNormalX.zyx * blendWeight.x +
		tangentNormalY.xzy * blendWeight.y +
		tangentNormalZ.xyz * blendWeight.z
	);

    return
outputNormal;
}

#endif