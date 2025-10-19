using System;
using UnityEngine;

[System.Serializable]
public class MeshData
{
    public string name;

    public int[] triangles = new int[0];
    public Vector3[] vertices = new Vector3[0];
    public Vector3[] normals = new Vector3[0];
    public Vector4[] texCoords = new Vector4[0];

    public MeshData(string name)
    {
        this.name = name;
    }

    public MeshData(Vector3[] vertices, int[] triangles, Vector3[] normals, Vector4[] texCoords, string name = "Mesh")
    {
        this.vertices = vertices;
        this.triangles = triangles;
        this.normals = normals;
        this.texCoords = texCoords;
        this.name = name;
    }

    public MeshData(Vector3[] vertices, int[] triangles, Vector3[] normals, string name = "Mesh")
    {
        this.vertices = vertices;
        this.triangles = triangles;
        this.normals = normals;
        this.name = name;
    }

    public MeshData(Vector3[] vertices, int[] triangles, string name = "Mesh")
    {
        this.vertices = vertices;
        this.triangles = triangles;
        this.name = name;
    }

    public Mesh ToMesh()
    {
        return MeshHelper.CreateMesh(this);
    }

    public void ToMesh(ref Mesh mesh)
    {
        MeshHelper.CreateMesh(ref mesh, this, false);
    }

    public byte[] ToBytes()
    {
        return MeshSerializer.MeshToBytes(this);
    }

    public static MeshData FromBytes(byte[] bytes)
    {
        return MeshSerializer.BytesToMesh(bytes);
    }

    public void Optimize()
    {
        Mesh mesh = ToMesh();
        mesh.Optimize();
        vertices = mesh.vertices;
        triangles = mesh.triangles;
        normals = mesh.normals;
        var reorderedUVs = new System.Collections.Generic.List<Vector4>();
        mesh.GetUVs(0, reorderedUVs);
        texCoords = reorderedUVs.ToArray();
    }

    public void RemoveVertex(int vertexIndex)
    {
        int write = 0;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int a = triangles[i];
            int b = triangles[i + 1];
            int c = triangles[i + 2];

            if (a != vertexIndex && b != vertexIndex && c != vertexIndex)
            {
                triangles[write++] = a;
                triangles[write++] = b;
                triangles[write++] = c;
            }
        }

        if (write != triangles.Length)
            Array.Resize(ref triangles, write);
    }

    public void RecalculateNormals()
    {
        Mesh mesh = ToMesh();
        mesh.RecalculateNormals();
        this.normals = mesh.normals;
    }

    public void Combine(MeshData other)
    {
        MeshData combinedMesh = Combine(this, other);
        this.vertices = combinedMesh.vertices;
        this.triangles = combinedMesh.triangles;
        this.normals = combinedMesh.normals;
        this.texCoords = combinedMesh.texCoords;
    }

    public static MeshData Combine(MeshData a, MeshData b, string newName = "Combined Mesh")
    {
        Vector3[] combinedVertices = CombineArrays(a.vertices, b.vertices);
        int[] combinedTriangles = new int[a.triangles.Length + b.triangles.Length];

        System.Array.Copy(a.triangles, combinedTriangles, a.triangles.Length);
        for (int i = 0; i < b.triangles.Length; i++)
        {
            combinedTriangles[i + a.triangles.Length] = b.triangles[i] + a.vertices.Length;
        }

        Vector3[] combinedNormals = CombineArraysEnforceLength(a.normals, b.normals, combinedVertices.Length);
        Vector4[] combinedTexCoords = CombineArraysEnforceLength(a.texCoords, b.texCoords, combinedVertices.Length);

        return new MeshData(combinedVertices, combinedTriangles, combinedNormals, combinedTexCoords, newName);

        T[] CombineArraysEnforceLength<T>(T[] arrayA, T[] arrayB, int length)
        {
            if (arrayA.Length == 0 && arrayB.Length == 0)
                return new T[0];
            else
            {
                if (arrayA.Length == 0)
                    arrayA = new T[length - arrayB.Length];
                else if (arrayB.Length == 0)
                    arrayB = new T[length - arrayA.Length];
            }
            return CombineArrays(arrayA, arrayB);
        }

        T[] CombineArrays<T>(T[] arrayA, T[] arrayB)
        {
            T[] combinedArray = new T[arrayA.Length + arrayB.Length];
            System.Array.Copy(arrayA, 0, combinedArray, 0, arrayA.Length);
            System.Array.Copy(arrayB, 0, combinedArray, arrayA.Length, arrayB.Length);
            return combinedArray;
        }
    }
}