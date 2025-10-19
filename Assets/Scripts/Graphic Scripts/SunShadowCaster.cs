using UnityEngine;

public class SunShadowCaster : MonoBehaviour
{
    Transform track;

    void Start()
    {
        track = Camera.main != null ? Camera.main.transform : null;
    }

    void LateUpdate()
    {
        if (track)
            transform.LookAt(track.position);
    }
}