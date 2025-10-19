using UnityEngine.UIElements;
using UnityEngine;
using System;

[UxmlElement]
public partial class CustomIconButton : Button
{
    private VisualElementFit m_VisualElementFit;
    private VisualElementFit.FitMode m_FitMode = VisualElementFit.FitMode.None;

    [UxmlAttribute("fit-mode")]
    public VisualElementFit.FitMode fitMode
    {
        get { return m_FitMode; }
        set
        {
            m_FitMode = value;
            m_VisualElementFit?.SetFitMode(value);
        }
    }

    public CustomIconButton() : base()
    {
        RegisterCallback<AttachToPanelEvent>(OnAttachedToPanel);
    }

    public CustomIconButton(Action clickEvent) : base(clickEvent)
    {
        RegisterCallback<AttachToPanelEvent>(OnAttachedToPanel);
    }

    private void OnAttachedToPanel(AttachToPanelEvent evt)
    {
        schedule.Execute(() =>
        {
            ReplaceImageWithVisualElementFit();
        });
    }

    private void ReplaceImageWithVisualElementFit()
    {
        var existingImage = this.Q<Image>(className: "unity-button__image");

        if (existingImage != null)
        {
            var parent = existingImage.parent;
            var index = parent.IndexOf(existingImage);

            var sprite = existingImage.sprite;
            var image = existingImage.image;
            var vectorImage = existingImage.vectorImage;
            var bgImage = existingImage.style.backgroundImage;

            existingImage.RemoveFromHierarchy();

            m_VisualElementFit = new VisualElementFit();
            m_VisualElementFit.AddToClassList("unity-button__image");
            m_VisualElementFit.SetFitMode(m_FitMode);

            if (string.IsNullOrEmpty(text))
            {
                m_VisualElementFit.style.marginRight = 0; 
            }

            var useBackgroundImageField = typeof(VisualElementFit).GetField("useBackgroundImage",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            useBackgroundImageField?.SetValue(m_VisualElementFit, true);

            if (sprite != null)
                m_VisualElementFit.style.backgroundImage = new StyleBackground(sprite);
            else if (image != null)
            {
                if (image is Texture2D texture2D)
                    m_VisualElementFit.style.backgroundImage = new StyleBackground(texture2D);
                else if (image is RenderTexture renderTexture)
                    m_VisualElementFit.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(renderTexture));
            }
            else if (vectorImage != null)
                m_VisualElementFit.style.backgroundImage = new StyleBackground(vectorImage);
            else if (bgImage.value.sprite != null || bgImage.value.texture != null ||
                     bgImage.value.renderTexture != null || bgImage.value.vectorImage != null)
                m_VisualElementFit.style.backgroundImage = bgImage;

            parent.Insert(index, m_VisualElementFit);
            m_VisualElementFit.RecalculateFit();
        }
        else
        {
            m_VisualElementFit = new VisualElementFit();
            m_VisualElementFit.AddToClassList("unity-button__image");
            m_VisualElementFit.SetFitMode(m_FitMode);

            if (string.IsNullOrEmpty(text))
            {
                m_VisualElementFit.style.marginRight = 0; 
            }

            var useBackgroundImageField = typeof(VisualElementFit).GetField("useBackgroundImage",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            useBackgroundImageField?.SetValue(m_VisualElementFit, true);

            Insert(0, m_VisualElementFit);
            m_VisualElementFit.RecalculateFit();
        }
    }

    private void UpdateVisualElementFitSettings()
    {
        if (m_VisualElementFit != null)
        {
            var useBackgroundImageField = typeof(VisualElementFit).GetField("useBackgroundImage",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (useBackgroundImageField != null)
            {
                useBackgroundImageField.SetValue(m_VisualElementFit, true);
                m_VisualElementFit.RecalculateFit();
            }
        }
    }

    public void RefreshVisualElementFit()
    {
        m_VisualElementFit?.RecalculateFit();
    }
}