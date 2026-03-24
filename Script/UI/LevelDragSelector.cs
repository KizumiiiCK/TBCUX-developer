using UnityEngine;
using UnityEngine.EventSystems;

public class LevelDragSelector : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private LevelTiler levelTiler;
    [SerializeField] private float dragSensitivity = 1f;

    private bool dragging;

    public void Configure(LevelTiler tiler)
    {
        levelTiler = tiler;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (levelTiler == null) return;
        dragging = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging || levelTiler == null) return;
        levelTiler.ApplyDragDelta(eventData.delta.x * dragSensitivity);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (levelTiler == null) return;
        dragging = false;
        levelTiler.SnapToNearestLevel();
    }
}
