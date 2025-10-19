using UnityEngine;

public abstract class CelestialBodyShape : ScriptableObject
{
    public bool randomize;
    public int seed;
    public ComputeShader heightMapCompute;

    public bool perturbVertices;
    public ComputeShader perturbCompute;
    [Range(0, 1)]
    public float perturbStrength = 0.7f;

    public event System.Action OnSettingChanged;

    ComputeBuffer heightBuffer;

    public virtual float[] CalculateHeights(ComputeBuffer vertexBuffer)
    {
        SetShapeData();
        heightMapCompute.SetInt("numVertices", vertexBuffer.count);
        heightMapCompute.SetBuffer(0, "vertices", vertexBuffer);
        ComputeHelper.CreateAndSetBuffer<float>(ref heightBuffer, vertexBuffer.count, heightMapCompute, "heights");

        var heights = new float[vertexBuffer.count];

        if (vertexBuffer.count < 2)
            return heights;

        ComputeHelper.Run(heightMapCompute, vertexBuffer.count);

        heightBuffer.GetData(heights);

        return heights;
    }

    public virtual void ReleaseBuffers()
    {
        ComputeHelper.Release(heightBuffer);
    }

    protected virtual void SetShapeData()
    {

    }

    protected virtual void OnValidate()
    {
        OnSettingChanged?.Invoke();
    }

}