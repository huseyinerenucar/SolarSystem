using UnityEngine.UIElements;
using UnityEngine;

[UxmlElement]
public partial class VisualElementFit : VisualElement
{
    public enum FitMode
    {
        None = 0,
        FitWidth = 1,
        FitHeight = 2
    }

    [UxmlAttribute("fit-mode")]
    private FitMode fitMode = FitMode.None;

    [UxmlAttribute("use-background-image")]
    private bool useBackgroundImage = true;

    private float cachedAspectRatio = -1f;
    private bool isProcessing;
    private bool isInitialized;

    public VisualElementFit()
    {
        RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
    }

    private void OnAttachToPanel(AttachToPanelEvent evt)
    {
        schedule.Execute(() =>
        {
            CacheTextureInfo();
            isInitialized = true;
            if (cachedAspectRatio > 0)
                ApplyFit(layout);
        });
    }

    private void OnGeometryChanged(GeometryChangedEvent evt)
    {
        if (fitMode == FitMode.None || isProcessing)
            return;

        if (!isInitialized || cachedAspectRatio < 0)
        {
            CacheTextureInfo();
            if (cachedAspectRatio < 0)
            {
                schedule.Execute(() =>
                {
                    CacheTextureInfo();
                    if (cachedAspectRatio > 0)
                        ApplyFit(evt.newRect);
                }).StartingIn(100);
                return;
            }
        }

        if (cachedAspectRatio > 0)
        {
            ApplyFit(evt.newRect);
        }
    }

    private void CacheTextureInfo()
    {
        Sprite sprite = null;

        if (useBackgroundImage)
        {
            var bgImage = resolvedStyle.backgroundImage;
            sprite = bgImage.sprite;
        }
        else
        {
            var imageChild = this.Q<Image>();
            if (imageChild != null)
            {
                var bgImage = imageChild.resolvedStyle.backgroundImage;
                if (bgImage.sprite != null)
                    sprite = bgImage.sprite;
                else if (imageChild.sprite != null)
                    sprite = imageChild.sprite;
            }
        }

        if (sprite?.rect.width > 0 && sprite.rect.height > 0)
            cachedAspectRatio = sprite.rect.width / sprite.rect.height;
    }

    private void ApplyFit(Rect newRect)
    {
        if (isProcessing || cachedAspectRatio <= 0)
            return;

        isProcessing = true;
        try
        {
            switch (fitMode)
            {
                case FitMode.FitWidth:
                    if (newRect.height > 0)
                    {
                        float newWidth = newRect.height * cachedAspectRatio;
                        if (Mathf.Abs(style.width.value.value - newWidth) > 0.5f)
                            style.width = newWidth;
                    }
                    break;

                case FitMode.FitHeight:
                    if (newRect.width > 0)
                    {
                        float newHeight = newRect.width / cachedAspectRatio;
                        if (Mathf.Abs(style.height.value.value - newHeight) > 0.5f)
                            style.height = newHeight;
                    }
                    break;
            }
        }
        finally
        {
            isProcessing = false;
        }
    }

    public void RecalculateFit()
    {
        CacheTextureInfo();
        if (cachedAspectRatio > 0)
            ApplyFit(layout);
    }

    public void SetFitMode(FitMode mode)
    {
        if (fitMode != mode)
        {
            fitMode = mode;
            if (isInitialized)
                RecalculateFit();
        }
    }
}