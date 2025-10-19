using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[ExecuteInEditMode]
public class CelestialBodyGenerator : MonoBehaviour
{
    public enum PreviewMode { LOD0, LOD1, LOD2, CollisionRes }
    public ResolutionSettings resolutionSettings;
    public PreviewMode previewMode;

    public bool logTimers;

    public CelestialBodySettings body;

    readonly bool debugDoubleUpdate = true;
    int debug_numUpdates;

    Mesh previewMesh;
    Mesh collisionMesh;
    Mesh[] lodMeshes;

    ComputeBuffer vertexBuffer;

    bool shapeSettingsUpdated;
    bool shadingNoiseSettingsUpdated;
    Camera cam;

    Vector2 heightMinMax;

    int activeLODIndex = -1;
    MeshFilter terrainMeshFilter;
    Material terrainMatInstance;

    static Dictionary<int, MeshData> sphereGenerators;

    void Start()
    {
        if (InGameMode)
        {
            cam = Camera.main;
            HandleGameModeGeneration();
        }
    }

    void Update()
    {
        if (InEditMode)
            HandleEditModeGeneration();
    }

    // Handles creation of celestial body when entering game mode
    // This differs from the edit-mode version in the following ways:
    // • creates all LOD meshes and stores them in mesh array (to be picked based on player position)
    // • creates its own instances of materials so multiple bodies can exist with their own shading
    // • doesn't support updating of shape/shading values once generated
    void HandleGameModeGeneration()
    {
        if (CanGenerateMesh())
        {
            Dummy();

            lodMeshes = new Mesh[ResolutionSettings.numLODLevels];
            for (int i = 0; i < lodMeshes.Length; i++)
            {
                Vector2 lodTerrainHeightMinMax = GenerateTerrainMesh(ref lodMeshes[i], resolutionSettings.GetLODResolution(i));
                if (i == 0)
                    heightMinMax = lodTerrainHeightMinMax;
            }

            GenerateCollisionMesh(resolutionSettings.collider);

            terrainMatInstance = new Material(body.shading.terrainMaterial);
            body.shading.Initialize(body.shape);
            body.shading.SetTerrainProperties(terrainMatInstance, heightMinMax, BodyScale);
            GameObject terrainHolder = GetOrCreateMeshObject("Terrain Mesh", null, terrainMatInstance);
            terrainMeshFilter = terrainHolder.GetComponent<MeshFilter>();

            if (!terrainHolder.TryGetComponent<MeshCollider>(out MeshCollider collider))
                collider = terrainHolder.AddComponent<MeshCollider>();

            var collisionBakeTimer = System.Diagnostics.Stopwatch.StartNew();
            MeshBaker.BakeMeshImmediate(collisionMesh);
            collider.sharedMesh = collisionMesh;
            LogTimer(collisionBakeTimer, "Mesh collider");

        }
        else
            Debug.Log("Could not generate mesh");

        ReleaseAllBuffers();
    }

    // Handles creation of celestial body in the editor
    // This allows for updating the shape/shading settings
    void HandleEditModeGeneration()
    {
        if (InEditMode)
        {
            ComputeHelper.ShouldReleaseEditModeBuffers -= ReleaseAllBuffers;
            ComputeHelper.ShouldReleaseEditModeBuffers += ReleaseAllBuffers;
        }

        if (CanGenerateMesh())
        {
            if (shapeSettingsUpdated)
            {
                shapeSettingsUpdated = false;
                shadingNoiseSettingsUpdated = false;
                Dummy();

                var terrainMeshTimer = System.Diagnostics.Stopwatch.StartNew();
                heightMinMax = GenerateTerrainMesh(ref previewMesh, PickTerrainRes());

                LogTimer(terrainMeshTimer, "Generate terrain mesh");
            }
            else if (shadingNoiseSettingsUpdated)
            {
                shadingNoiseSettingsUpdated = false;
                ComputeHelper.CreateStructuredBuffer<Vector3>(ref vertexBuffer, previewMesh.vertices);
                body.shading.Initialize(body.shape);
                Vector4[] shadingData = body.shading.GenerateShadingData(vertexBuffer);
                previewMesh.SetUVs(0, shadingData);

                // Sometimes when changing a colour property, invalid data is returned from compute shader
                // Running the shading a second time fixes it.
                // Not sure if this is my bug, or Unity's (TODO: investigate)
                debug_numUpdates++;
                if (debugDoubleUpdate && debug_numUpdates < 2)
                {
                    shadingNoiseSettingsUpdated = true;
                    HandleEditModeGeneration();
                }
                if (debug_numUpdates == 2)
                    debug_numUpdates = 0;

            }
        }

        if (body.shading)
        {
            body.shading.Initialize(body.shape);
            body.shading.SetTerrainProperties(body.shading.terrainMaterial, heightMinMax, BodyScale);
        }

        ReleaseAllBuffers();
    }

