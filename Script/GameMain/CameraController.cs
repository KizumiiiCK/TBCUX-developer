using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private float minSize = 8f; // 相机最小size值
    [SerializeField] private float maxSize = 20f; // 相机最大size值
    [SerializeField] private float limitation = 20f; // 相机最大size值
    private float leftLimit;
    private float rightLimit;
    private Camera cam;
    private BoxCollider2D boxCollider;

    private float currentSize;
    private float currentPositionX; // 当前相机X轴位置
    private float currentVelocityX; // 当前X轴速度
    private static Vector2 bcMinsize = new Vector2(50, 13);
    private float timestacker = 0;

    private void Start()
    {
        cam = GetComponent<Camera>();
        boxCollider = GetComponent<BoxCollider2D>();
        currentSize = minSize;
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
        // 平滑过渡到目标size
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, currentSize, Time.unscaledDeltaTime * 20);
        float bcrescale = (currentSize - minSize) / (maxSize - minSize);
        boxCollider.size = bcMinsize * (1 + bcrescale * 1.5f);
        boxCollider.offset = new Vector2(0, 3 + bcrescale * 4);
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
            Touch touchZero = Input.GetTouch(0);
            Touch touchOne = Input.GetTouch(1);

            Vector2 touchZeroPrevPos = touchZero.position - touchZero.deltaPosition;
            Vector2 touchOnePrevPos = touchOne.position - touchOne.deltaPosition;

            float prevTouchDeltaMag = (touchZeroPrevPos - touchOnePrevPos).magnitude;
            float touchDeltaMag = (touchZero.position - touchOne.position).magnitude;

            float deltaMagnitudeDiff = prevTouchDeltaMag - touchDeltaMag;

            currentSize += deltaMagnitudeDiff * 0.01f; // 使用 currentSize 来替代 targetSize
            currentSize = Mathf.Clamp(currentSize, minSize, Mathf.Min(limitation, maxSize));
        }
        // 处理单指左右滑动
        else if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            Vector2 touchPosition = Camera.main.ScreenToWorldPoint(touch.position);
            if (boxCollider.OverlapPoint(touchPosition))
            {
                switch (touch.phase)
                {
                    case TouchPhase.Moved:
                        Vector2 deltaMove = touch.deltaPosition;
                        /*if(Mathf.Abs(deltaMove.x)>0.001f) */
                        currentVelocityX = -deltaMove.x / Time.unscaledDeltaTime / 50; // 记录速度
                        timestacker += Time.unscaledDeltaTime;
                        break;

                    case TouchPhase.Ended:
                        if (timestacker > 0.2f) currentVelocityX = 0;
                        break;
                    case TouchPhase.Canceled:
                        if (timestacker > 0.2f) currentVelocityX = 0;
                        break;
                }
                
            }
        }
        else timestacker = 0;
    }

    private void HandleWindowsInput()
    {
        // 处理鼠标滚轮缩放
        float scroll = Input.mouseScrollDelta.y;
        currentSize -= scroll;
        currentSize = Mathf.Clamp(currentSize, minSize, Mathf.Min(limitation,maxSize));
        // 处理鼠标左键左右滑动
        if (Input.GetMouseButton(0))
        {
            Vector2 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            if (boxCollider.OverlapPoint(mousePosition))
            {
                float deltaX = Input.GetAxis("Mouse X");
                /*if (Mathf.Abs(deltaX) > 0.001f) */
                currentVelocityX = -deltaX / Time.unscaledDeltaTime / 3; // 记录速度
                timestacker += Time.unscaledDeltaTime;
            }
        }
        else timestacker = 0;
    }
    public void SetLimitation(float mapsize)
    {
        int minLimitTolerance = 2100;
        int MAX = 6000;
        limitation = minSize + (maxSize - minSize) * (Mathf.Clamp(mapsize-minLimitTolerance, 0, MAX-minLimitTolerance) / (MAX-minLimitTolerance));
        transform.position = new Vector3(Mathf.Clamp((mapsize-minLimitTolerance)/200-3.5f, 0, 18.5f), transform.position.y,-10);
    }
}

