using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class CustomPostProcessing : MonoBehaviour
{

    public PostProcessingEffect[] effects;
    Shader defaultShader;
    Material defaultMat;
    readonly List<RenderTexture> temporaryTextures = new();
    public bool debugOceanMask;

    public event System.Action<RenderTexture> OnPostProcessingComplete;
    public event System.Action<RenderTexture> OnPostProcessingBegin;

    void Init()
    {
        if (defaultShader == null)
            defaultShader = Shader.Find("Unlit/Texture");

        defaultMat = new Material(defaultShader);
    }

    [ImageEffectOpaque]
    void OnRenderImage(RenderTexture intialSource, RenderTexture finalDestination)
    {
        OnPostProcessingBegin?.Invoke(finalDestination);

        Init();

        temporaryTextures.Clear();

        RenderTexture currentSource = intialSource;
        RenderTexture currentDestination = null;

        if (effects != null)
        {
            for (int i = 0; i < effects.Length; i++)
            {
                PostProcessingEffect effect = effects[i];
                if (effect != null)
                {
                    if (i == effects.Length - 1)
                        currentDestination = finalDestination;
                    else
                    {
                        currentDestination = TemporaryRenderTexture(finalDestination);
                        temporaryTextures.Add(currentDestination);
                    }

                    effect.Render(currentSource, currentDestination);
                    currentSource = currentDestination;
                }
            }
        }

        if (currentDestination != finalDestination)
            Graphics.Blit(currentSource, finalDestination, defaultMat);

        for (int i = 0; i < temporaryTextures.Count; i++)
            RenderTexture.ReleaseTemporary(temporaryTextures[i]);

        if (debugOceanMask)
            Graphics.Blit(FindFirstObjectByType<OceanMaskRenderer>().oceanMaskTexture, finalDestination, defaultMat);

        OnPostProcessingComplete?.Invoke(finalDestination);
    }

    public static void RenderMaterials(RenderTexture source, RenderTexture destination, List<Material> materials)
    {
        List<RenderTexture> temporaryTextures = new();

        RenderTexture currentSource = source;
        RenderTexture currentDestination = null;

        if (materials != null)
        {
            for (int i = 0; i < materials.Count; i++)
            {
                Material material = materials[i];
                if (material != null)
                {

                    if (i == materials.Count - 1)
                        currentDestination = destination;
                    else
                    {
                        currentDestination = TemporaryRenderTexture(destination);
                        temporaryTextures.Add(currentDestination);
                    }
                    Graphics.Blit(currentSource, currentDestination, material);
                    currentSource = currentDestination;
                }
            }
        }

        if (currentDestination != destination)
            Graphics.Blit(currentSource, destination, new Material(Shader.Find("Unlit/Texture")));

        for (int i = 0; i < temporaryTextures.Count; i++)
            RenderTexture.ReleaseTemporary(temporaryTextures[i]);
    }

    public static RenderTexture TemporaryRenderTexture(RenderTexture template)
    {
        return RenderTexture.GetTemporary(template.descriptor);
    }

}