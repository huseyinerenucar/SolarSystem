using UnityEngine;
using UnityEngine.SceneManagement;

public class FloatingOriginManager : MonoBehaviour
{
    [Tooltip("When the camera's distance from origin exceeds this, re-center.")]
    public float distanceThreshold = 5000f;

    public GameObject player;

    public static Vector3 TotalOriginOffset { get; private set; }
    public static event System.Action<Vector3> OnOriginShifted;

    void LateUpdate()
    {
        if (!player) return;

        Vector3 p = player.transform.position;
        if (p.sqrMagnitude > distanceThreshold * distanceThreshold)
        {
            Vector3 offset = -p;
            ApplyOriginShift(offset);
        }
    }

    void ApplyOriginShift(Vector3 offset)
    {
        var scene = SceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();

        foreach (var go in roots)
        {
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true))
                rb.position += offset;
        }

        foreach (var go in roots)
        {
            if (go.TryGetComponent<Rigidbody>(out _))
                continue;
            go.transform.position += offset;
        }

        foreach (var trail in FindObjectsByType<TrailRenderer>(FindObjectsSortMode.None))
        {
            int n = trail.positionCount;
            if (n <= 0) continue;
            var tmp = new Vector3[n];
            trail.GetPositions(tmp);
            for (int i = 0; i < n; i++) tmp[i] += offset;
            trail.SetPositions(tmp);
        }

        Physics.SyncTransforms();

        TotalOriginOffset += offset;
        OnOriginShifted?.Invoke(offset);
    }
}
