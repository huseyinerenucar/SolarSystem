using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

public enum DepthMode
{
    None = 0,
    Depth16 = 16,
    Depth24 = 24
}

public enum Channel
{
    Red,
    Green,
    Blue,
    Alpha,
    Zero
}

public static class ComputeHelper
{
    public static event System.Action ShouldReleaseEditModeBuffers;
    public const FilterMode defaultFilterMode = FilterMode.Point;
    public const GraphicsFormat defaultGraphicsFormat = GraphicsFormat.R32G32B32A32_SFloat;

    static ComputeShader clearTextureCompute;
    static ComputeShader swizzleTextureCompute;
    static ComputeShader copy3DCompute;
    static Shader bicubicUpscale;

    public static void Run(ComputeShader cs, int numIterationsX, int numIterationsY = 1, int numIterationsZ = 1, int kernelIndex = 0)
    {
        Vector3Int threadGroupSizes = GetThreadGroupSizes(cs, kernelIndex);
        int numGroupsX = Mathf.CeilToInt(numIterationsX / (float)threadGroupSizes.x);
        int numGroupsY = Mathf.CeilToInt(numIterationsY / (float)threadGroupSizes.y);
        int numGroupsZ = Mathf.CeilToInt(numIterationsZ / (float)threadGroupSizes.y);
        cs.Dispatch(kernelIndex, numGroupsX, numGroupsY, numGroupsZ);
    }

    public static void Run(ComputeShader cs, RenderTexture texture, int kernelIndex = 0)
    {
        Run(cs, texture.width, texture.height, texture.volumeDepth, kernelIndex);
    }

    public static void Run(ComputeShader cs, Texture2D texture, int kernelIndex = 0)
    {
        Run(cs, texture.width, texture.height, 1, kernelIndex);
    }

