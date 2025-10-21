using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;

/// <summary>
/// Advanced N-body simulation with multiple optimization strategies
/// Features:
/// - Double precision (Vector3D) for long-term stability
/// - Barnes-Hut octree for O(n log n) complexity
/// - Unity Job System + Burst for multi-threading
/// - Multiple integration methods (Euler, Velocity Verlet, RK4)
/// - Adaptive timestep for stability
/// </summary>
public class OptimizedNBodySimulation : MonoBehaviour
{
    [Header("Simulation Method")]
    [Tooltip("Algorithm used for force calculation")]
    public ForceCalculationMethod forceMethod = ForceCalculationMethod.BarnesHut;

    [Tooltip("Integration method for updating positions/velocities")]
    public IntegrationMethod integrationMethod = IntegrationMethod.VelocityVerlet;

    [Header("Barnes-Hut Settings")]
    [Range(0.1f, 1.0f)]
    [Tooltip("Lower = more accurate but slower. 0.5 is typical.")]
    public float barnesHutTheta = 0.5f;

    [Header("Job System Settings")]
    [Tooltip("Use Burst-compiled parallel jobs for massive speedup")]
    public bool useJobSystem = true;

    [Range(1, 128)]
    [Tooltip("Bodies processed per job batch (tune for your CPU)")]
    public int jobBatchSize = 32;

    [Header("Adaptive Timestep")]
    [Tooltip("Automatically adjust timestep based on acceleration")]
    public bool useAdaptiveTimestep = false;

    [Range(0.001f, 1.0f)]
    public float minTimestep = 0.001f;

    [Range(0.01f, 10.0f)]
    public float maxTimestep = 0.1f;

    [Tooltip("Factor for adaptive timestep calculation")]
    public float adaptiveTimestepFactor = 0.01f;

    [Header("Stability")]
    [Tooltip("Softening length prevents singularities when bodies collide")]
    public double softeningLength = 0.01;

    [Header("Debug")]
    [Tooltip("Visualize Barnes-Hut octree structure")]
    public bool drawOctree = false;

    [SerializeField] private ScriptableVariables scriptableVariables;

    // Internal state
    private CelestialBody[] bodies;
    private BarnesHutOctree octree;

    // Job System native arrays
    private NativeArray<double3> positions;
    private NativeArray<double3> velocities;
    private NativeArray<double3> accelerations;
    private NativeArray<double3> oldAccelerations;
    private NativeArray<double> masses;
    private NativeArray<double> adaptiveTimestepArray;

    // RK4 intermediate arrays
    private NativeArray<double3> k1Pos, k2Pos, k3Pos, k4Pos;
    private NativeArray<double3> k1Vel, k2Vel, k3Vel, k4Vel;
    private NativeArray<double3> tempPositions, tempVelocities;

    private bool nativeArraysInitialized = false;
    private double currentAdaptiveTimestep;

    public enum ForceCalculationMethod
    {
        BruteForce,     // O(n²) - simple but slow
        BarnesHut,      // O(n log n) - fast and accurate
        JobSystem       // O(n²) but highly parallel - good for moderate counts
    }

    public enum IntegrationMethod
    {
        Euler,          // Fastest, least accurate
        VelocityVerlet, // Good balance of speed and accuracy
        RK4             // Slowest, most accurate
    }

    void Awake()
    {
        bodies = FindObjectsByType<CelestialBody>(FindObjectsSortMode.None);
        octree = new BarnesHutOctree { theta = barnesHutTheta };

        if (useJobSystem)
        {
            InitializeNativeArrays();
        }

        currentAdaptiveTimestep = StaticVariables.physicsTimeStep;
    }

    void OnDestroy()
    {
        DisposeNativeArrays();
    }

    void FixedUpdate()
    {
        if (bodies.Length == 0) return;

        double timeStep = CalculateTimestep();

        switch (forceMethod)
        {
            case ForceCalculationMethod.BruteForce:
                SimulateBruteForce(timeStep);
                break;

            case ForceCalculationMethod.BarnesHut:
                SimulateBarnesHut(timeStep);
                break;

            case ForceCalculationMethod.JobSystem:
                SimulateWithJobs(timeStep);
                break;
        }
    }

    /// <summary>
    /// Calculate timestep (adaptive or fixed)
    /// </summary>
    private double CalculateTimestep()
    {
        double baseTimestep = Time.fixedDeltaTime * scriptableVariables.currentTimeSpeed;

        if (!useAdaptiveTimestep)
        {
            return baseTimestep;
        }

        return currentAdaptiveTimestep * scriptableVariables.currentTimeSpeed;
    }

    #region Brute Force Simulation

