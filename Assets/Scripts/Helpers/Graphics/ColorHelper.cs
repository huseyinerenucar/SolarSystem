using UnityEngine;

public static class ColourHelper
{
    public static Color Random(RandomHelper random, float satMin, float satMax, float valMin, float valMax)
    {
        return Color.HSVToRGB(random.Value(), random.Range(satMin, satMax), random.Range(valMin, valMax));
    }

    public static Color TweakHSV(Color colRGB, float deltaH, float deltaS, float deltaV)
    {
        Color.RGBToHSV(colRGB, out float hue, out float sat, out float val);
        return Color.HSVToRGB((hue + deltaH) % 1, sat + deltaS, val + deltaV);
    }

    public static Color RandomSimilar(RandomHelper random, Color original, float maxHueDelta, float maxSatDelta, float maxValDelta)
    {
        Color.RGBToHSV(original, out float hue, out float sat, out float val);
        hue = (hue + random.SignedValue() * maxHueDelta) % 1;
        sat += random.SignedValue() * maxSatDelta;
        val += random.SignedValue() * maxValDelta;
        return Color.HSVToRGB(hue, sat, val);
    }

    public static Color RandomSimilar(RandomHelper random, Color original)
    {
        Color.RGBToHSV(original, out float hue, out float sat, out float val);
        hue = (hue + random.SignedValue() * 0.25f) % 1;
        sat += random.Sign() * random.Range(0.2f, 0.4f);
        sat = Mathf.Clamp(sat, 0.2f, 0.8f);
        val += random.Sign() * random.Range(0.2f, 0.4f);
        val = Mathf.Clamp(val, 0.2f, 0.8f);
        return Color.HSVToRGB(hue, sat, val);
    }

    public static Color RandomContrasting(RandomHelper random, Color original)
    {
        Color.RGBToHSV(original, out float hue, out float sat, out float val);
        hue = (hue + 0.5f + random.SignedValue() * 0.1f) % 1;
        sat += random.SignedValue() * 0.2f;
        val = (val < 0.5f) ? random.Range(val + 0.2f, 0.9f) : random.Range(0.1f, val - 0.2f);
        return Color.HSVToRGB(hue, sat, val);
    }
}