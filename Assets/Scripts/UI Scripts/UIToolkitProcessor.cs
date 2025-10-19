using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using System.Collections.Generic;
using Unity.Properties;
using static Unity.VisualScripting.AnnotationUtility;

[RequireComponent(typeof(UIDocument))]
public class UIToolkitProcessor : MonoBehaviour
{
    [Tooltip("The persistent RenderTexture from your UniversalBlurFeature.")]
    [SerializeField] private RenderTexture sourceBlurTexture;

    [Tooltip("The USS class name of the VisualElements to apply the blur to.")]
    [SerializeField] private string translucentButtonClass = "translucent-element";

    [Tooltip("Foreground color overlaid on the blurred background.")]
    [SerializeField] private Color foregroundColor = new(1f, 1f, 1f, 0.3f);

    [Tooltip("Foreground color for the active button.")]
    [SerializeField] private Color activeForegroundColor = new(0.7f, 1f, 0.7f, 0.5f);

    [SerializeField] private ScriptableVariables scriptableVariables;

    [SerializeField] private UIDocument uiDocument;

    private class BlurElement
    {
        public VisualElement element;
        public RenderTexture croppedRT;
        public Material material;
        public Color customForegroundColor;
    }

    private readonly List<BlurElement> m_BlurElements = new();
    private Camera m_Camera;
    private CommandBuffer m_CommandBuffer;
    private static readonly int CropViewportRect = Shader.PropertyToID("_CropViewportRect");
    private static readonly int MainTex = Shader.PropertyToID("_MainTex");
    private static readonly int ForegroundColor = Shader.PropertyToID("_ForegroundColor");
    private Button currentActiveTimeSpeedButton;
    private readonly DataBinding buttonDisplayBinding = new()
    {
        dataSourcePath = new PropertyPath("isUIMode"),
        bindingMode = BindingMode.ToTarget
    };

    private readonly float[] customTimeSpeeds = { 7.5f, 10, 20, 30, 40, 50, 75, 100, 200, 300, 400, 500, 750, 1000 };
    private int customTimeSpeedIndex = 1;

    void Start()
    {
        var root = uiDocument.rootVisualElement;

        buttonDisplayBinding.sourceToUiConverters.AddConverter<bool, StyleEnum<DisplayStyle>>(UIConverters.BoolToDisplayStyleEnum);

        Button TimeSpeedButton1x = root.Q<Button>("1xTimeSpeed_Button");
        Button TimeSpeedButton2x = root.Q<Button>("2xTimeSpeed_Button");
        Button TimeSpeedButton5x = root.Q<Button>("5xTimeSpeed_Button");
        Button DecreaseCustomTimeSpeedButton = root.Q<Button>("DecreaseCustomTimeSpeed_Button");
        Button TimeSpeedButtonCustom = root.Q<Button>("CustomTimeSpeed_Button");
        Button IncreaseCustomTimeSpeedButton = root.Q<Button>("IncreaseCustomTimeSpeed_Button");
        VisualElement UIModePanel = root.Q<VisualElement>("UIMode_Panel");

        //SetButtonForegroundColor(UIModePanel, new(0.6f, 0.6f, 1f, 0.5f));

        TimeSpeedButton1x.clicked += () => SetActiveTimeSpeedButton(TimeSpeedButton1x, 1);
        TimeSpeedButton2x.clicked += () => SetActiveTimeSpeedButton(TimeSpeedButton2x, 2);
        TimeSpeedButton5x.clicked += () => SetActiveTimeSpeedButton(TimeSpeedButton5x, 5);
        DecreaseCustomTimeSpeedButton.clicked += () => ChangeCustomTimeSpeed(DecreaseCustomTimeSpeedButton, false);
        TimeSpeedButtonCustom.clicked += () => SetActiveTimeSpeedButton(TimeSpeedButtonCustom, scriptableVariables.customTimeSpeed);
        IncreaseCustomTimeSpeedButton.clicked += () => ChangeCustomTimeSpeed(IncreaseCustomTimeSpeedButton, true);

        SetActiveTimeSpeedButton(TimeSpeedButton1x, 1);
    }

    private void SetActiveTimeSpeedButton(Button clickedButton, float speed)
    {
        if (currentActiveTimeSpeedButton != null)
        {
            currentActiveTimeSpeedButton.RemoveFromClassList("active");
            currentActiveTimeSpeedButton.SetBinding("style.display", buttonDisplayBinding);

            //SetButtonForegroundColor(currentActiveTimeSpeedButton, foregroundColor);
        }

        clickedButton.AddToClassList("active");
        currentActiveTimeSpeedButton = clickedButton;
        currentActiveTimeSpeedButton.ClearBinding("style.display");

        //SetButtonForegroundColor(clickedButton, activeForegroundColor);

        SetTimeSpeed(speed);
    }

