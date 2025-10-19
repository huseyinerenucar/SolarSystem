using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class UIConverters
{
#if UNITY_EDITOR
    [InitializeOnLoadMethod]
#else
    [RuntimeInitializeOnLoad(RuntimeInitializeLoadType.SubsystemRegistration)]
#endif

    public static void RegisterConverters()
    {
        RegisterConverter<bool, StyleEnum<DisplayStyle>>("BoolToDisplayStyleEnum", BoolToDisplayStyleEnum);
    }

    public static StyleEnum<DisplayStyle> BoolToDisplayStyleEnum(ref bool value)
    {
        if (value)
            return DisplayStyle.Flex;
        else 
            return DisplayStyle.None;
    }

    private static void RegisterConverter<TInput, TOutput>(string converterGroupName, Unity.Properties.TypeConverter<TInput, TOutput> converter)
    {
        ConverterGroup group = new(converterGroupName);
        group.AddConverter(converter);
        ConverterGroups.RegisterConverterGroup(group);
    }
}