    /// <summary>
    /// Simple O(n²) brute force simulation
    /// Good for small body counts (< 50)
    /// </summary>
    private void SimulateBruteForce(double timeStep)
    {
        // Calculate accelerations
        foreach (var body in bodies)
        {
            CalculateAccelerationBruteForce(body);
        }

        // Update velocities and positions using selected integration method
        switch (integrationMethod)
        {
            case IntegrationMethod.Euler:
                IntegrateEuler(timeStep);
                break;

            case IntegrationMethod.VelocityVerlet:
                IntegrateVelocityVerlet(timeStep);
                break;

            case IntegrationMethod.RK4:
                IntegrateRK4(timeStep);
                break;
        }

        // Update rotations
        foreach (var body in bodies)
        {
            body.UpdateRotation((float)timeStep);
        }
    }

    private void CalculateAccelerationBruteForce(CelestialBody celestialBody)
    {
        Vector3D acceleration = Vector3D.zero;

        foreach (var other in bodies)
        {
            if (other == celestialBody) continue;

            Vector3D direction = other.positionD - celestialBody.positionD;
            double distanceSqr = direction.sqrMagnitude + softeningLength * softeningLength;
            double distance = System.Math.Sqrt(distanceSqr);

            double forceMagnitude = StaticVariables.gravitationalConstant * other.mass / distanceSqr;
            acceleration += direction.normalized * forceMagnitude;
        }

        celestialBody.accelerationD = acceleration;
    }

    #endregion

    #region Barnes-Hut Simulation

    /// <summary>
    /// Optimized O(n log n) simulation using Barnes-Hut octree
    /// Excellent for large body counts (100+)
    /// </summary>
    private void SimulateBarnesHut(double timeStep)
    {
        // Rebuild octree
        octree.theta = barnesHutTheta;
        octree.Build(bodies);

        // Calculate accelerations using octree
        foreach (var body in bodies)
        {
            body.accelerationD = octree.CalculateAcceleration(body);
        }

        // Integrate
        switch (integrationMethod)
        {
            case IntegrationMethod.Euler:
                IntegrateEuler(timeStep);
                break;

            case IntegrationMethod.VelocityVerlet:
                IntegrateVelocityVerlet(timeStep);
                break;

            case IntegrationMethod.RK4:
                IntegrateRK4(timeStep);
                break;
        }

        // Update rotations
        foreach (var body in bodies)
        {
            body.UpdateRotation((float)timeStep);
        }
    }

    #endregion

    #region Job System Simulation

    /// <summary>
    /// Highly parallel simulation using Unity Job System + Burst
    /// Best for moderate body counts (50-500)
    /// </summary>
    private void SimulateWithJobs(double timeStep)
    {
        if (!nativeArraysInitialized)
        {
            InitializeNativeArrays();
        }

        // Copy data to native arrays
        NBodyJobsHelper.CopyBodiesToNativeArrays(bodies, positions, velocities, masses);

        // Calculate accelerations
        var accelJob = new CalculateAccelerationJob
        {
            positions = positions,
            masses = masses,
            gravitationalConstant = StaticVariables.gravitationalConstant,
            softeningLength = softeningLength,
            accelerations = accelerations
        };

        JobHandle accelHandle = accelJob.Schedule(bodies.Length, jobBatchSize);
        accelHandle.Complete();

        // Adaptive timestep calculation
        if (useAdaptiveTimestep)
        {
            var adaptiveTimestepJob = new CalculateAdaptiveTimestepJob
            {
                accelerations = accelerations,
                maxAccelerationFactor = adaptiveTimestepFactor,
                baseTimestep = Time.fixedDeltaTime,
                minTimestep = minTimestep,
                maxTimestep = maxTimestep,
                outputTimestep = adaptiveTimestepArray
            };

            adaptiveTimestepJob.Schedule().Complete();
            currentAdaptiveTimestep = adaptiveTimestepArray[0];
        }

        // Integration
        switch (integrationMethod)
        {
            case IntegrationMethod.Euler:
                IntegrateEulerJob(timeStep);
                break;

            case IntegrationMethod.VelocityVerlet:
                IntegrateVelocityVerletJob(timeStep);
                break;

            case IntegrationMethod.RK4:
                // RK4 with jobs is complex, fall back to CPU version
                NBodyJobsHelper.CopyNativeArraysToBodies(bodies, positions, velocities, accelerations);
                IntegrateRK4(timeStep);
                NBodyJobsHelper.CopyBodiesToNativeArrays(bodies, positions, velocities, masses);
                break;
        }

        // Copy results back
        NBodyJobsHelper.CopyNativeArraysToBodies(bodies, positions, velocities, accelerations);

        // Update rotations (not parallelized)
        foreach (var body in bodies)
        {
            body.UpdateRotation((float)timeStep);
        }
    }

