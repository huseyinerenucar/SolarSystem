using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Rigidbody))]
public class CelestialBody : MonoBehaviour
{
    public enum BodyType { Planet, Moon, Sun }
    public BodyType bodyType;
    public float radius;
    public float surfaceGravity;
    public string bodyName = "Unnamed";
    readonly Transform meshHolder;
    public Vector3 initialVelocityVector;
    public Vector3 initialRotationVector;

    // Double precision tracking for accurate long-term simulations
    [System.NonSerialized] public Vector3D positionD;
    [System.NonSerialized] public Vector3D velocityD;
    [System.NonSerialized] public Vector3D accelerationD;

    // Legacy float vectors for backward compatibility
    public Vector3 accelerationVector;
    public Vector3 velocityVector { get; private set; }
    public float mass { get; private set; }
    Rigidbody rb;
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        velocityVector = initialVelocityVector;

        // Initialize double precision vectors
        positionD = new Vector3D(rb.position);
        velocityD = new Vector3D(initialVelocityVector);
        accelerationD = Vector3D.zero;

        RecalculateMass();
    }
    // Legacy float-based updates (kept for backward compatibility)
    public void UpdateVelocity(float timeStep)
    {
        velocityVector += Time.fixedDeltaTime * timeStep * accelerationVector;
    }
    public void UpdatePosition(float timeStep)
    {
        rb.MovePosition(rb.position + (Time.fixedDeltaTime * timeStep * velocityVector));
    }

    // Double precision updates - used by optimized simulation
    public void UpdateVelocityD(double timeStep)
    {
        velocityD += accelerationD * timeStep;
        velocityVector = velocityD.ToVector3(); // Sync with float version
    }

    public void UpdatePositionD(double timeStep)
    {
        positionD += velocityD * timeStep;
        // Sync Rigidbody position with double precision position
        rb.MovePosition(positionD.ToVector3());
    }

    public void UpdateRotation(float timeStep)
    {
        Vector3 rotationDelta = Time.fixedDeltaTime * timeStep * initialRotationVector;
        rb.MoveRotation(rb.rotation * Quaternion.Euler(rotationDelta));
    }
    void OnValidate()
    {
        RecalculateMass();
        if (GetComponentInChildren<CelestialBodyGenerator>())
            GetComponentInChildren<CelestialBodyGenerator>().transform.localScale = Vector3.one * radius;
        gameObject.name = bodyName;
    }
    public void RecalculateMass()
    {
        mass = surfaceGravity * radius * radius / StaticVariables.gravitationalConstant;
        Rigidbody.mass = mass;
    }
    public Rigidbody Rigidbody
    {
        get
        {
            if (!rb)
                rb = GetComponent<Rigidbody>();
            return rb;
        }
    }
    public Vector3 Position
    {
        get
        {
            return rb.position;
        }
    }
}