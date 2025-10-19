using System.Collections.Generic;
using UnityEngine;

public class CityLightsGenerator : MonoBehaviour
{
    public enum Mode { Generate, Load }
    public Mode mode;

    [Header("Generation Settings")]
    public int numInstances;
    public ComputeShader compute;
    public Texture2D lightMap;
    public TerrainHeightSettings heightSettings;
    public Texture2D heightMap;

    public ComputeBuffer allLights;

    [Header("Processing")]
    public bool removeDuplicates;
    public int duplicatePrecision;

    [Header("Save/Load")]
    public string saveName;
    public TextAsset loadFile;

    [Header("Debug")]
    public Shader debugShader;
    public float debugSize = 1;
    ComputeBuffer argsBuffer;
    Material debugMat;
    Mesh debugMesh;

    void Awake()
    {
        if (mode == Mode.Generate)
        {
            Generate();
            CreateDebugVis();
        }
        else if (mode == Mode.Load)
            Load();

        //ComputeHelper.Release(allLights);
    }

    void Generate()
    {
        ComputeHelper.CreateStructuredBuffer<CityLight>(ref allLights, numInstances);

        compute.SetTexture(0, "LightMap", lightMap);
        compute.SetTexture(0, "HeightMap", heightMap);
        compute.SetBuffer(0, "CityLights", allLights);
        compute.SetInt("numLights", numInstances);
        compute.SetFloat("worldRadius", heightSettings.worldRadius);
        compute.SetFloat("heightMultiplier", heightSettings.heightMultiplier);
        ComputeHelper.Run(compute, numInstances);

        Process();
    }

    void Process()
    {
        if (removeDuplicates)
        {
            CityLight[] cityLights = new CityLight[allLights.count];
            allLights.GetData(cityLights);

            HashSet<Vector3Int> dupe = new();
            List<CityLight> filtered = new();

            for (int i = 0; i < cityLights.Length; i++)
            {
                Vector3 p = cityLights[i].pointOnSphere * duplicatePrecision;
                Vector3Int pQuant = new((int)p.x, (int)p.y, (int)p.z);

                if (!dupe.Contains(pQuant))
                {
                    dupe.Add(pQuant);
                    filtered.Add(cityLights[i]);
                }
            }

            Debug.Log($"Removed {cityLights.Length - filtered.Count} duplicate points");
            cityLights = filtered.ToArray();
            ComputeHelper.CreateStructuredBuffer(ref allLights, cityLights);

            CreateDebugVis();
        }
    }

    void CreateDebugVis()
    {
        if (debugMat == null)
            debugMat = new Material(debugShader);

        debugMesh = SphereMesh.GenerateMeshData(1, 0.5f).ToMesh();
        ComputeHelper.CreateArgsBuffer(ref argsBuffer, debugMesh, allLights.count);
        debugMat.SetBuffer("CityLights", allLights);
    }

    void Update()
    {
        debugMat.SetFloat("size", debugSize);
        Graphics.DrawMeshInstancedIndirect(debugMesh, 0, debugMat, new Bounds(Vector3.zero, Vector3.one * 1000), argsBuffer, camera: null, castShadows: UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows: false);
    }

    [NaughtyAttributes.Button("Save To File", NaughtyAttributes.EButtonEnableMode.Playmode)]
    void SaveToFile()
    {
        CityLight[] cityLights = new CityLight[allLights.count];
        allLights.GetData(cityLights);

        CityLightGroup[] groups = CreateGroups(cityLights);

        string saveString = JsonHelper.ArrayToJson(groups);
        FileHelper.SaveTextToFile(SavePath, saveName, "json", saveString, log: true);
    }

    [NaughtyAttributes.Button("Load", NaughtyAttributes.EButtonEnableMode.Playmode)]
    void Load()
    {
        Debug.Log("Loading city lights from " + loadFile.name);
        CityLightGroup[] cityLightsGroups = LoadFromFile(loadFile);

        List<CityLight> cityLights = new();

        foreach (var group in cityLightsGroups)
            cityLights.AddRange(group.cityLights);

        ComputeHelper.CreateStructuredBuffer(ref allLights, cityLights.ToArray());
        CreateDebugVis();
    }

