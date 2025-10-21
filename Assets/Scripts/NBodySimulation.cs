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
            CalculateAcceleration(bodies[i]);
            bodies[i].UpdateVelocity(scriptableVariables.currentTimeSpeed);
        }
        for (int i = 0; i < bodies.Length; i++)
            bodies[i].UpdatePosition(scriptableVariables.currentTimeSpeed);
        for (int i = 0; i < bodies.Length; i++)
            bodies[i].UpdateRotation(scriptableVariables.currentTimeSpeed);
    }
    public static void CalculateAcceleration(CelestialBody celestialBody = null)
    {
        Vector3 acceleration = Vector3.zero;
        foreach (var body in Instance.bodies)
        {
            if (body != celestialBody)
            {
                float sqrDst = (body.Position - celestialBody.Position).sqrMagnitude;
                Vector3 forceDir = (body.Position - celestialBody.Position).normalized;
                acceleration += body.mass * StaticVariables.gravitationalConstant * forceDir / sqrDst;
            }
        }
        celestialBody.accelerationVector = acceleration;
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