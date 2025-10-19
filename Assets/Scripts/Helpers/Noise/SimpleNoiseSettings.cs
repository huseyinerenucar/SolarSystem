using UnityEngine;

[System.Serializable]
public class SimpleNoiseSettings
{
    public int numLayers = 4;
    public float lacunarity = 2;
    public float persistence = 0.5f;
    public float scale = 1;
    public float elevation = 1;
    public float verticalShift = 0;
    public Vector3 offset;

    public void SetComputeValues(ComputeShader cs, RandomHelper prng, string varSuffix)
    {
        SetComputeValues(cs, prng, varSuffix, scale, elevation, persistence);
    }

    public void SetComputeValues(ComputeShader cs, RandomHelper prng, string varSuffix, float scale, float elevation)
    {
        SetComputeValues(cs, prng, varSuffix, scale, elevation, persistence);
    }

    public void SetComputeValues(ComputeShader cs, RandomHelper prng, string varSuffix, float scale, float elevation, float persistence)
    {
        Vector3 seededOffset = 10000 * prng.Value() * new Vector3(prng.Value(), prng.Value(), prng.Value());

        float[] noiseParams = {
            seededOffset.x + offset.x,
            seededOffset.y + offset.y,
            seededOffset.z + offset.z,
            numLayers,
            persistence,
            lacunarity,
            scale,
            elevation,
            verticalShift
        };

        cs.SetFloats("noiseParams" + varSuffix, noiseParams);
    }
}