    public static CityLightGroup[] LoadFromFile(TextAsset loadFile)
    {
        return JsonHelper.ArrayFromJson<CityLightGroup>(loadFile.text);
    }

    CityLightGroup[] CreateGroups(CityLight[] cityLights)
    {
        Vector3[] nodes = SphereMesh.GenerateMeshData(resolution: 2).vertices;
        var groups = new List<List<CityLight>>();

        for (int i = 0; i < nodes.Length; i++)
            groups.Add(new List<CityLight>());

        PopulateGroups();
        MergeSmallGroups();
        RemoveEmptyGroups();

        Bounds[] bounds = CreateGroupBounds();

        CityLightGroup[] cityLightGroups = new CityLightGroup[groups.Count];

        for (int i = 0; i < cityLightGroups.Length; i++)
            cityLightGroups[i] = new CityLightGroup() { bounds = bounds[i], cityLights = groups[i].ToArray() };

        return cityLightGroups;


        void PopulateGroups()
        {
            for (int i = 0; i < cityLights.Length; i++)
            {
                float nearestNodeSqrDst = float.MaxValue;
                int nearestNodeIndex = 0;

                for (int nodeIndex = 0; nodeIndex < nodes.Length; nodeIndex++)
                {
                    float sqrDst = (cityLights[i].pointOnSphere - nodes[nodeIndex]).sqrMagnitude;
                    if (sqrDst < nearestNodeSqrDst)
                    {
                        nearestNodeSqrDst = sqrDst;
                        nearestNodeIndex = nodeIndex;
                    }
                }

                groups[nearestNodeIndex].Add(cityLights[i]);
            }

        }

        void MergeSmallGroups()
        {
            const int smallGroupThreshold = 4000;
            const int mergedGroupThreshold = smallGroupThreshold + 1000;

            for (int i = 0; i < groups.Count; i++)
            {
                var a = groups[i];
                if (a.Count > 0 && a.Count < smallGroupThreshold)
                {
                    float bestMergeGroupDst = float.MaxValue;
                    int bestMergeGroupIndex = -1;
                    for (int j = i + 1; j < groups.Count; j++)
                    {
                        var b = groups[j];

                        if (b.Count > 0 && (a.Count + b.Count) < mergedGroupThreshold)
                        {
                            float dst = (nodes[i] - nodes[j]).magnitude;

                            if (dst < bestMergeGroupDst)
                            {
                                bestMergeGroupDst = dst;
                                bestMergeGroupIndex = j;
                            }
                        }
                    }

                    if (bestMergeGroupIndex != -1)
                    {
                        groups[bestMergeGroupIndex].AddRange(a);
                        a.Clear();
                    }
                }
            }
        }

        void RemoveEmptyGroups()
        {
            for (int i = groups.Count - 1; i >= 0; i--)
            {
                if (groups[i].Count == 0)
                    groups.RemoveAt(i);
            }
        }

        Bounds[] CreateGroupBounds()
        {
            Bounds[] bounds = new Bounds[groups.Count];

            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                for (int i = 0; i < groups[groupIndex].Count; i++)
                {
                    CityLight cityLight = groups[groupIndex][i];
                    Vector3 point = cityLight.pointOnSphere * cityLight.height;

                    if (i == 0)
                        bounds[groupIndex] = new Bounds(point, Vector3.one);
                    else
                        bounds[groupIndex].Encapsulate(point);
                }
            }
            return bounds;
        }

    }

    protected string SavePath
    {
        get
        {
            return FileHelper.MakePath("Assets", "Data", "City Lights");
        }
    }

    void OnDestroy()
    {
        ComputeHelper.Release(allLights, argsBuffer);
        Destroy(debugMat);
    }
}

[System.Serializable]
public struct CityLight
{
    public Vector3 pointOnSphere;
    public float height;
    public float intensity;
    public float randomT;
}

[System.Serializable]
public struct CityLightGroup
{
    public Bounds bounds;
    public CityLight[] cityLights;
}
