using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement("translucent-element")]
public partial class TranslucentElement : VisualElement
{
    private readonly VisualElement blurBackground;

    public TranslucentElement()
    {
        blurBackground = new VisualElement
        {
            name = "blur-background"
        };
        blurBackground.AddToClassList("blur-background");

        hierarchy.Insert(0, blurBackground);

        RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
    }

    private void OnGeometryChanged(GeometryChangedEvent evt)
    {
        blurBackground.style.position = Position.Absolute;
        blurBackground.style.width = resolvedStyle.width;
        blurBackground.style.height = resolvedStyle.height;
        blurBackground.style.top = 0;
        blurBackground.style.left = 0;
    }
}

  