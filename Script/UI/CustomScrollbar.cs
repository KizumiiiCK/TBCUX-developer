using UnityEngine;

public class CustomScrollbar : MonoBehaviour
{
    private BoxCollider2D boxCollider;
    private Vector2 dragStartPos;
    private Vector2 dragStartLocalPos;
    private Vector2 targetPos;
    private bool isDragging = false;

    [SerializeField] private RectTransform target; // 需要移动的子物体
    private float smoothTime = 0.3f; // 用于 Lerp 的平滑系数

    // 限制移动的范围
    [SerializeField] private float maxY = 100f; // 最大 Y 轴移动限制
    private float minY = 0f; // 最小 Y 轴移动限制

    private void Awake()
    {
        // 获取 BoxCollider2D 组件
        boxCollider = GetComponent<BoxCollider2D>();
    }

    private void Update()
    {
        if (Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer)
        {
            HandleAndroidInput();
        }
        else if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
        {
            HandleWindowsInput();
        }
        // 使用 Lerp 平滑移动目标物体
        target.localPosition = Vector2.Lerp(target.localPosition, targetPos, Time.deltaTime / smoothTime);
    }

    private void HandleAndroidInput()
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);

            // 检测触摸开始
            if (touch.phase == TouchPhase.Began && IsPointerOverCollider(touch.position))
            {
                StartDragging();
            }

            // 处理触摸拖动
            if (isDragging && touch.phase == TouchPhase.Moved)
            {
                Drag(touch.position);
            }

            // 检测触摸结束
            if (isDragging && touch.phase == TouchPhase.Ended)
            {
                StopDragging();
            }
        }
    }

    private void HandleWindowsInput()
    {
        // 检测鼠标输入
        if (Input.GetMouseButtonDown(0) && IsPointerOverCollider())
        {
            StartDragging();
        }

        if (isDragging)
        {
            Drag();
        }

        if (Input.GetMouseButtonUp(0))
        {
            StopDragging();
        }
    }

    private bool IsPointerOverCollider()
    {
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        return boxCollider.OverlapPoint(mousePos);
    }

    private bool IsPointerOverCollider(Vector2 touchPosition)
    {
        Vector2 worldPoint = Camera.main.ScreenToWorldPoint(touchPosition);
        return boxCollider.OverlapPoint(worldPoint);
    }

    private void StartDragging()
    {
        isDragging = true;
        dragStartPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        dragStartLocalPos = target.localPosition;
    }

    private void Drag()
    {
        Vector2 currentMousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        float deltaY = (currentMousePos.y - dragStartPos.y) * 50;

        // 计算新的目标位置
        targetPos = new Vector2(dragStartLocalPos.x, dragStartLocalPos.y + deltaY);

        // 限制目标位置在最小和最大 Y 轴之间
        targetPos.y = Mathf.Clamp(targetPos.y, minY, maxY);

        // 使用 Lerp 平滑移动目标物体
        //target.localPosition = Vector2.Lerp(target.localPosition, targetPos, Time.deltaTime / smoothTime);
    }

    private void Drag(Vector2 touchPosition)
    {
        Vector2 currentTouchPos = Camera.main.ScreenToWorldPoint(touchPosition);
        float deltaY = currentTouchPos.y - dragStartPos.y;

        // 计算新的目标位置
        targetPos = new Vector2(dragStartLocalPos.x, dragStartLocalPos.y + deltaY);

        // 限制目标位置在最小和最大 Y 轴之间
        targetPos.y = Mathf.Clamp(targetPos.y, minY, maxY);
    }

    private void StopDragging()
    {
        isDragging = false;
    }
    public void SetMaxY(int sectionNumbers, float gap)
    {
        maxY = Mathf.Clamp(gap*(sectionNumbers-4), 0, Mathf.Infinity);
    }
}

