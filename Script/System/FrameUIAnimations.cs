using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FrameUIAnimations : MonoBehaviour
{
    public const float DoorDuration = 0.5f;

    [Header("Doors")]
    [SerializeField] private RectTransform leftDoor;
    [SerializeField] private RectTransform rightDoor;
    private static readonly Vector2 leftClosedPos = new Vector2(0, 0);
    private static readonly Vector2 rightClosedPos = new Vector2(0, 0);
    private static readonly Vector2 leftOpenPos = new Vector2(-1500, 0);
    private static readonly Vector2 rightOpenPos = new Vector2(1500, 0);
    public bool startOpened = false;

    private Coroutine doorRoutine;

    private void Awake()
    {
        ApplyImmediate(startOpened);
    }

    public void OpenDoor()
    {
        StartDoorRoutine(true);
    }

    public void CloseDoor()
    {
        StartDoorRoutine(false);
    }

    public void SetDoorSprites(Sprite leftSprite, Sprite rightSprite)
    {
        if (leftDoor != null)
        {
            var leftImage = leftDoor.GetComponent<Image>();
            if (leftImage != null) leftImage.sprite = leftSprite;
        }
        if (rightDoor != null)
        {
            var rightImage = rightDoor.GetComponent<Image>();
            if (rightImage != null) rightImage.sprite = rightSprite;
        }
    }

    private void StartDoorRoutine(bool open)
    {
        if (leftDoor == null || rightDoor == null) return;
        if (doorRoutine != null) StopCoroutine(doorRoutine);
        doorRoutine = StartCoroutine(DoorRoutine(open));
    }

    private IEnumerator DoorRoutine(bool open)
    {
        Debug.Log("StartDoorRoutine: " + open);
        Vector2 lStart = leftDoor.anchoredPosition;
        Vector2 rStart = rightDoor.anchoredPosition;
        Vector2 lEnd = open ? leftOpenPos : leftClosedPos;
        Vector2 rEnd = open ? rightOpenPos : rightClosedPos;

        float t = 0f;
        float d = Mathf.Max(0.01f, DoorDuration);
        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / d);
            leftDoor.anchoredPosition = Vector2.LerpUnclamped(lStart, lEnd, k);
            rightDoor.anchoredPosition = Vector2.LerpUnclamped(rStart, rEnd, k);
            yield return null;
        }

        leftDoor.anchoredPosition = lEnd;
        rightDoor.anchoredPosition = rEnd;
        doorRoutine = null;
    }

    private void ApplyImmediate(bool open)
    {
        if (leftDoor != null) leftDoor.anchoredPosition = open ? leftOpenPos : leftClosedPos;
        if (rightDoor != null) rightDoor.anchoredPosition = open ? rightOpenPos : rightClosedPos;
    }
}
