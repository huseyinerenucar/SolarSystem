# N-Body Simulation Optimization Scripts

This folder contains highly optimized N-body simulation components that provide dramatic performance improvements.

## Files

### BarnesHutOctree.cs
Implements the Barnes-Hut algorithm for O(n log n) force calculations.
- Hierarchical space partitioning
- Configurable accuracy (theta parameter)
- Debug visualization support

### NBodyJobs.cs
Unity Job System + Burst compiler integration.
- Parallel force calculation jobs
- Multiple integration method jobs (Euler, Velocity Verlet, RK4)
- Adaptive timestep calculation
- SIMD-optimized with Burst

### OptimizedNBodySimulation.cs
Main simulation controller that orchestrates all optimizations.
- Multiple force calculation methods (Brute Force, Barnes-Hut, Job System)
- Multiple integration methods (Euler, Velocity Verlet, RK4)
- Adaptive timestep support
- Inspector-friendly configuration

## Quick Setup

1. **Add to Scene:**
   ```
   GameObject → Create Empty → Name: "Optimized Simulation"
   Add Component → OptimizedNBodySimulation
   ```

2. **Configure:**
   - Assign your ScriptableVariables asset
   - Choose force calculation method
   - Choose integration method
   - Adjust settings as needed

3. **Disable Old Simulation:**
   - Disable the `NBodySimulation` component if present

4. **Play!**
   - The simulation will automatically detect all CelestialBody objects
   - Performance should be dramatically improved

## Recommended Settings

### Default (Good for most cases)
- Force Method: **Barnes-Hut**
- Integration: **Velocity Verlet**
- Theta: **0.5**
- Job System: **Enabled**

### Maximum Performance
- Force Method: **Barnes-Hut**
- Integration: **Euler**
- Theta: **0.7-0.8**
- Adaptive Timestep: **Enabled**

### Maximum Accuracy
- Force Method: **Barnes-Hut**
- Integration: **RK4**
- Theta: **0.3**
- Job System: **Enabled**

## Dependencies

Required packages:
- Unity.Mathematics (install via Package Manager)
- Unity.Jobs (built-in)
- Unity.Burst (built-in)
- Unity.Collections (built-in)

## See Also

- Main optimization guide: `/OPTIMIZATION_GUIDE.md`
- Vector3D implementation: `/Assets/Scripts/sd/Vector3D.cs`
- CelestialBody: `/Assets/Scripts/Graphic Scripts/Renderers/CelestialBody.cs`
