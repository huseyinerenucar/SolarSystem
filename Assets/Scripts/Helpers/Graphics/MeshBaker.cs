using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;

public static class MeshBaker
{

    static readonly Dictionary<Mesh, JobHandle> jobsByMesh;

    static MeshBaker()
    {
        jobsByMesh = new Dictionary<Mesh, JobHandle>();
    }

    public static void BakeMeshImmediate(Mesh mesh)
    {
        var job = new BakeJob(mesh.GetInstanceID());
        JobHandle currentJob = job.Schedule();
        currentJob.Complete();
    }

    public static void StartBakingMesh(Mesh mesh)
    {
        var job = new BakeJob(mesh.GetInstanceID());
        JobHandle currentJob = job.Schedule();
        jobsByMesh.Add(mesh, currentJob);
    }

    public static void EnsureBakingComplete(Mesh mesh)
    {
        if (jobsByMesh.ContainsKey(mesh))
        {
            jobsByMesh[mesh].Complete();
            jobsByMesh.Remove(mesh);
        }
        else
            BakeMeshImmediate(mesh);
    }

}

public struct BakeJob : IJob
{

    readonly int meshID;
    public BakeJob(int meshID)
    {
        this.meshID = meshID;
    }

    public readonly void Execute()
    {
        Physics.BakeMesh(meshID, false);
    }
}