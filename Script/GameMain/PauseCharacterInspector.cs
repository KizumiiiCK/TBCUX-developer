using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PauseCharacterInspector : MonoBehaviour
{
    private const int HistorySize = 5;
    private const float ClickDragPixels = 20f;
    private const float MaxXDistance = 2f;
    private const float MaxYDistance = 5f;
    private const string StatementPrefabPath = "UI/FunctionalPanels/PauseCharacterStatement";

    [SerializeField] private GameObject statementRoot;

    private readonly Queue<Character> history = new Queue<Character>(HistorySize);
    private readonly List<Character> candidates = new List<Character>();

    private IndexViewerPause statement;
    private Character selectedCharacter;
    private SimpleCommandBufferOutline outline;
    private bool outlineOwned;
    private bool outlinePreviousEnabled;

    private bool pointerActive;
    private int pointerId;
    private Vector2 pointerStart;
    private bool pointerDragged;
    private bool pointerOnUi;

    public Character SelectedCharacter => selectedCharacter;
    public event Action<Character> SelectedCharacterChanged;

    public void Initialize(GameObject existingStatementRoot)
    {
        if (statementRoot == null) statementRoot = existingStatementRoot;
    }

    public void Tick()
    {
        if (selectedCharacter != null && (!selectedCharacter.gameObject || !selectedCharacter.gameObject.activeInHierarchy))
        {
            ClearOutlineAndPanel();
        }

        HandlePointer();
    }

    public void ClearSelection()
    {
        ClearOutlineAndPanel();
        history.Clear();
        pointerActive = false;
        pointerDragged = false;
        pointerOnUi = false;
    }

    private void HandlePointer()
    {
        if (Input.touchCount > 0)
        {
            HandleTouch();
            return;
        }

        HandleMouse();
    }

    private void HandleTouch()
    {
        if (Input.touchCount > 1) pointerDragged = true;

        if (!pointerActive)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase != TouchPhase.Began) continue;
                BeginPointer(touch.position, touch.fingerId);
                return;
            }
            return;
        }

        bool found = false;
        Touch tracked = default;
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.fingerId != pointerId) continue;
            tracked = touch;
            found = true;
            break;
        }

        if (!found)
        {
            FinishPointer(pointerStart);
            return;
        }

        if (Vector2.Distance(tracked.position, pointerStart) > ClickDragPixels)
        {
            pointerDragged = true;
        }

        if (tracked.phase == TouchPhase.Canceled)
        {
            CancelPointer();
            return;
        }

        if (tracked.phase == TouchPhase.Ended)
        {
            FinishPointer(tracked.position);
        }
    }

    private void HandleMouse()
    {
        if (!pointerActive)
        {
            if (!Input.GetMouseButtonDown(0)) return;
            BeginPointer(Input.mousePosition, -1);
            return;
        }

        Vector2 now = Input.mousePosition;
        if (Vector2.Distance(now, pointerStart) > ClickDragPixels)
        {
            pointerDragged = true;
        }

        if (Input.GetMouseButtonUp(0))
        {
            FinishPointer(now);
            return;
        }

        if (!Input.GetMouseButton(0))
        {
            FinishPointer(now);
        }
    }

    private void BeginPointer(Vector2 screenPos, int id)
    {
        pointerActive = true;
        pointerId = id;
        pointerStart = screenPos;
        pointerDragged = false;
        pointerOnUi = IsPointerOnInteractiveUI(screenPos, id);
    }

    private void FinishPointer(Vector2 screenPos)
    {
        bool dragged = pointerDragged;
        bool onUi = pointerOnUi;
        pointerActive = false;
        if (dragged || onUi) return;
        HandleClick(screenPos);
    }

    private void CancelPointer()
    {
        pointerActive = false;
        pointerDragged = true;
    }

    private static bool IsPointerOnInteractiveUI(Vector2 screenPos, int id)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return false;

        PointerEventData pointerData = new PointerEventData(eventSystem)
        {
            position = screenPos,
            pointerId = id
        };

        List<RaycastResult> raycasts = new List<RaycastResult>();
        eventSystem.RaycastAll(pointerData, raycasts);
        for (int i = 0; i < raycasts.Count; i++)
        {
            GameObject hit = raycasts[i].gameObject;
            if (hit == null) continue;
            if (hit.GetComponentInParent<Selectable>() != null) return true;
            if (hit.GetComponentInParent<ScrollRect>() != null) return true;
        }

        return false;
    }

    private void HandleClick(Vector2 screenPos)
    {
        PruneHistory();
        if (history.Count >= HistorySize)
        {
            history.Dequeue();
        }

        Character target = PickCharacter(screenPos);
        if (target == null) return;

        SelectCharacter(target);
        Remember(target);
    }

    private Character PickCharacter(Vector2 screenPos)
    {
        CollectNearPointer(screenPos, candidates);
        if (candidates.Count == 0) return null;

        Character bestOutside = null;
        float bestOutsideDx = float.MaxValue;
        float bestOutsideDy = float.MaxValue;
        Character bestInside = null;
        float bestInsideDx = float.MaxValue;
        float bestInsideDy = float.MaxValue;

        Camera cam = Camera.main;
        if (cam == null) return null;
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, Mathf.Abs(cam.transform.position.z)));
        world.z = 0f;

        for (int i = 0; i < candidates.Count; i++)
        {
            Character candidate = candidates[i];
            if (candidate == null) continue;
            Vector3 pos = candidate.transform.position;
            float dx = Mathf.Abs(pos.x - world.x);
            float dy = Mathf.Abs(pos.y - world.y);
            if (HistoryContains(candidate))
            {
                if (IsCloser(dx, dy, bestInsideDx, bestInsideDy))
                {
                    bestInside = candidate;
                    bestInsideDx = dx;
                    bestInsideDy = dy;
                }
            }
            else if (IsCloser(dx, dy, bestOutsideDx, bestOutsideDy))
            {
                bestOutside = candidate;
                bestOutsideDx = dx;
                bestOutsideDy = dy;
            }
        }

        return bestOutside != null ? bestOutside : bestInside;
    }

    private static bool IsCloser(float dx, float dy, float bestDx, float bestDy)
    {
        return dx < bestDx || (Mathf.Approximately(dx, bestDx) && dy < bestDy);
    }

    private static void CollectNearPointer(Vector2 screenPos, List<Character> results)
    {
        results.Clear();
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, Mathf.Abs(cam.transform.position.z)));
        world.z = 0f;

        CatCharacter[] cats = FindObjectsOfType<CatCharacter>();
        for (int i = 0; i < cats.Length; i++)
        {
            if (IsNearPointer(cats[i], world)) results.Add(cats[i]);
        }

        EnemyCharacter[] enemies = FindObjectsOfType<EnemyCharacter>();
        for (int i = 0; i < enemies.Length; i++)
        {
            if (IsNearPointer(enemies[i], world)) results.Add(enemies[i]);
        }
    }

    private static bool IsNearPointer(Character candidate, Vector3 clickWorld)
    {
        if (candidate == null || candidate.gameObject == null || !candidate.gameObject.activeInHierarchy) return false;
        Vector3 pos = candidate.transform.position;
        return Mathf.Abs(pos.x - clickWorld.x) <= MaxXDistance && Mathf.Abs(pos.y - clickWorld.y) <= MaxYDistance;
    }

    private void SelectCharacter(Character target)
    {
        if (target == null) return;
        if (selectedCharacter == target)
        {
            ShowStatement(target);
            return;
        }

        ClearOutlineAndPanel();
        ApplySelection(target);
    }

    private void Remember(Character target)
    {
        if (target == null) return;
        if (HistoryContains(target)) return;
        history.Enqueue(target);
    }

    private bool HistoryContains(Character target)
    {
        if (target == null) return false;
        foreach (Character shown in history)
        {
            if (shown == target) return true;
        }
        return false;
    }

    private void PruneHistory()
    {
        if (history.Count == 0) return;
        Character[] snapshot = history.ToArray();
        history.Clear();
        for (int i = 0; i < snapshot.Length; i++)
        {
            Character shown = snapshot[i];
            if (shown == null || shown.gameObject == null || !shown.gameObject.activeInHierarchy) continue;
            history.Enqueue(shown);
        }
    }

    private void ApplySelection(Character target)
    {
        if (target == null) return;
        SimpleCommandBufferOutline existing = target.GetComponent<SimpleCommandBufferOutline>();
        bool created = false;
        if (existing == null)
        {
            existing = target.gameObject.AddComponent<SimpleCommandBufferOutline>();
            created = true;
            outlinePreviousEnabled = false;
        }
        else outlinePreviousEnabled = existing.enabled;

        existing.RefreshTargets();
        existing.SetColor(new Color(1f, 0.92f, 0.1f, 1f));
        existing.SetHighlightColor(Color.white);
        existing.SetActive(true);

        outline = existing;
        outlineOwned = created;
        SetSelectedCharacter(target);
        ShowStatement(target);
    }

    private void ClearOutlineAndPanel()
    {
        if (outline != null)
        {
            if (outlineOwned) Destroy(outline);
            else outline.SetActive(outlinePreviousEnabled);
        }

        if (statement != null)
        {
            statement.HidePanel();
        }

        outline = null;
        outlineOwned = false;
        outlinePreviousEnabled = false;
        SetSelectedCharacter(null);
    }

    private void ShowStatement(Character target)
    {
        if (target == null) return;
        EnsureStatementLoaded();
        if (statement == null) return;
        UpdateStatementPosition(target);
        statement.ShowCharacter(target);
    }

    private void EnsureStatementLoaded()
    {
        if (statement != null && statementRoot != null) return;

        GameObject prefab = Resources.Load<GameObject>(StatementPrefabPath);
        Transform parent = GameObject.Find("UI Canvas")?.transform;

        if (statementRoot == null)
        {
            if (prefab != null)
            {
                statementRoot = Instantiate(prefab, parent, false);
            }
            else
            {
                statementRoot = new GameObject("PauseCharacterStatement(Runtime)");
                RectTransform rt = statementRoot.AddComponent<RectTransform>();
                rt.SetParent(parent, false);
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(640f, 360f);
                rt.anchoredPosition = Vector2.zero;
            }

            statementRoot.SetActive(false);
        }

        statement = statementRoot.GetComponent<IndexViewerPause>();
        if (statement == null)
        {
            statement = statementRoot.AddComponent<IndexViewerPause>();
        }
    }

    private void UpdateStatementPosition(Character target)
    {
        if (statementRoot == null || target == null) return;
        RectTransform panelRect = statementRoot.GetComponent<RectTransform>();
        if (panelRect == null) return;

        Camera cam = Camera.main;
        if (cam == null)
        {
            panelRect.anchoredPosition = new Vector2(800f, 0f);
            return;
        }

        float viewportX = cam.WorldToViewportPoint(target.transform.position).x;
        bool isCat = target is CatCharacter;
        float matchScreenX = (viewportX - 0.5f) * Screen.width;
        float panelX;
        if (isCat)
        {
            bool inRightTwoThirds = viewportX >= (1f / 3f);
            panelX = inRightTwoThirds ? matchScreenX - 800f : 800f;
        }
        else
        {
            bool inLeftTwoThirds = viewportX <= (2f / 3f);
            panelX = inLeftTwoThirds ? matchScreenX + 800f : -800f;
        }

        panelRect.anchoredPosition = new Vector2(panelX, 0f);
    }

    private void SetSelectedCharacter(Character target)
    {
        if (selectedCharacter == target) return;
        selectedCharacter = target;
        SelectedCharacterChanged?.Invoke(selectedCharacter);
    }
}
