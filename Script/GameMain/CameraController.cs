using UnityEngine;
using UnityEngine.EventSystems;

public class CameraController : MonoBehaviour
{
    [SerializeField] private float minSize = 8f; // 相机最小size值
    [SerializeField] private float maxSize = 20f; // 相机最大size值
    [SerializeField] private float limitation = 20f; // 相机最大size值
    [Header("Input Feel")]
    [SerializeField] private float touchZoomSensitivity = 0.01f;
    [SerializeField] private float mouseZoomSensitivity = 1f;
    [SerializeField] private float maxPanSpeed = 35f;
    [SerializeField] private float longDragStopThreshold = 0.2f;
    private float leftLimit;
    private float rightLimit;
    private Camera cam;

    private float currentSize;
    private float currentPositionX; // 当前相机X轴位置
    private float currentVelocityX; // 当前X轴速度
    private bool touchDragging;
    private bool mouseDragging;
    private Vector2 lastMousePosition;
    private float touchDragDuration;
    private float mouseDragDuration;

    private void Start()
    {
        cam = GetComponent<Camera>();
        currentSize = minSize;
        cam.orthographicSize = currentSize;
        leftLimit = -limitation;
        rightLimit = limitation;
    }

    private void Update()
    {
        rightLimit = limitation * (limitation - currentSize) / (maxSize - minSize);
        leftLimit = -rightLimit;
        // 检测平台
        if (Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer)
        {
            HandleAndroidInput();
        }
        else if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
        {
            HandleWindowsInput();
        }
        // 不做平滑，直接应用目标size
        cam.orthographicSize = currentSize;
        // 更新相机位置
        transform.Translate(new Vector3(currentVelocityX*Time.unscaledDeltaTime, 0, 0));
        currentPositionX = transform.position.x;
        currentPositionX = Mathf.Clamp(currentPositionX, leftLimit, rightLimit);
        transform.position = new Vector3(currentPositionX, transform.position.y, transform.position.z);
    }

    private void HandleAndroidInput()
    {
        // 处理双指缩放
        if (Input.touchCount == 2)
        {
            touchDragging = false;
            touchDragDuration = 0f;
            // 缩放时直接打断移动惯性
            currentVelocityX = 0f;
            Touch touchZero = Input.GetTouch(0);
            Touch touchOne = Input.GetTouch(1);

            Vector2 touchZeroPrevPos = touchZero.position - touchZero.deltaPosition;
            Vector2 touchOnePrevPos = touchOne.position - touchOne.deltaPosition;

            float prevTouchDeltaMag = (touchZeroPrevPos - touchOnePrevPos).magnitude;
            float touchDeltaMag = (touchZero.position - touchOne.position).magnitude;

            float deltaMagnitudeDiff = prevTouchDeltaMag - touchDeltaMag;

            currentSize += deltaMagnitudeDiff * touchZoomSensitivity; // 使用 currentSize 来替代 targetSize
            currentSize = Mathf.Clamp(currentSize, minSize, Mathf.Min(limitation, maxSize));
        }
        // 处理单指左右滑动
        else if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    touchDragging = !IsPointerOverUI(touch.fingerId);
                    touchDragDuration = 0f;
                    break;
                case TouchPhase.Moved:
                    if (touchDragging)
                    {
                        touchDragDuration += touch.deltaTime;
                        ApplyDragDeltaX(touch.deltaPosition.x);
                    }
                    break;
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    if (touchDragging && touchDragDuration > longDragStopThreshold) currentVelocityX = 0f;
                    touchDragging = false;
                    touchDragDuration = 0f;
                    break;
            }
        }
        else
        {
            touchDragging = false;
            touchDragDuration = 0f;
        }
    }

    private void HandleWindowsInput()
    {
        // 处理鼠标滚轮缩放
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            currentSize -= scroll * mouseZoomSensitivity;
            currentSize = Mathf.Clamp(currentSize, minSize, Mathf.Min(limitation, maxSize));
            // 缩放时直接打断移动惯性
            currentVelocityX = 0f;
            mouseDragging = false;
            mouseDragDuration = 0f;
        }
        // 处理鼠标左键左右滑动
        if (Input.GetMouseButtonDown(0))
        {
            mouseDragging = !IsPointerOverUI();
            lastMousePosition = Input.mousePosition;
            mouseDragDuration = 0f;
        }
        if (Input.GetMouseButton(0))
        {
            if (mouseDragging)
            {
                Vector2 now = Input.mousePosition;
                float deltaX = now.x - lastMousePosition.x;
                mouseDragDuration += Time.unscaledDeltaTime;
                ApplyDragDeltaX(deltaX);
                lastMousePosition = now;
            }
        }
        if (Input.GetMouseButtonUp(0))
        {
            if (mouseDragging && mouseDragDuration > longDragStopThreshold) currentVelocityX = 0f;
            mouseDragging = false;
            mouseDragDuration = 0f;
        }
    }

    private void ApplyDragDeltaX(float deltaXPixel)
    {
        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        float worldPerPixel = (cam.orthographicSize * 2f) / Mathf.Max(1, Screen.height);
        float targetVelocity = -(deltaXPixel * worldPerPixel) / dt;
        targetVelocity = Mathf.Clamp(targetVelocity, -maxPanSpeed, maxPanSpeed);
        // 不做平滑，保持即时速度响应
        currentVelocityX = targetVelocity;
    }

    private bool IsPointerOverUI(int fingerId = -1)
    {
        if (EventSystem.current == null) return false;
        return fingerId >= 0 ? EventSystem.current.IsPointerOverGameObject(fingerId) : EventSystem.current.IsPointerOverGameObject();
    }
    public void SetLimitation(float mapsize)
    {
        int minLimitTolerance = 2100;
        int MAX = 6000;
        limitation = minSize + (maxSize - minSize) * (Mathf.Clamp(mapsize-minLimitTolerance, 0, MAX-minLimitTolerance) / (MAX-minLimitTolerance));
        transform.position = new Vector3(Mathf.Clamp((mapsize-minLimitTolerance)/200-3.5f, 0, 18.5f), transform.position.y,-10);
    }
}

