using UnityEngine;
using UnityEngine.InputSystem;

public class RTSCameraController : MonoBehaviour
{
    [Header("Hierarchy")]
    [Tooltip("Das direkte Child 'CameraHolder' von CameraRoot.")]
    [SerializeField] private Transform cameraHolder;

    [Header("Map bounds")]
    [SerializeField] private Vector2 boundsX = new(-100f, 100f);
    [SerializeField] private Vector2 boundsZ = new(-100f, 100f);

    [Header("WASD movement")]
    [SerializeField] private float panSpeed = 30f;
    [SerializeField] private float panSmoothing = 12f;

    [Header("Right-click drag")]
    [SerializeField] private float dragSmoothing = 20f;
    [SerializeField] private float groundHeight = 0f;

    [Header("Rotation (Q/E)")]
    [SerializeField] private float rotationSpeed = 120f;
    [SerializeField] private float rotationSmoothing = 14f;

    [Header("Zoom (mouse wheel)")]
    [SerializeField] private float zoomSpeed = 8f;
    [SerializeField] private float zoomSmoothing = 14f;
    [SerializeField] private float minZoomDistance = 12f;
    [SerializeField] private float maxZoomDistance = 80f;

    private Camera mainCamera;
    private Plane groundPlane;

    private Vector3 targetRootPosition;
    private float targetRootYaw;
    private Vector3 targetHolderLocalPosition;

    private bool isDragging;
    private bool dragHasGroundPoint;
    private Vector3 dragStartGroundPoint;
    private Vector3 dragStartRootPosition;

    private void Awake()
    {
        mainCamera = GetComponentInChildren<Camera>();

        if (mainCamera == null)
        {
            Debug.LogError("Keine Camera unterhalb von CameraRoot gefunden.");
            enabled = false;
            return;
        }

        if (cameraHolder == null)
        {
            Debug.LogError("CameraHolder fehlt. Ziehe das Objekt im Inspector in das Feld.");
            enabled = false;
            return;
        }

        targetRootPosition = transform.position;
        targetRootYaw = transform.eulerAngles.y;
        targetHolderLocalPosition = cameraHolder.localPosition;

        groundPlane = new Plane(Vector3.up, new Vector3(0f, groundHeight, 0f));

        ClampZoomTarget();
    }

    private void Update()
    {
        if (Keyboard.current == null || Mouse.current == null)
            return;

        HandleDrag();
        HandleKeyboardMovement();
        HandleRotation();
        HandleZoom();
        ApplyTargets();
    }

    private void HandleKeyboardMovement()
    {
        if (isDragging)
            return;

        Vector3 direction = Vector3.zero;

        if (Keyboard.current.wKey.isPressed)
            direction += transform.forward;

        if (Keyboard.current.sKey.isPressed)
            direction -= transform.forward;

        if (Keyboard.current.dKey.isPressed)
            direction += transform.right;

        if (Keyboard.current.aKey.isPressed)
            direction -= transform.right;

        direction.y = 0f;

        if (direction.sqrMagnitude > 1f)
            direction.Normalize();

        targetRootPosition += direction * panSpeed * Time.deltaTime;
        ClampRootTarget();
    }

    private void HandleDrag()
    {
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            isDragging = true;
            dragHasGroundPoint = TryGetGroundPoint(
                Mouse.current.position.ReadValue(),
                out dragStartGroundPoint
            );

            dragStartRootPosition = targetRootPosition;
        }

        if (Mouse.current.rightButton.wasReleasedThisFrame)
        {
            isDragging = false;
            dragHasGroundPoint = false;
        }

        if (!isDragging || !dragHasGroundPoint)
            return;

        if (!TryGetGroundPoint(Mouse.current.position.ReadValue(), out Vector3 currentGroundPoint))
            return;

        // Der beim Rechtsklick "gegriffene" Bodenpunkt bleibt unter dem Mauszeiger.
        Vector3 dragOffset = dragStartGroundPoint - currentGroundPoint;
        dragOffset.y = 0f;

        targetRootPosition = dragStartRootPosition + dragOffset;
        ClampRootTarget();
    }

    private void HandleRotation()
    {
        float rotationInput = 0f;

        if (Keyboard.current.qKey.isPressed)
            rotationInput -= 1f;

        if (Keyboard.current.eKey.isPressed)
            rotationInput += 1f;

        targetRootYaw += rotationInput * rotationSpeed * Time.deltaTime;
    }

    private void HandleZoom()
    {
        float scroll = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) < 0.01f)
            return;

        // Scrollwerte können je nach Maus/Trackpad variieren.
        // Daher in kleine, verlässliche Zoom-Schritte umsetzen.
        float scrollSteps = scroll / 120f;

        // Holder-Richtung vom Root nach außen. Dadurch bleibt der Blickwinkel erhalten.
        Vector3 zoomDirection = targetHolderLocalPosition.normalized;

        if (zoomDirection.sqrMagnitude < 0.001f)
            zoomDirection = new Vector3(0f, 0.7f, -0.7f).normalized;

        targetHolderLocalPosition += zoomDirection * scrollSteps * zoomSpeed;
        ClampZoomTarget();
    }

    private void ApplyTargets()
    {
        float panT = 1f - Mathf.Exp(-panSmoothing * Time.deltaTime);
        float rotationT = 1f - Mathf.Exp(-rotationSmoothing * Time.deltaTime);
        float zoomT = 1f - Mathf.Exp(-zoomSmoothing * Time.deltaTime);

        transform.position = Vector3.Lerp(
            transform.position,
            targetRootPosition,
            panT
        );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.Euler(0f, targetRootYaw, 0f),
            rotationT
        );

        cameraHolder.localPosition = Vector3.Lerp(
            cameraHolder.localPosition,
            targetHolderLocalPosition,
            zoomT
        );
    }

    private bool TryGetGroundPoint(Vector2 screenPosition, out Vector3 groundPoint)
    {
        Ray ray = mainCamera.ScreenPointToRay(screenPosition);

        if (groundPlane.Raycast(ray, out float distance))
        {
            groundPoint = ray.GetPoint(distance);
            return true;
        }

        groundPoint = default;
        return false;
    }

    private void ClampRootTarget()
    {
        targetRootPosition.x = Mathf.Clamp(
            targetRootPosition.x,
            boundsX.x,
            boundsX.y
        );

        targetRootPosition.z = Mathf.Clamp(
            targetRootPosition.z,
            boundsZ.x,
            boundsZ.y
        );
    }

    private void ClampZoomTarget()
    {
        float distance = targetHolderLocalPosition.magnitude;
        float clampedDistance = Mathf.Clamp(
            distance,
            minZoomDistance,
            maxZoomDistance
        );

        targetHolderLocalPosition =
            targetHolderLocalPosition.normalized * clampedDistance;
    }
}