    #endregion

    #region Integration Methods

    private void IntegrateEuler(double dt)
    {
        foreach (var body in bodies)
        {
            body.velocityD += body.accelerationD * dt;
            body.positionD += body.velocityD * dt;
            body.Rigidbody.MovePosition(body.positionD.ToVector3());
        }
    }

    private void IntegrateVelocityVerlet(double dt)
    {
        // Store old accelerations
        Vector3D[] oldAccels = new Vector3D[bodies.Length];
        for (int i = 0; i < bodies.Length; i++)
        {
            oldAccels[i] = bodies[i].accelerationD;
        }

        // Update positions
        foreach (var body in bodies)
        {
            body.positionD += body.velocityD * dt + 0.5 * body.accelerationD * dt * dt;
        }

        // Recalculate accelerations at new positions
        if (forceMethod == ForceCalculationMethod.BarnesHut)
        {
            octree.Build(bodies);
            foreach (var body in bodies)
            {
                body.accelerationD = octree.CalculateAcceleration(body);
            }
        }
        else
        {
            foreach (var body in bodies)
            {
                CalculateAccelerationBruteForce(body);
            }
        }

        // Update velocities using average of old and new accelerations
        for (int i = 0; i < bodies.Length; i++)
        {
            bodies[i].velocityD += 0.5 * (oldAccels[i] + bodies[i].accelerationD) * dt;
            bodies[i].Rigidbody.MovePosition(bodies[i].positionD.ToVector3());
        }
    }

    private void IntegrateRK4(double dt)
    {
        // Store initial state
        Vector3D[] pos0 = new Vector3D[bodies.Length];
        Vector3D[] vel0 = new Vector3D[bodies.Length];

        for (int i = 0; i < bodies.Length; i++)
        {
            pos0[i] = bodies[i].positionD;
            vel0[i] = bodies[i].velocityD;
        }

        // k1
        Vector3D[] k1v = new Vector3D[bodies.Length];
        for (int i = 0; i < bodies.Length; i++)
        {
            k1v[i] = bodies[i].accelerationD;
        }

        // k2
        for (int i = 0; i < bodies.Length; i++)
        {
            bodies[i].positionD = pos0[i] + vel0[i] * (dt * 0.5);
            bodies[i].velocityD = vel0[i] + k1v[i] * (dt * 0.5);
        }
        RecalculateAccelerations();
        Vector3D[] k2v = new Vector3D[bodies.Length];
        for (int i = 0; i < bodies.Length; i++)
        {
            k2v[i] = bodies[i].accelerationD;
        }

        // k3
        for (int i = 0; i < bodies.Length; i++)
        {
            bodies[i].positionD = pos0[i] + vel0[i] * (dt * 0.5);
            bodies[i].velocityD = vel0[i] + k2v[i] * (dt * 0.5);
        }
        RecalculateAccelerations();
        Vector3D[] k3v = new Vector3D[bodies.Length];
        for (int i = 0; i < bodies.Length; i++)
        {
            k3v[i] = bodies[i].accelerationD;
        }

        // k4
        for (int i = 0; i < bodies.Length; i++)
        {
            bodies[i].positionD = pos0[i] + vel0[i] * dt;
            bodies[i].velocityD = vel0[i] + k3v[i] * dt;
        }
        RecalculateAccelerations();
        Vector3D[] k4v = new Vector3D[bodies.Length];
        for (int i = 0; i < bodies.Length; i++)
        {
            k4v[i] = bodies[i].accelerationD;
        }

        // Final integration
        for (int i = 0; i < bodies.Length; i++)
        {
            bodies[i].positionD = pos0[i] + (vel0[i] + (k1v[i] + k2v[i] * 2 + k3v[i] * 2) * dt / 6.0) * dt;
            bodies[i].velocityD = vel0[i] + (k1v[i] + k2v[i] * 2 + k3v[i] * 2 + k4v[i]) * (dt / 6.0);
            bodies[i].Rigidbody.MovePosition(bodies[i].positionD.ToVector3());
        }
    }

    private void RecalculateAccelerations()
    {
        if (forceMethod == ForceCalculationMethod.BarnesHut)
        {
            octree.Build(bodies);
            foreach (var body in bodies)
            {
                body.accelerationD = octree.CalculateAcceleration(body);
            }
        }
        else
        {
            foreach (var body in bodies)
            {
                CalculateAccelerationBruteForce(body);
            }
        }
    }

    #endregion

    #region Job Integration Methods

    private void IntegrateEulerJob(double dt)
    {
        var job = new EulerIntegrationJob
        {
            positions = positions,
            velocities = velocities,
            accelerations = accelerations,
            deltaTime = dt
        };

        job.Schedule(bodies.Length, jobBatchSize).Complete();
    }

