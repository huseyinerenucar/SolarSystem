using UnityEngine;

/// <summary>
/// AdaptiveFOV
/// - Keeps FOV feeling consistent across screens by maintaining a target *horizontal* FOV
///   (auto-converts to vertical FOV for Unity's Camera.fieldOfView).
/// - Lets players override with their own FOV via a slider (HFOV).
/// - Handles split-screen/letterboxed cameras via Camera.pixelWidth/Height.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class AdaptiveFOV : MonoBehaviour
{
    public enum Mode
    {
        MaintainHorizontalFOV, // Use targetHorizontalFOV (recommended for most games)
        MaintainVerticalFOV,   // Keep targetVerticalFOV regardless of aspect
        ClampFromHorizontal    // Use horizontal but clamp resulting vertical
    }

    [Header("FOV Mode")]
    public Mode mode = Mode.MaintainHorizontalFOV;

    [Header("Targets (Degrees)")]
    [Tooltip("Desired horizontal FOV. Converted to vertical based on current aspect.")]
    [Range(30f, 150f)] public float targetHorizontalFOV = 100f;

    [Tooltip("Desired vertical FOV (Unity's fieldOfView).")]
    [Range(30f, 120f)] public float targetVerticalFOV = 60f;

    [Header("Clamping (for ultrawide / tiny screens)")]
    [Tooltip("Only used when Mode = ClampFromHorizontal.")]
    public Vector2 verticalClamp = new Vector2(45f, 100f);

    Camera cam;
    Vector2Int lastPixelSize;

    void OnEnable()
    {
        cam = GetComponent<Camera>();
        ForceUpdateFOV();
    }

    void OnValidate()
    {
        if (verticalClamp.x > verticalClamp.y)
            verticalClamp = new Vector2(verticalClamp.y, verticalClamp.x);

        ForceUpdateFOV();
    }

    void Update()
    {
        Vector2Int now = new(cam.pixelWidth, cam.pixelHeight);
        if (now != lastPixelSize)
        {
            lastPixelSize = now;
            ApplyFOV();
        }

#if UNITY_EDITOR
        if (!Application.isPlaying) ApplyFOV();
#endif
    }

    public void SetUserHorizontalFOV(float hFovDegrees)
    {
        hFovDegrees = Mathf.Clamp(hFovDegrees, 30f, 150f);
        targetHorizontalFOV = hFovDegrees;



        ApplyFOV();
    }

    public float GetCurrentHorizontalFOV()
    {
        float v = cam.fieldOfView;
        float aspect = GetAspect();
        return VerticalToHorizontal(v, aspect);
    }

    void ForceUpdateFOV()
    {
        lastPixelSize = new Vector2Int(cam ? cam.pixelWidth : Screen.width, cam ? cam.pixelHeight : Screen.height);
        ApplyFOV();
    }

    void ApplyFOV()
    {
        if (cam == null)
            return;

        float aspect = GetAspect();
        float newVertical;

        switch (mode)
        {
            case Mode.MaintainHorizontalFOV:
                newVertical = HorizontalToVertical(targetHorizontalFOV, aspect);
                break;

            case Mode.MaintainVerticalFOV:
                newVertical = targetVerticalFOV;
                break;

            case Mode.ClampFromHorizontal:
                newVertical = HorizontalToVertical(targetHorizontalFOV, aspect);
                newVertical = Mathf.Clamp(newVertical, verticalClamp.x, verticalClamp.y);
                break;

            default:
                newVertical = cam.fieldOfView;
                break;
        }

        cam.fieldOfView = Mathf.Clamp(newVertical, 1f, 179f);
    }

    float GetAspect()
    {
        int w = Mathf.Max(1, cam.pixelWidth);
        int h = Mathf.Max(1, cam.pixelHeight);
        return (float)w / h;
    }

    public static float HorizontalToVertical(float hDeg, float aspect)
    {
        aspect = Mathf.Max(0.0001f, aspect);
        float hRad = hDeg * Mathf.Deg2Rad;
        float vRad = 2f * Mathf.Atan(Mathf.Tan(hRad * 0.5f) / aspect);
        return vRad * Mathf.Rad2Deg;
    }

    public static float VerticalToHorizontal(float vDeg, float aspect)
    {
        aspect = Mathf.Max(0.0001f, aspect);
        float vRad = vDeg * Mathf.Deg2Rad;
        float hRad = 2f * Mathf.Atan(Mathf.Tan(vRad * 0.5f) * aspect);
        return hRad * Mathf.Rad2Deg;
    }
}
