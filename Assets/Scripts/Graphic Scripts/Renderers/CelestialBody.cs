using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Rigidbody))]
public class CelestialBody : MonoBehaviour
{
    public enum BodyType { Planet, Moon, Sun }
    public BodyType bodyType;
    public float radius;
    public float surfaceGravity;
    public Vector3 initialVelocity;
    public Vector3 initialRotation;
    public string bodyName = "Unnamed";
    readonly Transform meshHolder;

    public Vector3 Velocity { get; private set; }
    public float mass { get; private set; }
    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        Velocity = initialVelocity;
        RecalculateMass();
    }

    public void UpdateVelocity(Vector3 acceleration, float timeStep)
    {
        Velocity += acceleration * timeStep * Time.fixedDeltaTime;
    }

    public void UpdatePosition(float timeStep)
    {
        rb.MovePosition(rb.position + (Time.fixedDeltaTime * timeStep * Velocity));
        //if (bodyType.HasFlag(BodyType.Planet))
        //    Debug.Log(rb.position);
    }

    public void UpdateRotation(float timeStep)
    {
        transform.Rotate(Time.fixedDeltaTime * timeStep * initialRotation);
        if (bodyType.HasFlag(BodyType.Moon))
        {
            Debug.Log(initialRotation);
            Debug.Log(transform.rotation);
        }
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