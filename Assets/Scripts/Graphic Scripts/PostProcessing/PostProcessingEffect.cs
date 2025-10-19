using UnityEngine;

public abstract class PostProcessingEffect : ScriptableObject
{

    protected Material material;

    public virtual Material GetMaterial()
    {
        return null;
    }

    public abstract void Render(RenderTexture source, RenderTexture destination);
}