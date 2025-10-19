using UnityEngine;

public class SolarSystemSpawner : MonoBehaviour
{
    public CelestialBodyGenerator.ResolutionSettings resolutionSettings;

    void Awake()
    {
        Spawn();
    }

    public void Spawn()
    {
        CelestialBody[] bodies = FindObjectsByType<CelestialBody>(FindObjectsSortMode.None);

        foreach (var body in bodies)
        {
            if (body.bodyType == CelestialBody.BodyType.Sun)
                continue;

            if (body.bodyName == "Earth")
                continue;

            CelestialBodyPlaceholder placeholder = body.gameObject.GetComponentInChildren<CelestialBodyPlaceholder>();
            var template = placeholder.bodySettings;

            Destroy(placeholder.gameObject);

            GameObject holder = new("Celestial Body Generator");
            var generator = holder.AddComponent<CelestialBodyGenerator>();
            generator.transform.parent = body.transform;
            generator.gameObject.layer = body.gameObject.layer;
            generator.transform.localRotation = Quaternion.identity;
            generator.transform.localPosition = Vector3.zero;
            generator.transform.localScale = Vector3.one * body.radius;
            generator.resolutionSettings = resolutionSettings;

            generator.body = template;

        }
    }

}