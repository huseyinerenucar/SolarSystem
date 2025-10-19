using UnityEngine;

public class CelestialBodyPlaceholder : MonoBehaviour
{
    public int terrainResolution = 50;
    public Material material;
    public bool useBodySettings;
    public CelestialBodySettings bodySettings;
    public bool generateCollider;
    Mesh mesh;

    bool settingsChanged;

    void Update()
    {
        if (settingsChanged)
        {
            settingsChanged = false;
            if (mesh == null)
                mesh = new Mesh();
            else
                mesh.Clear();

            MeshData s = SphereMesh.GenerateMeshData(terrainResolution);
            mesh.vertices = s.vertices;
            mesh.triangles = s.triangles;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            var g = GetOrCreateMeshObject("Mesh", mesh, material);
            if (generateCollider)
            {
                if (!g.GetComponent<MeshCollider>())
                    g.AddComponent<MeshCollider>();
                g.GetComponent<MeshCollider>().sharedMesh = mesh;
            }
        }
    }

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

    void OnValidate()
    {
        settingsChanged = true;
    }
}