    public void SetLOD(int lodIndex)
    {
        if (lodIndex != activeLODIndex && terrainMeshFilter)
        {
            activeLODIndex = lodIndex;
            terrainMeshFilter.sharedMesh = lodMeshes[lodIndex];
        }
    }

    public void OnShapeSettingChanged()
    {
        shapeSettingsUpdated = true;
    }

    public void OnShadingNoiseSettingChanged()
    {
        shadingNoiseSettingsUpdated = true;
    }

    void OnValidate()
    {
        if (body)
        {
            if (body.shape)
            {
                body.shape.OnSettingChanged -= OnShapeSettingChanged;
                body.shape.OnSettingChanged += OnShapeSettingChanged;
            }
            if (body.shading)
            {
                body.shading.OnSettingChanged -= OnShadingNoiseSettingChanged;
                body.shading.OnSettingChanged += OnShadingNoiseSettingChanged;
            }
        }

        resolutionSettings?.ClampResolutions();
        OnShapeSettingChanged();
    }

    void Dummy()
    {
        // Crude fix for a problem I was having where the values in the vertex buffer were *occasionally* all zero at start of game
        // This function runs the compute shader once with single dummy input, after which it seems the problem doesn't occur
        // (Waiting until Time.frameCount > 3 before generating is another gross hack that seems to fix the problem)
        // I don't know why...
        Vector3[] vertices = new Vector3[] { Vector3.zero };
        ComputeHelper.CreateStructuredBuffer<Vector3>(ref vertexBuffer, vertices);
        body.shape.CalculateHeights(vertexBuffer);
    }

    // Generates terrain mesh based on heights generated by the Shape object
    // Shading data from the Shading object is stored in the mesh uvs
    // Returns the min/max height of the terrain
    Vector2 GenerateTerrainMesh(ref Mesh mesh, int resolution)
    {
        var (vertices, triangles) = CreateSphereVertsAndTris(resolution);
        ComputeHelper.CreateStructuredBuffer<Vector3>(ref vertexBuffer, vertices);

        float edgeLength = (vertices[triangles[0]] - vertices[triangles[1]]).magnitude;

        float[] heights = body.shape.CalculateHeights(vertexBuffer);

        if (body.shape.perturbVertices && body.shape.perturbCompute)
        {
            ComputeShader perturbShader = body.shape.perturbCompute;
            float maxperturbStrength = body.shape.perturbStrength * edgeLength / 2;

            perturbShader.SetBuffer(0, "points", vertexBuffer);
            perturbShader.SetInt("numPoints", vertices.Length);
            perturbShader.SetFloat("maxStrength", maxperturbStrength);

            ComputeHelper.Run(perturbShader, vertices.Length);
            vertexBuffer.GetData(vertices);
        }

        float minHeight = heights.Min();
        float maxHeight = heights.Max();
        for (int i = 0; i < heights.Length; i++)
        {
            float height = heights[i];
            vertices[i] *= height;
        }

        CreateMesh(ref mesh, vertices.Length);
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0, true);
        mesh.RecalculateNormals();

        body.shading.Initialize(body.shape);
        Vector4[] shadingData = body.shading.GenerateShadingData(vertexBuffer);
        mesh.SetUVs(0, shadingData);

        var normals = mesh.normals;
        var crudeTangents = new Vector4[mesh.vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 normal = normals[i];
            crudeTangents[i] = new Vector4(-normal.z, 0, normal.x, 1);
        }
        mesh.SetTangents(crudeTangents);