    private void SetTimeSpeed(float speed)
    {
        //Time.timeScale = speed;
        scriptableVariables.currentTimeSpeed = speed;
    }

    private void ChangeCustomTimeSpeed(Button clickedButton, bool isIncreasing)
    {
        clickedButton.AddToClassList("active");

        if (isIncreasing)
            customTimeSpeedIndex++;
        else
            customTimeSpeedIndex--;

        customTimeSpeedIndex = Mathf.Clamp(customTimeSpeedIndex, 0, customTimeSpeeds.Length - 1);

        scriptableVariables.customTimeSpeed = customTimeSpeeds[customTimeSpeedIndex];

        if (currentActiveTimeSpeedButton.name == "CustomTimeSpeed_Button")
            SetTimeSpeed(scriptableVariables.customTimeSpeed);

        clickedButton.RemoveFromClassList("active");
    }

    private void SetButtonForegroundColor(VisualElement button, Color color)
    {
        var blurElement = m_BlurElements.Find(be => be.element == button);
        if (blurElement != null)
            blurElement.customForegroundColor = color;
    }

    void OnEnable()
    {
        m_Camera = Camera.main;
        m_CommandBuffer = new CommandBuffer { name = "UIToolkit Blur Crop" };
        var elements = uiDocument.rootVisualElement.Query(className: translucentButtonClass).ToList();

        foreach (var element in elements)
        {
            var blurElement = new BlurElement
            {
                element = element,
                material = new Material(Shader.Find("Hidden/UI Toolkit Crop")),
                customForegroundColor = foregroundColor
            };
            m_BlurElements.Add(blurElement);
            element.RegisterCallback<GeometryChangedEvent>(evt => OnElementGeometryChanged(blurElement));
        }
    }

    private void OnDisable()
    {
        foreach (var blurElement in m_BlurElements)
        {
            if (blurElement.croppedRT != null)
            {
                blurElement.croppedRT.Release();
                DestroyImmediate(blurElement.croppedRT);
            }

            if (blurElement.material != null)
                DestroyImmediate(blurElement.material);

            if (blurElement.element != null)
                blurElement.element.style.backgroundImage = null;
        }

        m_BlurElements.Clear();
        m_CommandBuffer?.Release();
    }

    private void OnElementGeometryChanged(BlurElement blurElement)
    {
        int width = Mathf.CeilToInt(blurElement.element.resolvedStyle.width);
        int height = Mathf.CeilToInt(blurElement.element.resolvedStyle.height);

        if (width <= 0 || height <= 0)
            return;

        if (blurElement.croppedRT == null || blurElement.croppedRT.width != width || blurElement.croppedRT.height != height)
        {
            if (blurElement.croppedRT != null)
                blurElement.croppedRT.Release();

            blurElement.croppedRT = new RenderTexture(width, height, 0, sourceBlurTexture.format);
            blurElement.croppedRT.Create();
            blurElement.element.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(blurElement.croppedRT));
        }
    }

    void LateUpdate()
    {
        if (sourceBlurTexture == null || m_Camera == null || m_CommandBuffer == null)
            return;

        m_CommandBuffer.Clear();

        foreach (var blurElement in m_BlurElements)
        {
            if (blurElement.croppedRT == null || blurElement.element == null || blurElement.material == null)
                continue;

            Rect bounds = blurElement.element.worldBound;
            float normalizedX = bounds.xMin / Screen.width;
            float normalizedY = bounds.yMin / Screen.height;
            float normalizedWidth = bounds.width / Screen.width;
            float normalizedHeight = bounds.height / Screen.height;

            var viewportRect = new Vector4(
                normalizedX,
                normalizedY,
                normalizedWidth,
                normalizedHeight
            );

            blurElement.material.SetVector(CropViewportRect, viewportRect);
            blurElement.material.SetTexture(MainTex, sourceBlurTexture);
            blurElement.material.SetColor(ForegroundColor, blurElement.customForegroundColor);

            m_CommandBuffer.SetRenderTarget(blurElement.croppedRT);
            m_CommandBuffer.SetViewport(new Rect(0, 0, blurElement.croppedRT.width, blurElement.croppedRT.height));
            m_CommandBuffer.DrawProcedural(Matrix4x4.identity, blurElement.material, 0, MeshTopology.Triangles, 3);
        }

        Graphics.ExecuteCommandBuffer(m_CommandBuffer);
    }
}