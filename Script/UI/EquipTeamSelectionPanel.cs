using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EquipTeamSelectionPanel : MonoBehaviour
{
    [Header("Selection Slots")]
    [SerializeField] private KiButton[] currentSelectedButtons = new KiButton[13];
    [SerializeField] private Sprite emptySlotSprite;

    [Header("Reorder Panel")]
    [SerializeField] private GameObject changePositionSlide;
    [SerializeField] private Button[] changePositionButtons = new Button[10];
    [SerializeField] private Button removeCharacterButton;

    [Header("Team Header")]
    [SerializeField] private Button changeTeamPrevButton;
    [SerializeField] private Button changeTeamNextButton;
    [SerializeField] private TMP_InputField teamNameInput;

    [Header("Feedback")]
    [SerializeField] private float normalScale = 1.8f;
    [SerializeField] private float maxScaleMultiplier = 1.5f;
    [SerializeField] private float feedbackDuration = 0.3f;

    private Action<int> onSlotClicked;
    private Action<int> onSwapTargetClicked;
    private Action onRemoveClicked;
    private Action<bool> onChangeTeamClicked;
    private Action<string> onTeamNameChanged;
    private readonly Coroutine[] feedbackCoroutines = new Coroutine[13];
    private readonly string[] cachedCharCodes = new string[13];
    private bool suppressTeamNameNotify;

    public void Initialize(
        Action<int> onSlotClicked,
        Action<int> onSwapTargetClicked,
        Action onRemoveClicked,
        Action<bool> onChangeTeamClicked,
        Action<string> onTeamNameChanged)
    {
        this.onSlotClicked = onSlotClicked;
        this.onSwapTargetClicked = onSwapTargetClicked;
        this.onRemoveClicked = onRemoveClicked;
        this.onChangeTeamClicked = onChangeTeamClicked;
        this.onTeamNameChanged = onTeamNameChanged;

        BindEvents();
        HideSwapPanel();
    }

    public void SetTeamDisplay(int teamNumber, string teamName)
    {
        if (teamNameInput == null) return;
        suppressTeamNameNotify = true;
        string fallback = $"Team {teamNumber + 1}";
        teamNameInput.text = string.IsNullOrWhiteSpace(teamName) ? fallback : teamName.Trim();
        suppressTeamNameNotify = false;
    }

    public void RefreshSlots(string[] charCodes)
    {
        if (charCodes == null) return;
        int max = Mathf.Min(currentSelectedButtons.Length, charCodes.Length);
        Array.Clear(cachedCharCodes, 0, cachedCharCodes.Length);
        Array.Copy(charCodes, cachedCharCodes, max);
        for (int i = 0; i < max; i++)
        {
            ApplySlotVisual(i, charCodes[i]);
        }
    }

    public void ShowSwapPanel(int selectedSlot)
    {
        if (changePositionSlide != null) changePositionSlide.SetActive(true);
        for (int i = 0; i < changePositionButtons.Length; i++)
        {
            var btn = changePositionButtons[i];
            if (btn == null) continue;
            Image img = btn.GetComponent<Image>();
            if (img == null) continue;
            if (i == selectedSlot && selectedSlot >= 0 && selectedSlot < currentSelectedButtons.Length && currentSelectedButtons[selectedSlot] != null)
            {
                Sprite selectedIcon = ResolveIconSprite(cachedCharCodes[selectedSlot]);
                img.sprite = selectedIcon != null ? selectedIcon : emptySlotSprite;
            }
            else
            {
                img.sprite = emptySlotSprite;
            }
        }
    }

    public void HideSwapPanel()
    {
        if (changePositionSlide != null) changePositionSlide.SetActive(false);
    }

    public void PlaySlotChangeFeedback(int position)
    {
        if (position < 0 || position >= currentSelectedButtons.Length) return;
        var btn = currentSelectedButtons[position];
        if (btn == null) return;

        if (feedbackCoroutines[position] != null)
        {
            StopCoroutine(feedbackCoroutines[position]);
            feedbackCoroutines[position] = null;
        }
        feedbackCoroutines[position] = StartCoroutine(PlaySlotFeedbackRoutine(position, btn.transform));
    }

    private void BindEvents()
    {
        for (int i = 0; i < currentSelectedButtons.Length; i++)
        {
            int idx = i;
            if (currentSelectedButtons[idx] == null) continue;
            currentSelectedButtons[idx].onClick.RemoveAllListeners();
            currentSelectedButtons[idx].onClick.AddListener(() => onSlotClicked?.Invoke(idx));
        }

        for (int i = 0; i < changePositionButtons.Length; i++)
        {
            int idx = i;
            if (changePositionButtons[idx] == null) continue;
            changePositionButtons[idx].onClick.RemoveAllListeners();
            changePositionButtons[idx].onClick.AddListener(() => onSwapTargetClicked?.Invoke(idx));
        }

        if (removeCharacterButton != null)
        {
            removeCharacterButton.onClick.RemoveAllListeners();
            removeCharacterButton.onClick.AddListener(() => onRemoveClicked?.Invoke());
        }

        if (changeTeamPrevButton != null)
        {
            changeTeamPrevButton.onClick.RemoveAllListeners();
            changeTeamPrevButton.onClick.AddListener(() => onChangeTeamClicked?.Invoke(false));
        }
        if (changeTeamNextButton != null)
        {
            changeTeamNextButton.onClick.RemoveAllListeners();
            changeTeamNextButton.onClick.AddListener(() => onChangeTeamClicked?.Invoke(true));
        }
        if (teamNameInput != null)
        {
            teamNameInput.onValueChanged.RemoveListener(OnTeamNameInputValueChanged);
            teamNameInput.onValueChanged.AddListener(OnTeamNameInputValueChanged);
        }
    }

    private void OnTeamNameInputValueChanged(string value)
    {
        if (suppressTeamNameNotify) return;
        onTeamNameChanged?.Invoke(value);
    }

    private void ApplySlotVisual(int slotIndex, string fullCode)
    {
        if (slotIndex < 0 || slotIndex >= currentSelectedButtons.Length) return;
        var btn = currentSelectedButtons[slotIndex];
        if (btn == null) return;

        TMP_Text costText = btn.transform.childCount > 0 ? btn.transform.GetChild(0).GetComponent<TMP_Text>() : null;
        Image image = btn.GetComponent<Image>();
        if (image == null) return;

        if (string.IsNullOrEmpty(fullCode))
        {
            btn.SetCover(null);
            btn.SetOutfit(KiOutfit.Panel, 0);
            if (costText != null) costText.text = string.Empty;
            return;
        }

        int rarity = fullCode[0] - '0';
        Sprite icon = ResolveIconSprite(fullCode);
        CharacterData cd = Resources.Load<CharacterData>($"Units/Cat Units/{fullCode[0]}/{fullCode.Substring(1, 3)}/{fullCode[4]}/data");
        if (icon != null && cd != null)
        {
            btn.SetOutfit(KiOutfit.Panel, rarity + 1);
            btn.SetCover(icon);
            if (costText != null) costText.text = cd.Cost + " $";
        }
        else
        {
            btn.SetCover(null);
            btn.SetOutfit(KiOutfit.Panel, 0);
            if (costText != null) costText.text = string.Empty;
        }
    }

    private static Sprite ResolveIconSprite(string fullCode)
    {
        if (string.IsNullOrEmpty(fullCode) || fullCode.Length < 5) return null;
        return Resources.Load<Sprite>($"Units/Cat Units/{fullCode[0]}/{fullCode.Substring(1, 3)}/{fullCode[4]}/icon_deploy");
    }

    private IEnumerator PlaySlotFeedbackRoutine(int position, Transform target)
    {
        if (target == null) yield break;
        float osize = normalScale;
        float maxsize = osize * Mathf.Max(1f, maxScaleMultiplier);
        float duration = Mathf.Max(0.05f, feedbackDuration);
        float t = 0f;
        float fdx = (maxsize - osize) / Mathf.Pow(duration / 2f, 2f);
        while (t < duration)
        {
            t += Time.deltaTime;
            target.localScale = Vector3.one * (maxsize - Mathf.Pow(t - duration / 2f, 2f) * fdx);
            yield return new WaitForFixedUpdate();
        }
        target.localScale = Vector3.one * osize;
        feedbackCoroutines[position] = null;
    }
}
