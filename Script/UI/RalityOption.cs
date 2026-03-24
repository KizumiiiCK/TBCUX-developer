using System;
using UnityEngine;

public class RalityOption : MonoBehaviour
{
    [Header("Structure")]
    [SerializeField] private KiButton selectButton;
    [SerializeField] private GameObject selectionArea;
    [SerializeField] private KiButton[] rarityButtons = new KiButton[7];

    private Action<int> onRaritySelected;
    private int selectedRarity;
    private bool initial_text_mod = false;

    private void Awake()
    {
        AutoCacheRefs();
        if (selectionArea != null) selectionArea.SetActive(false);
        BindButtonEvents();
        // ApplySelectionVisual(selectedRarity);
    }

    public void Initialize(Action<int> onSelected, int defaultRarity = 0)
    {
        onRaritySelected = onSelected;
        SetSelectedRarity(defaultRarity, true);
    }

    private void BindButtonEvents()
    {
        if (selectButton != null)
        {
            selectButton.onClick.RemoveListener(ToggleSelectionArea);
            selectButton.onClick.AddListener(ToggleSelectionArea);
        }

        for (int i = 0; i < rarityButtons.Length; i++)
        {
            int idx = i;
            if (rarityButtons[idx] == null) continue;
            rarityButtons[idx].onClick.AddListener(() => SelectRarity(idx));
        }
    }

    private void AutoCacheRefs()
    {
        if (selectButton == null)
        {
            var selectTf = transform.Find("Kibtn-select");
            if (selectTf != null) selectButton = selectTf.GetComponent<KiButton>();
            if (selectButton == null) selectButton = GetComponentInChildren<KiButton>(true);
        }

        if (selectionArea == null)
        {
            var areaTf = transform.Find("selectionArea");
            if (areaTf != null) selectionArea = areaTf.gameObject;
        }

        if (rarityButtons == null || rarityButtons.Length == 0 || HasNullButton(rarityButtons))
        {
            Transform buttonsRoot = null;
            if (selectionArea != null)
            {
                var areaTf = selectionArea.transform;
                var btTf = areaTf.Find("buttons");
                buttonsRoot = btTf != null ? btTf : areaTf;
            }

            if (buttonsRoot != null)
            {
                var found = buttonsRoot.GetComponentsInChildren<KiButton>(true);
                if (found != null && found.Length > 0)
                {
                    rarityButtons = found;
                }
            }
        }
    }

    private bool HasNullButton(KiButton[] buttons)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) return true;
        }
        return false;
    }

    private void ToggleSelectionArea()
    {
        if (selectionArea == null) return;
        selectionArea.SetActive(!selectionArea.activeSelf);
    }

    private void SelectRarity(int rarityIndex)
    {
        SetSelectedRarity(rarityIndex, true);
        if (selectionArea != null) selectionArea.SetActive(false);
    }

    private void SetSelectedRarity(int rarityIndex, bool notify, bool initialyvisual=false)
    {
        selectedRarity = Mathf.Clamp(rarityIndex, 0, rarityButtons.Length - 1);
        ApplySelectionVisual(selectedRarity);
        if (notify) onRaritySelected?.Invoke(selectedRarity);
    }

    private void ApplySelectionVisual(int rarityIndex)
    {
        if (selectButton == null) return;
        if (rarityButtons == null || rarityButtons.Length == 0) return;
        var option = rarityButtons[Mathf.Clamp(rarityIndex, 0, rarityButtons.Length - 1)];
        if (option == null) return;
        if (initial_text_mod)
        {
            selectButton.SetText(option.GetText(), 42);
            selectButton.SetFrameColorPersistent(option.GetInitialColor());
        }
        else
        {
            initial_text_mod = true;
        }
    }
}