        return new Vector2(minHeight, maxHeight);
    }

    void GenerateCollisionMesh(int resolution)
    {
        var (vertices, triangles) = CreateSphereVertsAndTris(resolution);
        ComputeHelper.CreateStructuredBuffer<Vector3>(ref vertexBuffer, vertices);

        float[] heights = body.shape.CalculateHeights(vertexBuffer);
        for (int i = 0; i < vertices.Length; i++)
        {
            float height = heights[i];
            vertices[i] *= height;
        }

        CreateMesh(ref collisionMesh, vertices.Length);
        collisionMesh.vertices = vertices;
        collisionMesh.triangles = triangles;
    }

    void CreateMesh(ref Mesh mesh, int numVertices)
    {
        const int vertexLimit16Bit = 1 << 16 - 1;
        if (mesh == null)
            mesh = new Mesh();
        else
            mesh.Clear();

        mesh.indexFormat = (numVertices < vertexLimit16Bit) ? UnityEngine.Rendering.IndexFormat.UInt16 : UnityEngine.Rendering.IndexFormat.UInt32;
    }

    // Gets child object with specified name.
    // If it doesn't exist, then creates object with that name, adds mesh renderer/filter and attaches mesh and material
    GameObject GetOrCreateMeshObject(string name, Mesh mesh, Material material)
    {
        var child = transform.Find(name);
        if (!child)
        {
            child = new GameObject(name).transform;
            child.parent = transform;
            child.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            child.localScale = Vector3.one;
            child.gameObject.layer = gameObject.layer;
        }

        if (!child.TryGetComponent<MeshFilter>(out MeshFilter filter))
            filter = child.gameObject.AddComponent<MeshFilter>();

        filter.sharedMesh = mesh;

        if (!child.TryGetComponent<MeshRenderer>(out MeshRenderer renderer))
            renderer = child.gameObject.AddComponent<MeshRenderer>();

        renderer.sharedMaterial = material;

        return child.gameObject;
    }

    public int PickTerrainRes()
    {
        if (!Application.isPlaying)
        {
            switch (previewMode)
            {
                case PreviewMode.LOD0:
                    return resolutionSettings.lod0;
                case PreviewMode.LOD1:
                    return resolutionSettings.lod1;
                case PreviewMode.LOD2:
                    return resolutionSettings.lod2;
                case PreviewMode.CollisionRes:
                    return resolutionSettings.collider;
            }
        }

        return 0;

    }

    public float GetOceanRadius()
    {
        if (!body.shading.hasOcean)
            return 0;

        return UnscaledOceanRadius * BodyScale;
    }

    float UnscaledOceanRadius
    {
        get
        {
            return Mathf.Lerp(heightMinMax.x, 1, body.shading.oceanLevel);
        }
    }

    public float BodyScale
    {
        get
        {
            return transform.localScale.x;
        }
    }

    // Generate sphere (or reuse if already generated) and return a copy of the vertices and triangles
    (Vector3[] vertices, int[] triangles) CreateSphereVertsAndTris(int resolution)
    {
        sphereGenerators ??= new Dictionary<int, MeshData>();

        if (!sphereGenerators.ContainsKey(resolution))
            sphereGenerators.Add(resolution, SphereMesh.GenerateMeshData(resolution));

        var generator = sphereGenerators[resolution];

        var vertices = new Vector3[generator.vertices.Length];
        var triangles = new int[generator.triangles.Length];
        System.Array.Copy(generator.vertices, vertices, vertices.Length);
        System.Array.Copy(generator.triangles, triangles, triangles.Length);
        return (vertices, triangles);
    }

    void ReleaseAllBuffers()
    {
        ComputeHelper.Release(vertexBuffer);
        if (body.shape)
            body.shape.ReleaseBuffers();
        if (body.shading)
            body.shading.ReleaseBuffers();
    }

    void OnDestroy()
    {
        ReleaseAllBuffers();
    }

    bool CanGenerateMesh()
    {
        return ComputeHelper.CanRunEditModeCompute && body.shape && body.shape.heightMapCompute;
    }

    void LogTimer(System.Diagnostics.Stopwatch sw, string text)
    {
        if (logTimers)
            Debug.Log(text + " " + sw.ElapsedMilliseconds + " ms.");
    }

    bool InGameMode
    {
        get
        {
            return Application.isPlaying;
        }
    }

    bool InEditMode
    {
        get
        {
            return !Application.isPlaying;
        }
    }

    public class TerrainData
    {
        public float[] heights;
        public Vector4[] uvs;
    }

    [System.Serializable]
    public class ResolutionSettings
    {

        public const int numLODLevels = 3;
        const int maxAllowedResolution = 500;

        public int lod0 = 300;
        public int lod1 = 100;
        public int lod2 = 50;
        public int collider = 100;

        public int GetLODResolution(int lodLevel)
        {
            switch (lodLevel)
            {
                case 0:
                    return lod0;
                case 1:
                    return lod1;
                case 2:
                    return lod2;
            }
            return lod2;
        }

        public void ClampResolutions()
        {
            lod0 = Mathf.Min(maxAllowedResolution, lod0);
            lod1 = Mathf.Min(maxAllowedResolution, lod1);
            lod2 = Mathf.Min(maxAllowedResolution, lod2);
            collider = Mathf.Min(maxAllowedResolution, collider);
        }
    }
}