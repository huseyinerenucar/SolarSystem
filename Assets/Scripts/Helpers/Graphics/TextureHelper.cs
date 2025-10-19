using UnityEngine;

public static class TextureHelper
{

    public static void TextureFromGradient(Gradient gradient, int width, ref Texture2D texture)
    {
        if (texture == null || texture.width != width || texture.height != 1)
        {
            texture = new Texture2D(width, 1)
            {
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        Color32[] colors = new Color32[width];
        for (int i = 0; i < width; i++)
        {
            float t = i / (width - 1f);
            colors[i] = gradient.Evaluate(t);
        }
        texture.SetPixels32(colors);
        texture.Apply();
    }
}