    public static int GetStride<T>()
    {
        return System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));
    }

    public static bool CanRunEditModeCompute
    {
        get
        {
            return CheckIfCanRunInEditMode();
        }
    }

    public static void SetParams(System.Object settings, ComputeShader shader, string variableNamePrefix = "", string variableNameSuffix = "")
    {
        var fields = settings.GetType().GetFields();
        foreach (var field in fields)
        {
            var fieldType = field.FieldType;
            string shaderVariableName = variableNamePrefix + field.Name + variableNameSuffix;

            if (fieldType == typeof(UnityEngine.Vector4) || fieldType == typeof(Vector3) || fieldType == typeof(Vector2))
                shader.SetVector(shaderVariableName, (Vector4)field.GetValue(settings));
            else if (fieldType == typeof(int))
                shader.SetInt(shaderVariableName, (int)field.GetValue(settings));
            else if (fieldType == typeof(float))
                shader.SetFloat(shaderVariableName, (float)field.GetValue(settings));
            else if (fieldType == typeof(bool))
                shader.SetBool(shaderVariableName, (bool)field.GetValue(settings));
            else
                Debug.Log($"Type {fieldType} not implemented");
        }
    }

    public static ComputeBuffer CreateAppendBuffer<T>(int capacity)
    {
        int stride = GetStride<T>();
        ComputeBuffer buffer = new(capacity, stride, ComputeBufferType.Append);
        buffer.SetCounterValue(0);
        return buffer;
    }

    public static void CreateStructuredBuffer<T>(ref ComputeBuffer buffer, int count)
    {
        int stride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));
        bool createNewBuffer = buffer == null || !buffer.IsValid() || buffer.count != count || buffer.stride != stride;

        if (createNewBuffer)
        {
            Release(buffer);
            buffer = new ComputeBuffer(count, stride);
        }
    }

    public static ComputeBuffer CreateStructuredBuffer<T>(T[] data)
    {
        var buffer = new ComputeBuffer(data.Length, GetStride<T>());
        buffer.SetData(data);
        return buffer;
    }

    public static ComputeBuffer CreateStructuredBuffer<T>(List<T> data) where T : struct
    {
        var buffer = new ComputeBuffer(data.Count, GetStride<T>());
        buffer.SetData<T>(data);
        return buffer;
    }

    public static ComputeBuffer CreateStructuredBuffer<T>(int count)
    {
        return new ComputeBuffer(count, GetStride<T>());
    }

    public static void CreateStructuredBuffer<T>(ref ComputeBuffer buffer, T[] data)
    {
        CreateStructuredBuffer<T>(ref buffer, data.Length);
        buffer.SetData(data);
    }

    public static ComputeBuffer CreateAndSetBuffer<T>(T[] data, ComputeShader cs, string nameID, int kernelIndex = 0)
    {
        ComputeBuffer buffer = null;
        CreateAndSetBuffer<T>(ref buffer, data, cs, nameID, kernelIndex);
        return buffer;
    }

    public static void CreateAndSetBuffer<T>(ref ComputeBuffer buffer, T[] data, ComputeShader cs, string nameID, int kernelIndex = 0)
    {
        int stride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));
        CreateStructuredBuffer<T>(ref buffer, data.Length);
        buffer.SetData(data);
        cs.SetBuffer(kernelIndex, nameID, buffer);
    }

    public static ComputeBuffer CreateAndSetBuffer<T>(int length, ComputeShader cs, string nameID, int kernelIndex = 0)
    {
        ComputeBuffer buffer = null;
        CreateAndSetBuffer<T>(ref buffer, length, cs, nameID, kernelIndex);
        return buffer;
    }

    public static void CreateAndSetBuffer<T>(ref ComputeBuffer buffer, int length, ComputeShader cs, string nameID, int kernelIndex = 0)
    {
        CreateStructuredBuffer<T>(ref buffer, length);
        cs.SetBuffer(kernelIndex, nameID, buffer);
    }

    public static T[] ReadDataFromBuffer<T>(ComputeBuffer buffer, bool isAppendBuffer)
    {
        int numElements = buffer.count;
        if (isAppendBuffer)
        {
            ComputeBuffer sizeBuffer = new(1, sizeof(int), ComputeBufferType.IndirectArguments);
            ComputeBuffer.CopyCount(buffer, sizeBuffer, 0);
            int[] bufferCountData = new int[1];
            sizeBuffer.GetData(bufferCountData);
            numElements = bufferCountData[0];
            Release(sizeBuffer);
        }

        T[] data = new T[numElements];
        buffer.GetData(data);

        return data;
    }

    public static void ResetAppendBuffer(ComputeBuffer appendBuffer)
    {
        appendBuffer.SetCounterValue(0);
    }

    public static void Release(params ComputeBuffer[] buffers)
    {
        for (int i = 0; i < buffers.Length; i++)
            buffers[i]?.Release();
    }

    public static void Release(params RenderTexture[] textures)
    {
        for (int i = 0; i < textures.Length; i++)
        {
            if (textures[i] != null)
                textures[i].Release();
        }
    }

    public static Vector3Int GetThreadGroupSizes(ComputeShader compute, int kernelIndex = 0)
    {
        compute.GetKernelThreadGroupSizes(kernelIndex, out uint x, out uint y, out uint z);
        return new Vector3Int((int)x, (int)y, (int)z);
    }

    public static RenderTexture CreateRenderTexture(RenderTexture template)
    {
        RenderTexture renderTexture = null;
        CreateRenderTexture(ref renderTexture, template);
        return renderTexture;
    }

    public static RenderTexture CreateRenderTexture(int width, int height, FilterMode filterMode, GraphicsFormat format, string name = "Unnamed", DepthMode depthMode = DepthMode.None, bool useMipMaps = false)
    {
        RenderTexture texture = new(width, height, (int)depthMode)
        {
            graphicsFormat = format,
            enableRandomWrite = true,
            autoGenerateMips = false,
            useMipMap = useMipMaps
        };
        texture.Create();

        texture.name = name;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = filterMode;
        return texture;
    }

    public static void CreateRenderTexture(ref RenderTexture texture, RenderTexture template)
    {
        if (texture != null)
        {
            texture.Release();
        }
        texture = new RenderTexture(template.descriptor);
        texture.enableRandomWrite = true;
        texture.Create();
    }

    public static void CreateRenderTexture(ref RenderTexture texture, int size, FilterMode filterMode = FilterMode.Bilinear, GraphicsFormat format = GraphicsFormat.R16G16B16A16_SFloat)
    {
        CreateRenderTexture(ref texture, size, size, filterMode, format);
    }

    public static bool CreateRenderTexture(ref RenderTexture texture, int width, int height, FilterMode filterMode, GraphicsFormat format, string name = "Unnamed", DepthMode depthMode = DepthMode.None, bool useMipMaps = false)
    {
        if (texture == null || !texture.IsCreated() || texture.width != width || texture.height != height || texture.graphicsFormat != format || texture.depth != (int)depthMode || texture.useMipMap != useMipMaps)
        {
            if (texture != null)
                texture.Release();

            texture = CreateRenderTexture(width, height, filterMode, format, name, depthMode, useMipMaps);
            return true;
        }
        else
        {
            texture.name = name;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = filterMode;
        }

        return false;
    }

    public static void CreateRenderTexture3D(ref RenderTexture texture, RenderTexture template)
    {
        CreateRenderTexture(ref texture, template);
    }

    public static void CreateRenderTexture3D(ref RenderTexture texture, int size, GraphicsFormat format, TextureWrapMode wrapMode = TextureWrapMode.Repeat, string name = "Untitled", bool mipmaps = false)
    {
        if (texture == null || !texture.IsCreated() || texture.width != size || texture.height != size || texture.volumeDepth != size || texture.graphicsFormat != format)
        {
            if (texture != null)
                texture.Release();

            const int numBitsInDepthBuffer = 0;
            texture = new RenderTexture(size, size, numBitsInDepthBuffer)
            {
                graphicsFormat = format,
                volumeDepth = size,
                enableRandomWrite = true,
                dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
                useMipMap = mipmaps,
                autoGenerateMips = false
            };
            texture.Create();
        }
        texture.wrapMode = wrapMode;
        texture.filterMode = FilterMode.Bilinear;
        texture.name = name;
    }

    public static void CopyRenderTexture(Texture source, RenderTexture target)
    {
        Graphics.Blit(source, target);
    }

    public static void CopyRenderTexture3D(Texture source, RenderTexture target)
    {
        LoadComputeShader(ref copy3DCompute, "Copy3D");
        copy3DCompute.SetInts("dimensions", target.width, target.height, target.volumeDepth);
        copy3DCompute.SetTexture(0, "Source", source);
        copy3DCompute.SetTexture(0, "Target", target);
        Run(copy3DCompute, target.width, target.height, target.volumeDepth);
    }

    public static void SwizzleTexture(Texture texture, Channel x, Channel y, Channel z, Channel w)
    {
        if (swizzleTextureCompute == null)
            swizzleTextureCompute = (ComputeShader)Resources.Load("Swizzle");

        swizzleTextureCompute.SetInt("width", texture.width);
        swizzleTextureCompute.SetInt("height", texture.height);
        swizzleTextureCompute.SetTexture(0, "Source", texture);
        swizzleTextureCompute.SetVector("x", ChannelToMask(x));
        swizzleTextureCompute.SetVector("y", ChannelToMask(y));
        swizzleTextureCompute.SetVector("z", ChannelToMask(z));
        swizzleTextureCompute.SetVector("w", ChannelToMask(w));
        Run(swizzleTextureCompute, texture.width, texture.height, 1, 0);
    }

    public static void ClearRenderTexture(RenderTexture source)
    {
        LoadComputeShader(ref clearTextureCompute, "ClearTexture");

        clearTextureCompute.SetInt("width", source.width);
        clearTextureCompute.SetInt("height", source.height);
        clearTextureCompute.SetTexture(0, "Source", source);
        Run(clearTextureCompute, source.width, source.height, 1, 0);
    }

    public static RenderTexture BicubicUpscale(RenderTexture original, int sizeMultiplier = 2)
    {
        RenderTexture upscaled = CreateRenderTexture(original.width * sizeMultiplier, original.height * sizeMultiplier, original.filterMode, original.graphicsFormat, original.name + " upscaled");
        upscaled.wrapModeU = original.wrapModeU;
        upscaled.wrapModeV = original.wrapModeV;
        LoadShader(ref bicubicUpscale, "BicubicUpscale");
        Material material = new(bicubicUpscale);
        material.SetVector("textureSize", new Vector2(original.width, original.height));
        Graphics.Blit(original, upscaled, material);
        return upscaled;
    }

    public static ComputeBuffer CreateArgsBuffer(Mesh mesh, int numInstances)
    {
        const int subMeshIndex = 0;
        uint[] args = new uint[5];
        args[0] = mesh.GetIndexCount(subMeshIndex);
        args[1] = (uint)numInstances;
        args[2] = mesh.GetIndexStart(subMeshIndex);
        args[3] = mesh.GetBaseVertex(subMeshIndex);
        args[4] = 0;

        ComputeBuffer argsBuffer = new(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        argsBuffer.SetData(args);
        return argsBuffer;
    }

    public static void CreateArgsBuffer(ref ComputeBuffer argsBuffer, Mesh mesh, int numInstances)
    {
        Release(argsBuffer);
        argsBuffer = CreateArgsBuffer(mesh, numInstances);
    }

    public static ComputeBuffer CreateArgsBuffer(Mesh mesh, ComputeBuffer appendBuffer)
    {
        ComputeBuffer argsBuffer = CreateArgsBuffer(mesh, 0);
        SetArgsBufferCount(argsBuffer, appendBuffer);
        return argsBuffer;
    }

    public static void SetArgsBufferCount(ComputeBuffer argsBuffer, ComputeBuffer appendBuffer)
    {
        ComputeBuffer.CopyCount(appendBuffer, argsBuffer, sizeof(uint));
    }

    public static void AssignTexture(ComputeShader compute, Texture texture, string name, params int[] kernels)
    {
        for (int i = 0; i < kernels.Length; i++)
            compute.SetTexture(kernels[i], name, texture);
    }

    public static void AssignBuffer(ComputeShader compute, ComputeBuffer texture, string name, params int[] kernels)
    {
        for (int i = 0; i < kernels.Length; i++)
            compute.SetBuffer(kernels[i], name, texture);
    }

    public static float[] PackFloats(params float[] values)
    {
        float[] packed = new float[values.Length * 4];
        for (int i = 0; i < values.Length; i++)
            packed[i * 4] = values[i];
        return values;
    }

    static Vector4 ChannelToMask(Channel channel)
    {
        return channel switch
        {
            Channel.Red => new Vector4(1, 0, 0, 0),
            Channel.Green => new Vector4(0, 1, 0, 0),
            Channel.Blue => new Vector4(0, 0, 1, 0),
            Channel.Alpha => new Vector4(0, 0, 0, 1),
            Channel.Zero => new Vector4(0, 0, 0, 0),
            _ => Vector4.zero,
        };
    }

    static void LoadComputeShader(ref ComputeShader shader, string name)
    {
        if (shader == null)
            shader = (ComputeShader)Resources.Load(name);
    }

    static void LoadShader(ref Shader shader, string name)
    {
        if (shader == null)
            shader = (Shader)Resources.Load(name);
    }


#if UNITY_EDITOR
    static UnityEditor.PlayModeStateChange playModeState;

    static ComputeHelper()
    {
        UnityEditor.EditorApplication.playModeStateChanged -= MonitorPlayModeState;
        UnityEditor.EditorApplication.playModeStateChanged += MonitorPlayModeState;

        UnityEditor.Compilation.CompilationPipeline.compilationStarted -= OnCompilationStarted;
        UnityEditor.Compilation.CompilationPipeline.compilationStarted += OnCompilationStarted;
    }

    static void MonitorPlayModeState(UnityEditor.PlayModeStateChange state)
    {
        playModeState = state;
        if (state == UnityEditor.PlayModeStateChange.ExitingEditMode)
            ShouldReleaseEditModeBuffers?.Invoke();
    }

    static void OnCompilationStarted(System.Object obj)
    {
        ShouldReleaseEditModeBuffers?.Invoke();
    }
#endif

    static bool CheckIfCanRunInEditMode()
    {
        bool isCompilingOrExitingEditMode = false;
#if UNITY_EDITOR
        isCompilingOrExitingEditMode |= UnityEditor.EditorApplication.isCompiling;
        isCompilingOrExitingEditMode |= playModeState == UnityEditor.PlayModeStateChange.ExitingEditMode;
#endif
        bool canRun = !isCompilingOrExitingEditMode;
        return canRun;
    }
}