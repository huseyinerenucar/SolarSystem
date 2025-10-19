using UnityEngine;
using UnityEngine.InputSystem;

public class DebugMovementScript : MonoBehaviour
{
    private Camera playerCamera;
    public float moveSpeed = 100000f;
    public float sprintAdding = 1000f;
    public float slowSubs = 1000f;
    public float mouseSensitivity = 0.15f;
    public bool invertY = false;
    public float pitchMin = -85f, pitchMax = 85f;
    [SerializeField] private ScriptableVariables scriptableVariables;

    private InputAction moveAction, lookAction, sprintAction, slowAction, toggleUIAction;
    private float yaw, pitch;

    void Awake()
    {
        playerCamera = GetComponentInChildren<Camera>();
        SetupInput();
        SetCameraMode();
        yaw = transform.eulerAngles.y;
        if (playerCamera != null)
            pitch = NormalizeAngle(playerCamera.transform.localEulerAngles.x);
    }

    void OnEnable()
    {
        moveAction.Enable();
        lookAction.Enable();
        sprintAction.Enable();
        slowAction.Enable();
        toggleUIAction.Enable();
    }

    void OnDisable()
    {
        moveAction.Disable();
        lookAction.Disable();
        sprintAction.Disable();
        slowAction.Disable();
        toggleUIAction.Disable();
    }

    void Update()
    {
        if (toggleUIAction.WasPressedThisFrame())
        {
            scriptableVariables.isUIMode = !scriptableVariables.isUIMode;
            SetCameraMode();
        }

        if (!scriptableVariables.isUIMode)
        {
            HandleLook();
            HandleMovement();
        }
    }

    void SetCameraMode()
    {
        if (scriptableVariables.isUIMode)
        {
            scriptableVariables.timeSpeedPanelWidth = new UnityEngine.UIElements.Length(StaticVariables.TimeSpeedPanelWidth, UnityEngine.UIElements.LengthUnit.Percent);
            scriptableVariables.timeSpeedPanelHeight = new UnityEngine.UIElements.Length(StaticVariables.TimeSpeedPanelHeight, UnityEngine.UIElements.LengthUnit.Percent);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            scriptableVariables.timeSpeedPanelWidth = new UnityEngine.UIElements.Length(StaticVariables.TimeSpeedPanelWidth / 3, UnityEngine.UIElements.LengthUnit.Percent);
            scriptableVariables.timeSpeedPanelHeight = new UnityEngine.UIElements.Length(StaticVariables.TimeSpeedPanelHeight / 2, UnityEngine.UIElements.LengthUnit.Percent);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void HandleLook()
    {
        Vector2 look = lookAction.ReadValue<Vector2>();
        yaw += look.x * mouseSensitivity;
        pitch += look.y * mouseSensitivity * (invertY ? 1f : -1f);
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void HandleMovement()
    {
        Vector2 move = moveAction.ReadValue<Vector2>();
        Vector3 input = new(move.x, 0f, move.y);
        Vector3 worldMove = playerCamera.transform.TransformDirection(input);

        if (sprintAction.IsPressed())
            moveSpeed += sprintAdding;
        else if (slowAction.IsPressed())
            moveSpeed -= slowSubs;

        transform.position += moveSpeed * Time.deltaTime * worldMove;
    }

    void SetupInput()
    {
        var map = new InputActionMap("SpaceMove");

        moveAction = map.AddAction("Move");
        var moveComposite = moveAction.AddCompositeBinding("2DVector");
        moveComposite.With("Up", "<Keyboard>/w");
        moveComposite.With("Down", "<Keyboard>/s");
        moveComposite.With("Left", "<Keyboard>/a");
        moveComposite.With("Right", "<Keyboard>/d");

        lookAction = map.AddAction("Look");
        lookAction.AddBinding("<Mouse>/delta");

        sprintAction = map.AddAction("Sprint", binding: "<Keyboard>/leftShift");
        slowAction = map.AddAction("Slow", binding: "<Keyboard>/leftCtrl");

        toggleUIAction = map.AddAction("ToggleUI", binding: "<Keyboard>/escape");
    }

    static float NormalizeAngle(float a)
    {
        while (a > 180f) a -= 360f;
        while (a < -180f) a += 360f;
        return a;
    }
}