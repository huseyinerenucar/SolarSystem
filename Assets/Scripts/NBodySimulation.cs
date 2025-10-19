using UnityEngine;

public class NBodySimulation : MonoBehaviour
{
    [SerializeField] private ScriptableVariables scriptableVariables;
    CelestialBody[] bodies;
    static NBodySimulation instance;

    void Awake()
    {
        bodies = FindObjectsByType<CelestialBody>(FindObjectsSortMode.None);
    }

    void FixedUpdate()
    {
        for (int i = 0; i < bodies.Length; i++)
        {
            Vector3 acceleration = CalculateAcceleration(bodies[i].Position, bodies[i]);
            bodies[i].UpdateVelocity(acceleration, scriptableVariables.currentTimeSpeed);
        }

        for (int i = 0; i < bodies.Length; i++)
            bodies[i].UpdatePosition(scriptableVariables.currentTimeSpeed);

        for (int i = 0; i < bodies.Length; i++)
            bodies[i].UpdateRotation(scriptableVariables.currentTimeSpeed);
    }

    public static Vector3 CalculateAcceleration(Vector3 point, CelestialBody ignoreBody = null)
    {
        Vector3 acceleration = Vector3.zero;
        foreach (var body in Instance.bodies)
        {
            if (body != ignoreBody)
            {
                float sqrDst = (body.Position - point).sqrMagnitude;
                Vector3 forceDir = (body.Position - point).normalized;
                acceleration += body.mass * StaticVariables.gravitationalConstant * forceDir / sqrDst;
            }
        }

        return acceleration;
    }

    public static CelestialBody[] Bodies
    {
        get
        {
            return Instance.bodies;
        }
    }

    static NBodySimulation Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<NBodySimulation>();
            }
            return instance;
        }
    }
}