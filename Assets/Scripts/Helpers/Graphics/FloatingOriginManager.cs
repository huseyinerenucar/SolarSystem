using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Floating Origin Controller prevents floating-point precision loss by keeping 
/// the camera/player near the world origin and shifting everything else.
/// Essential for large-scale space simulations.
/// </summary>
public class FloatingOriginController : MonoBehaviour
{
    [Header("Reference Object")]
    [Tooltip("The object to keep near origin (usually camera or player spacecraft)")]
    [SerializeField] private Transform referenceObject;

    [Header("Threshold Settings")]
    [Tooltip("Distance from origin before triggering a shift (in Unity units)")]
    [SerializeField] private float shiftThreshold = 1000f;

    [Tooltip("How often to check if shift is needed (in seconds)")]
    [SerializeField] private float checkInterval = 0.5f;

    [Header("What to Shift")]
    [SerializeField] private bool shiftCelestialBodies = true;
    [SerializeField] private bool shiftParticleSystems = true;
    [SerializeField] private bool shiftTrailRenderers = true;
    [SerializeField] private bool shiftLineRenderers = true;

    [Header("Debug")]
    [SerializeField] private bool logShifts = true;
    [SerializeField] private bool showDebugInfo = false;

    private float nextCheckTime;
    private CelestialBody[] celestialBodies;
    private int totalShifts = 0;
    private Vector3 lastShiftAmount;

    void Start()
    {
        if (referenceObject == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                referenceObject = mainCam.transform;
                Debug.Log("FloatingOrigin: Using Main Camera as reference object");
            }
            else
                Debug.LogWarning("FloatingOrigin: No reference object specified and no Main Camera found!");
        }

        RefreshCelestialBodies();

        nextCheckTime = Time.time + checkInterval;
    }

    void Update()
    {
        if (referenceObject == null) return;

        if (Time.time >= nextCheckTime)
        {
            nextCheckTime = Time.time + checkInterval;
            CheckAndShiftOrigin();
        }
    }

    /// <summary>
    /// Check if origin shift is needed and perform it if necessary.
    /// </summary>
    private void CheckAndShiftOrigin()
    {
        float distanceFromOrigin = referenceObject.position.magnitude;

        if (distanceFromOrigin > shiftThreshold)
            ShiftOrigin(referenceObject.position);
    }

    /// <summary>
    /// Perform the origin shift operation.
    /// </summary>
    private void ShiftOrigin(Vector3 offset)
    {
        if (logShifts)
            Debug.Log($"FloatingOrigin: Shifting by {offset.magnitude:F1} units. Total shifts: {totalShifts + 1}");

        lastShiftAmount = offset;
        totalShifts++;

        if (shiftCelestialBodies)
        {
            Vector3D currentOrigin = CelestialBody.GetWorldOrigin();
            Vector3D newOrigin = currentOrigin + new Vector3D(offset);
            CelestialBody.SetWorldOrigin(newOrigin);

            if (shiftTrailRenderers && celestialBodies != null)
            {
                foreach (var body in celestialBodies)
                    body?.ShiftTrail(offset);
            }
        }

        ShiftRootTransforms(offset);

        if (shiftParticleSystems)
            ShiftParticleSystems(offset);

        if (shiftTrailRenderers)
            ShiftTrailRenderers(offset);

        if (shiftLineRenderers)
            ShiftLineRenderers(offset);

        referenceObject.position = Vector3.zero;
    }

    /// <summary>
    /// Shift all root transforms in the scene (except reference object).
    /// </summary>
    private void ShiftRootTransforms(Vector3 offset)
    {
        GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

        foreach (GameObject obj in rootObjects)
        {
            Transform t = obj.transform;

            if (t == referenceObject || t == referenceObject.parent)
                continue;

            t.position -= offset;
        }
    }

    /// <summary>
    /// Shift all active particle systems in world space.
    /// </summary>
    private void ShiftParticleSystems(Vector3 offset)
    {
        ParticleSystem[] particleSystems = FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);

        foreach (ParticleSystem ps in particleSystems)
        {
            if (ps.main.simulationSpace != ParticleSystemSimulationSpace.World)
                continue;

            ParticleSystem.Particle[] particles = new ParticleSystem.Particle[ps.main.maxParticles];
            int particleCount = ps.GetParticles(particles);

            for (int i = 0; i < particleCount; i++)
                particles[i].position -= offset;

            ps.SetParticles(particles, particleCount);
        }
    }

    /// <summary>
    /// Shift all trail renderers (except those on celestial bodies, handled separately).
    /// </summary>
    private void ShiftTrailRenderers(Vector3 offset)
    {
        TrailRenderer[] trails = FindObjectsByType<TrailRenderer>(FindObjectsSortMode.None);

        foreach (TrailRenderer trail in trails)
        {
            if (trail.GetComponent<CelestialBody>() != null)
                continue;

            if (trail.positionCount == 0)
                continue;

            Vector3[] positions = new Vector3[trail.positionCount];
            trail.GetPositions(positions);

            for (int i = 0; i < positions.Length; i++)
                positions[i] -= offset;

            trail.SetPositions(positions);
        }
    }

    /// <summary>
    /// Shift all line renderers in the scene.
    /// </summary>
    private void ShiftLineRenderers(Vector3 offset)
    {
        LineRenderer[] lineRenderers = FindObjectsByType<LineRenderer>(FindObjectsSortMode.None);

        foreach (LineRenderer line in lineRenderers)
        {
            if (line.positionCount == 0)
                continue;

            if (line.useWorldSpace)
            {
                Vector3[] positions = new Vector3[line.positionCount];
                line.GetPositions(positions);

                for (int i = 0; i < positions.Length; i++)
                    positions[i] -= offset;

                line.SetPositions(positions);
            }
        }
    }

    /// <summary>
    /// Refresh the cached list of celestial bodies.
    /// Call this if bodies are added or removed at runtime.
    /// </summary>
    public void RefreshCelestialBodies()
    {
        celestialBodies = FindObjectsByType<CelestialBody>(FindObjectsSortMode.None);
    }

    /// <summary>
    /// Manually trigger an origin shift (for testing or special cases).
    /// </summary>
    public void ForceOriginShift()
    {
        if (referenceObject != null && referenceObject.position != Vector3.zero)
            ShiftOrigin(referenceObject.position);
    }

    /// <summary>
    /// Get statistics about origin shifts.
    /// </summary>
    public void GetStatistics(out int shifts, out Vector3 lastShift)
    {
        shifts = totalShifts;
        lastShift = lastShiftAmount;
    }

    void OnGUI()
    {
        if (showDebugInfo && referenceObject != null)
        {
            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.white;
            style.fontSize = 14;
            style.padding = new RectOffset(10, 10, 10, 10);

            string info = $"Floating Origin Debug:\n" +
                         $"Distance from origin: {referenceObject.position.magnitude:F1} / {shiftThreshold:F1}\n" +
                         $"Total origin shifts: {totalShifts}\n" +
                         $"World origin: {CelestialBody.GetWorldOrigin()}\n" +
                         $"Reference position: {referenceObject.position}";

            GUI.Label(new Rect(10, 10, 400, 120), info, style);
        }
    }

    void OnValidate()
    {
        if (shiftThreshold <= 0)
            shiftThreshold = 1000f;

        if (checkInterval <= 0)
            checkInterval = 0.1f;
    }
}