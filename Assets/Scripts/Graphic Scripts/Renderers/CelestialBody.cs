using UnityEngine;

/// <summary>
/// Enhanced celestial body with double precision physics.
/// Maintains separate high-precision world position while syncing with Unity Transform for rendering.
/// </summary>
[ExecuteInEditMode]
[RequireComponent(typeof(Rigidbody))]
public class CelestialBody : MonoBehaviour
{
    public enum BodyType { Star, Planet, Moon, Asteroid }
    public BodyType bodyType = BodyType.Planet;

    public string bodyName = "Unnamed";
    public float radius = 1f;
    [SerializeField] private float surfaceGravity = 9.8f;

    [Header("Initial Conditions")]
    public Vector3 initialVelocityVector = Vector3.zero;
    public Vector3 initialRotationVector = new(0, 10, 0);

    [Header("Visual Settings")]
    [SerializeField] private bool showOrbitPath = false;
    [SerializeField] private Color orbitColor = Color.white;
    
    [System.NonSerialized] public Vector3D worldPosition;
    [System.NonSerialized] public Vector3D worldVelocity;
    [System.NonSerialized] public Vector3D worldAcceleration;

    private double _mass;
    public double mass => _mass;

    private Rigidbody rb;
    private Transform cachedTransform;
    private TrailRenderer trailRenderer;

    private static Vector3D s_worldOrigin = Vector3D.zero;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        cachedTransform = transform;
        trailRenderer = GetComponent<TrailRenderer>();

        rb.isKinematic = true;
        rb.useGravity = false;

        worldPosition = new Vector3D(cachedTransform.position);
        worldVelocity = new Vector3D(initialVelocityVector);
        worldAcceleration = Vector3D.zero;

        RecalculateMass();
    }

    void OnValidate()
    {
        RecalculateMass();
        gameObject.name = bodyName;

        if (Application.isPlaying)
            UpdateVisualScale();
    }

    /// <summary>
    /// Recalculate mass from radius and surface gravity.
    /// Formula: m = g * r² / G
    /// </summary>
    public void RecalculateMass()
    {
        _mass = surfaceGravity * radius * radius / StaticVariables.gravitationalConstant;

        if (rb != null)
            rb.mass = (float)_mass;
    }

    /// <summary>
    /// Update Unity transform from double precision world position.
    /// Converts to float relative to current world origin.
    /// </summary>
    public void UpdateRenderPosition()
    {
        Vector3D relativePosition = worldPosition - s_worldOrigin;
        cachedTransform.position = relativePosition.ToVector3();
    }

    /// <summary>
    /// Apply rotation based on initial rotation vector.
    /// </summary>
    public void UpdateRotation(float deltaTime)
    {
        Vector3 rotationDelta = deltaTime * initialRotationVector;
        cachedTransform.Rotate(rotationDelta, Space.Self);
    }

    /// <summary>
    /// Set the world origin for all celestial bodies.
    /// Used by floating origin system.
    /// </summary>
    public static void SetWorldOrigin(Vector3D newOrigin)
    {
        s_worldOrigin = newOrigin;
    }

    /// <summary>
    /// Get the current world origin.
    /// </summary>
    public static Vector3D GetWorldOrigin()
    {
        return s_worldOrigin;
    }

    /// <summary>
    /// Initialize body with specific position and velocity.
    /// Useful for programmatic setup.
    /// </summary>
    public void Initialize(Vector3D position, Vector3D velocity, double bodyMass)
    {
        worldPosition = position;
        worldVelocity = velocity;
        worldAcceleration = Vector3D.zero;
        _mass = bodyMass;

        UpdateRenderPosition();
        UpdateVisualScale();
    }

    /// <summary>
    /// Update visual scale based on mass/radius.
    /// </summary>
    private void UpdateVisualScale()
    {
        cachedTransform.localScale = 2f * radius * Vector3.one; 

        if (trailRenderer != null)
        {
            trailRenderer.startWidth = radius * 0.5f;
            trailRenderer.endWidth = radius * 0.2f;
        }
    }

    /// <summary>
    /// Shift this body's trail renderer when origin changes.
    /// Called by FloatingOriginController.
    /// </summary>
    public void ShiftTrail(Vector3 offset)
    {
        if (trailRenderer == null || trailRenderer.positionCount == 0)
            return;

        Vector3[] positions = new Vector3[trailRenderer.positionCount];
        trailRenderer.GetPositions(positions);

        for (int i = 0; i < positions.Length; i++)
            positions[i] -= offset;

        trailRenderer.SetPositions(positions);
    }

    /// <summary>
    /// Calculate orbital velocity for circular orbit around a central mass.
    /// Useful for initial setup.
    /// </summary>
    public static Vector3D CalculateOrbitalVelocity(Vector3D position, Vector3D centerPosition, double centerMass)
    {
        Vector3D toCenter = centerPosition - position;
        double distance = toCenter.magnitude;

        if (distance < 0.01) return Vector3D.zero;

        double orbitalSpeed = System.Math.Sqrt(StaticVariables.gravitationalConstant * centerMass / distance);

        Vector3D up = Vector3D.up;
        Vector3D tangent = Vector3D.Cross(toCenter.normalized, up).normalized;

        return tangent * orbitalSpeed;
    }

    public Vector3 Position => cachedTransform.position;
    public Vector3 velocityVector => worldVelocity.ToVector3();
    public Vector3 accelerationVector => worldAcceleration.ToVector3();
    public Rigidbody Rigidbody => rb;

    void OnDrawGizmos()
    {
        if (showOrbitPath && Application.isPlaying)
        {
            Gizmos.color = orbitColor;
            Gizmos.DrawWireSphere(transform.position, 0.5f * radius);
        }
    }
}