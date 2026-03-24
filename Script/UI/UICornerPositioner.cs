using UnityEngine;

public class UICornerPositioner : MonoBehaviour
{
    [SerializeField]private RectTransform topLeft;    // 左上角的 UI 元素
    [SerializeField] private RectTransform topRight;   // 右上角的 UI 元素
    [SerializeField] private RectTransform bottomLeft;  // 左下角的 UI 元素
    [SerializeField] private RectTransform bottomRight; // 右下角的 UI 元素
    [SerializeField] private RectTransform leftPinned;
    [SerializeField] private RectTransform rightPinned;

    void Start()
    {
        PositionCorners();
    }

    void PositionCorners()
    {
        RectTransform canvasRect = GetComponent<RectTransform>();
        Vector2 canvasSize = canvasRect.sizeDelta;

        if(topLeft!=null)topLeft.anchoredPosition = new Vector2(-canvasSize.x/2, canvasSize.y / 2); // 左上角
        if (topRight != null) topRight.anchoredPosition = new Vector2(canvasSize.x/2, canvasSize.y / 2); // 右上角
        if (bottomLeft != null) bottomLeft.anchoredPosition = new Vector2(-canvasSize.x/2, -canvasSize.y / 2); // 左下角
        if (bottomRight != null) bottomRight.anchoredPosition = new Vector2(canvasSize.x/2, -canvasSize.y / 2); // 右下角
        if (leftPinned != null) leftPinned.anchoredPosition = new Vector2(-canvasSize.x/2,0);
        if (rightPinned != null) rightPinned.anchoredPosition = new Vector2(canvasSize.x/2,0);
    }
}

