using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

[CreateAssetMenu(fileName = "ScriptableVariables", menuName = "Scriptable Objects/ScriptableVariables")]
public class ScriptableVariables : ScriptableObject
{
    public bool isUIMode = false;
    public StyleLength timeSpeedPanelWidth = new UnityEngine.UIElements.Length(StaticVariables.TimeSpeedPanelWidth, UnityEngine.UIElements.LengthUnit.Percent);
    public StyleLength timeSpeedPanelHeight = new UnityEngine.UIElements.Length(StaticVariables.TimeSpeedPanelHeight, UnityEngine.UIElements.LengthUnit.Percent);
    public float customTimeSpeed = 10;
    public float currentTimeSpeed = 1;

    [CreateProperty]
    public string customTimeSpeedButtonText => isUIMode ? $"Custom: {customTimeSpeed}x" : $"{customTimeSpeed}x";
}
