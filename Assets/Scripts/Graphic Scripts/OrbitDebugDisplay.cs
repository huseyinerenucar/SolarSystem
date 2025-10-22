using UnityEngine;

[ExecuteInEditMode]
public class OrbitDebugDisplay : MonoBehaviour
{

    public int numSteps = 1000;
    public float timeStep = 0.1f;
    public bool usePhysicsTimeStep;

    public bool relativeToBody;
    public CelestialBody centralBody;
    public float width = 100;
    public bool useThickLines;

    void Start()
    {
        DrawOrbits();
    }

    void DrawOrbits()
    {
        CelestialBody[] bodies = FindObjectsByType<CelestialBody>(FindObjectsSortMode.None);
        var virtualBodies = new VirtualBody[bodies.Length];
        var drawPoints = new Vector3[bodies.Length][];
        int referenceFrameIndex = 0;
        Vector3 referenceBodyInitialPosition = Vector3.zero;

        for (int i = 0; i < virtualBodies.Length; i++)
        {
            virtualBodies[i] = new VirtualBody(bodies[i]);
            drawPoints[i] = new Vector3[numSteps];

            if (bodies[i] == centralBody && relativeToBody)
            {
                referenceFrameIndex = i;
                referenceBodyInitialPosition = virtualBodies[i].position;
            }
        }

        for (int step = 0; step < numSteps; step++)
        {
            Vector3 referenceBodyPosition = (relativeToBody) ? virtualBodies[referenceFrameIndex].position : Vector3.zero;

            for (int i = 0; i < virtualBodies.Length; i++)
                virtualBodies[i].velocity += CalculateAcceleration(i, virtualBodies) * timeStep;

            for (int i = 0; i < virtualBodies.Length; i++)
            {
                Vector3 newPos = virtualBodies[i].position + virtualBodies[i].velocity * timeStep;
                virtualBodies[i].position = newPos;
                if (relativeToBody)
                {
                    var referenceFrameOffset = referenceBodyPosition - referenceBodyInitialPosition;
                    newPos -= referenceFrameOffset;
                }
                if (relativeToBody && i == referenceFrameIndex)
                    newPos = referenceBodyInitialPosition;

                drawPoints[i][step] = newPos;
            }
        }

        for (int bodyIndex = 0; bodyIndex < virtualBodies.Length; bodyIndex++)
        {
            if (useThickLines)
            {
                var lineRenderer = bodies[bodyIndex].gameObject.GetComponentInChildren<LineRenderer>();
                lineRenderer.enabled = true;
                lineRenderer.positionCount = drawPoints[bodyIndex].Length;
                lineRenderer.SetPositions(drawPoints[bodyIndex]);
                lineRenderer.startColor = new Color(1f, 1f, 1f);
                lineRenderer.endColor = new Color(1f, 1f, 1f);
                lineRenderer.widthMultiplier = width;
            }
            else
            {
                for (int i = 0; i < drawPoints[bodyIndex].Length - 1; i++)
                    Debug.DrawLine(drawPoints[bodyIndex][i], drawPoints[bodyIndex][i + 1], new Color(1f, 1f, 1f));

                var lineRenderer = bodies[bodyIndex].gameObject.GetComponentInChildren<LineRenderer>();

                if (lineRenderer)
                    lineRenderer.enabled = false;
            }

        }
    }

    Vector3 CalculateAcceleration(int i, VirtualBody[] virtualBodies)
    {
        Vector3D acceleration = Vector3D.zero;
        for (int j = 0; j < virtualBodies.Length; j++)
        {
            if (i == j)
                continue;

            Vector3D forceDir = (virtualBodies[j].position - virtualBodies[i].position).normalized;
            float sqrDst = (virtualBodies[j].position - virtualBodies[i].position).sqrMagnitude;
            acceleration += StaticVariables.gravitationalConstant * virtualBodies[j].mass * forceDir / sqrDst;
        }
        return acceleration;
    }

    void HideOrbits()
    {
        CelestialBody[] bodies = FindObjectsByType<CelestialBody>(FindObjectsSortMode.None);

        for (int bodyIndex = 0; bodyIndex < bodies.Length; bodyIndex++)
        {
            var lineRenderer = bodies[bodyIndex].gameObject.GetComponentInChildren<LineRenderer>();
            lineRenderer.positionCount = 0;
        }
    }

    void OnValidate()
    {
        if (usePhysicsTimeStep)
            timeStep = StaticVariables.physicsTimeStep;
    }

    class VirtualBody
    {
        public Vector3 position;
        public Vector3 velocity;
        public double mass;

        public VirtualBody(CelestialBody body)
        {
            position = body.transform.position;
            velocity = body.initialVelocityVector;
            mass = body.mass;
        }
    }
}