using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class StarFieldRenderer : MonoBehaviour
{
    // --- URP Change: Static list to hold all active instances ---
    public static readonly List<StarFieldRenderer> Instances = new();

    [Header("Star Field Settings")]
    public int randomSeed = 8;
    public int starCount = 3000;
    public int verticesPerStar = 10; // Reduced for performance, 100 is overkill for tiny stars
    public Vector2 starSizeRange = new(0, 300);
    public float minStarBrightness = 0;
    public float maxStarBrightness = 1;
    public float starFieldRadius = 100000;
    public float dayNightFadeThreshold = 4f;

    [Header("Rendering Material & Gradient")]
    public Material starMaterial;
    public Gradient starColorGradient;

    // Public accessors for the render feature
    public Mesh StarMesh { get; private set; }
    public Material StarMaterial => starMaterial;

    private OceanMaskRenderer oceanMaskRenderer;
    private Texture2D gradientTexture;
    private bool needsMeshRebuild = true;
    private GradientColorKey[] previousColorKeys;

    void OnEnable()
    {
        // --- URP Change: Register this instance ---
        Instances.Add(this);

        // --- Simplified Awake/Start Logic ---
        oceanMaskRenderer = FindFirstObjectByType<OceanMaskRenderer>();

        StarMesh = new Mesh
        {
            name = "StarFieldMesh",
            indexFormat = IndexFormat.UInt32
        };
        StarMesh.MarkDynamic();
        GetComponent<MeshFilter>().sharedMesh = StarMesh;

        var meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = starMaterial;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        TextureHelper.TextureFromGradient(starColorGradient, 64, ref gradientTexture);
        starMaterial.SetTexture("_Spectrum", gradientTexture);
        previousColorKeys = starColorGradient.colorKeys;

        BuildStarFieldMesh();
    }

    void OnDisable()
    {
        // --- URP Change: Unregister this instance ---
        Instances.Remove(this);
    }

    void OnDestroy()
    {
        if (StarMesh != null) Destroy(StarMesh);
        if (gradientTexture != null) Destroy(gradientTexture);
    }

    void OnValidate()
    {
        needsMeshRebuild = true;
    }

    void Update()
    {
        if (needsMeshRebuild)
        {
            BuildStarFieldMesh();
            needsMeshRebuild = false;
        }

        // Keep material properties updated
        starMaterial.SetFloat("daytimeFade", dayNightFadeThreshold);
        if (oceanMaskRenderer)
        {
            starMaterial.SetTexture("_OceanMask", oceanMaskRenderer.oceanMaskTexture);
        }

        if (HasGradientChanged())
        {
            TextureHelper.TextureFromGradient(starColorGradient, 64, ref gradientTexture);
            starMaterial.SetTexture("_Spectrum", gradientTexture);
        }

        // --- URP Change: No longer need to handle camera directly ---
        // The object will be drawn relative to the camera automatically.
        // Make sure this GameObject is a child of the main camera to follow it.
    }

    private bool HasGradientChanged()
    {
        var currentKeys = starColorGradient.colorKeys;
        if (previousColorKeys.Length != currentKeys.Length)
        {
            previousColorKeys = currentKeys;
            return true;
        }
        for (int i = 0; i < currentKeys.Length; i++)
        {
            if (currentKeys[i].color != previousColorKeys[i].color || !Mathf.Approximately(currentKeys[i].time, previousColorKeys[i].time))
            {
                previousColorKeys = currentKeys;
                return true;
            }
        }
        return false;
    }

    private void BuildStarFieldMesh()
    {
        if (StarMesh == null) return;
        StarMesh.Clear();

        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var uvCoordinates = new List<Vector2>();

        Random.InitState(randomSeed);
        for (int i = 0; i < starCount; i++)
        {
            Vector3 direction = Random.onUnitSphere;
            var (circleVerts, circleTris, circleUvs) = GenerateStarCircle(direction, vertices.Count);
            vertices.AddRange(circleVerts);
            triangles.AddRange(circleTris);
            uvCoordinates.AddRange(circleUvs);
        }

        StarMesh.SetVertices(vertices);
        StarMesh.SetTriangles(triangles, 0);
        StarMesh.SetUVs(0, uvCoordinates);
        StarMesh.UploadMeshData(false);
    }

    // GenerateStarCircle method remains unchanged...
    private (Vector3[] circleVertices, int[] circleTriangles, Vector2[] circleUVs) GenerateStarCircle(Vector3 direction, int vertexOffset)
    {
        float size = Random.Range(starSizeRange.x, starSizeRange.y);
        float brightness = Random.Range(minStarBrightness, maxStarBrightness);
        float spectrumT = Random.value;

        Vector3 axisA = Vector3.Cross(direction, Vector3.up).normalized;
        if (axisA == Vector3.zero)
            axisA = Vector3.Cross(direction, Vector3.forward).normalized;

        Vector3 axisB = Vector3.Cross(direction, axisA);
        Vector3 center = direction * starFieldRadius;
        int vertCount = verticesPerStar + 1;

        var circleVertices = new Vector3[vertCount];
        var circleUVs = new Vector2[vertCount];
        var circleTriangles = new int[verticesPerStar * 3];

        circleVertices[0] = center;
        circleUVs[0] = new Vector2(brightness, spectrumT); // UV.x = brightness, UV.y = color lookup

        for (int vi = 0; vi < verticesPerStar; vi++)
        {
            float angle = vi / (float)verticesPerStar * Mathf.PI * 2f;
            Vector3 offset = axisA * Mathf.Sin(angle) + axisB * Mathf.Cos(angle);
            circleVertices[vi + 1] = center + offset * size;
            circleUVs[vi + 1] = new Vector2(0f, spectrumT);

            int triIndex = vi * 3;
            circleTriangles[triIndex + 0] = vertexOffset;
            circleTriangles[triIndex + 1] = vertexOffset + vi + 1;
            circleTriangles[triIndex + 2] = vertexOffset + ((vi + 1) % verticesPerStar) + 1;
        }

        return (circleVertices, circleTriangles, circleUVs);
    }
}