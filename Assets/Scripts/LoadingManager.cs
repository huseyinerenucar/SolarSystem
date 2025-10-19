using System.Collections.Generic;
using TerrainGeneration;
using UnityEngine;

public class LoadingManager : MonoBehaviour
{
    [Header("References")]
    public Light sunLight;
    public AtmosphereEffect atmosphereEffect;

    public TerrainHeightProcessor heightProcessor;
    public MeshLoader terrainLoader;
    public MeshLoader oceanLoader;

    void Awake()
    {
        Load();
    }

    public LoadTask[] GetTasks()
    {
        List<LoadTask> tasks = new();

        AddTask(() => heightProcessor.ProcessHeightMap(), "Processing Height Map");
        AddTask(() => terrainLoader.Load(), "Loading Terrain Mesh");
        AddTask(() => oceanLoader.Load(), "Loading Ocean Mesh");

        void AddTask(System.Action task, string name)
        {
            tasks.Add(new LoadTask(task, name));
        }

        return tasks.ToArray();
    }

    void Load()
    {
        foreach (LoadTask task in GetTasks())
            task.Execute();

        heightProcessor.Release();
        Resources.UnloadUnusedAssets();
    }

    public class LoadTask
    {
        public System.Action task;
        public string taskName;

        public LoadTask(System.Action task, string taskName)
        {
            this.task = task;
            this.taskName = taskName;
        }

        public long Execute()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            task.Invoke();

            return sw.ElapsedMilliseconds;
        }
    }
}