    private void IntegrateVelocityVerletJob(double dt)
    {
        // Store old accelerations
        oldAccelerations.CopyFrom(accelerations);

        // Update positions
        var posJob = new VelocityVerletPositionJob
        {
            positions = positions,
            velocities = velocities,
            accelerations = accelerations,
            deltaTime = dt
        };
        posJob.Schedule(bodies.Length, jobBatchSize).Complete();

        // Recalculate accelerations
        var accelJob = new CalculateAccelerationJob
        {
            positions = positions,
            masses = masses,
            gravitationalConstant = StaticVariables.gravitationalConstant,
            softeningLength = softeningLength,
            accelerations = accelerations
        };
        accelJob.Schedule(bodies.Length, jobBatchSize).Complete();

        // Update velocities
        var velJob = new VelocityVerletVelocityJob
        {
            velocities = velocities,
            oldAccelerations = oldAccelerations,
            newAccelerations = accelerations,
            deltaTime = dt
        };
        velJob.Schedule(bodies.Length, jobBatchSize).Complete();
    }

    #endregion

    #region Native Array Management

    private void InitializeNativeArrays()
    {
        int count = bodies.Length;

        positions = new NativeArray<double3>(count, Allocator.Persistent);
        velocities = new NativeArray<double3>(count, Allocator.Persistent);
        accelerations = new NativeArray<double3>(count, Allocator.Persistent);
        oldAccelerations = new NativeArray<double3>(count, Allocator.Persistent);
        masses = new NativeArray<double>(count, Allocator.Persistent);
        adaptiveTimestepArray = new NativeArray<double>(1, Allocator.Persistent);

        // RK4 arrays
        k1Pos = new NativeArray<double3>(count, Allocator.Persistent);
        k2Pos = new NativeArray<double3>(count, Allocator.Persistent);
        k3Pos = new NativeArray<double3>(count, Allocator.Persistent);
        k4Pos = new NativeArray<double3>(count, Allocator.Persistent);
        k1Vel = new NativeArray<double3>(count, Allocator.Persistent);
        k2Vel = new NativeArray<double3>(count, Allocator.Persistent);
        k3Vel = new NativeArray<double3>(count, Allocator.Persistent);
        k4Vel = new NativeArray<double3>(count, Allocator.Persistent);
        tempPositions = new NativeArray<double3>(count, Allocator.Persistent);
        tempVelocities = new NativeArray<double3>(count, Allocator.Persistent);

        nativeArraysInitialized = true;
    }

    private void DisposeNativeArrays()
    {
        if (!nativeArraysInitialized) return;

        if (positions.IsCreated) positions.Dispose();
        if (velocities.IsCreated) velocities.Dispose();
        if (accelerations.IsCreated) accelerations.Dispose();
        if (oldAccelerations.IsCreated) oldAccelerations.Dispose();
        if (masses.IsCreated) masses.Dispose();
        if (adaptiveTimestepArray.IsCreated) adaptiveTimestepArray.Dispose();

        if (k1Pos.IsCreated) k1Pos.Dispose();
        if (k2Pos.IsCreated) k2Pos.Dispose();
        if (k3Pos.IsCreated) k3Pos.Dispose();
        if (k4Pos.IsCreated) k4Pos.Dispose();
        if (k1Vel.IsCreated) k1Vel.Dispose();
        if (k2Vel.IsCreated) k2Vel.Dispose();
        if (k3Vel.IsCreated) k3Vel.Dispose();
        if (k4Vel.IsCreated) k4Vel.Dispose();
        if (tempPositions.IsCreated) tempPositions.Dispose();
        if (tempVelocities.IsCreated) tempVelocities.Dispose();

        nativeArraysInitialized = false;
    }

    #endregion

    #region Debug Visualization

    void OnDrawGizmos()
    {
        if (drawOctree && octree != null && Application.isPlaying)
        {
            octree.DrawGizmos();
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Get current simulation statistics
    /// </summary>
    public SimulationStats GetStats()
    {
        return new SimulationStats
        {
            bodyCount = bodies.Length,
            forceMethod = forceMethod,
            integrationMethod = integrationMethod,
            currentTimestep = (float)currentAdaptiveTimestep,
            useJobSystem = useJobSystem,
            barnesHutTheta = barnesHutTheta
        };
    }

    public struct SimulationStats
    {
        public int bodyCount;
        public ForceCalculationMethod forceMethod;
        public IntegrationMethod integrationMethod;
        public float currentTimestep;
        public bool useJobSystem;
        public float barnesHutTheta;
    }

    #endregion
}
