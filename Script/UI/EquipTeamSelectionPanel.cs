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
    [SerializeField] private float normalScale = 0.9f;
    [SerializeField] private float maxScaleMultiplier = 1.5f;
    [SerializeField] private float feedbackDuration = 0.3f;

    private Action<int> onSlotClicked;
    private Action<int> onSwapTargetClicked;
    private Action onRemoveClicked;
    private Action<bool> onChangeTeamClicked;
    private Action<string> onTeamNameChanged;
    private Action<int, string> onTeamStateChanged;
    private readonly Coroutine[] feedbackCoroutines = new Coroutine[13];
    private readonly string[] cachedCharCodes = new string[13];
    private bool suppressTeamNameNotify;
    private int currentModifyingSlot = -1;
    private int currentTeamIndex = 0;
    private string currentTeamName = string.Empty;

    public void Initialize(
        Action<int> onSlotClicked,
        Action<int> onSwapTargetClicked,
        Action onRemoveClicked,
        Action<bool> onChangeTeamClicked,
        Action<string> onTeamNameChanged,
        Action<int, string> onTeamStateChanged = null)
    {
        this.onSlotClicked = onSlotClicked;
        this.onSwapTargetClicked = onSwapTargetClicked;
        this.onRemoveClicked = onRemoveClicked;
        this.onChangeTeamClicked = onChangeTeamClicked;
        this.onTeamNameChanged = onTeamNameChanged;
        this.onTeamStateChanged = onTeamStateChanged;

        BindEvents();
        HideSwapPanel();
        LoadCurrentTeamFromPrefs();
    }

    public void SetTeamDisplay(int teamNumber, string teamName)
    {
        currentTeamIndex = Mathf.Clamp(teamNumber, 0, SelectionsSave.TeamNum - 1);
        currentTeamName = TeamNameSave.NormalizeTeamName(currentTeamIndex, teamName);
        if (teamNameInput == null) return;
        suppressTeamNameNotify = true;
        string fallback = $"Team {currentTeamIndex + 1}";
        teamNameInput.text = string.IsNullOrWhiteSpace(currentTeamName) ? fallback : currentTeamName;
        suppressTeamNameNotify = false;
    }

    public void RefreshSlots(string[] charCodes)
    {
        if (charCodes == null) return;
        string[] source = charCodes;
        if (ReferenceEquals(source, cachedCharCodes))
        {
            source = (string[])charCodes.Clone();
        }

        int max = Mathf.Min(currentSelectedButtons.Length, source.Length);
        Array.Clear(cachedCharCodes, 0, cachedCharCodes.Length);
        Array.Copy(source, cachedCharCodes, max);
        for (int i = 0; i < cachedCharCodes.Length; i++)
        {
            if (cachedCharCodes[i] == null) cachedCharCodes[i] = string.Empty;
        }
        for (int i = 0; i < max; i++)
        {
            ApplySlotVisual(i, cachedCharCodes[i]);
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
            currentSelectedButtons[idx].onClick.AddListener(() => HandleSlotClicked(idx));
        }

        for (int i = 0; i < changePositionButtons.Length; i++)
        {
            int idx = i;
            if (changePositionButtons[idx] == null) continue;
            changePositionButtons[idx].onClick.RemoveAllListeners();
            changePositionButtons[idx].onClick.AddListener(() => HandleSwapTargetClicked(idx));
        }

        if (removeCharacterButton != null)
        {
            removeCharacterButton.onClick.RemoveAllListeners();
            removeCharacterButton.onClick.AddListener(HandleRemoveClicked);
        }

        if (changeTeamPrevButton != null)
        {
            changeTeamPrevButton.onClick.RemoveAllListeners();
            changeTeamPrevButton.onClick.AddListener(() => HandleChangeTeamClicked(false));
        }
        if (changeTeamNextButton != null)
        {
            changeTeamNextButton.onClick.RemoveAllListeners();
            changeTeamNextButton.onClick.AddListener(() => HandleChangeTeamClicked(true));
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
        if (onTeamNameChanged != null)
        {
            onTeamNameChanged.Invoke(value);
            return;
        }

        currentTeamName = TeamNameSave.NormalizeTeamName(currentTeamIndex, value);
        TeamNameSave.SetTeamName(currentTeamIndex, currentTeamName);
        NotifyTeamStateChanged();
    }

    private void ApplySlotVisual(int slotIndex, string fullCode)
    {
        if (slotIndex < 0 || slotIndex >= currentSelectedButtons.Length) return;
        var btn = currentSelectedButtons[slotIndex];
        if (btn == null) return;

        if (string.IsNullOrEmpty(fullCode))
        {
            btn.SetCover(null);
            btn.SetOutfit(KiOutfit.TransparentCenter, 0);
            btn.SetFrameColorPersistent(UXPref.GetRarityFrameColor(0));
            btn.SetText(string.Empty);
            return;
        }

        int rality = fullCode[0] - '0';
        Sprite icon = ResolveIconSprite(fullCode);
        CharacterData cd = Resources.Load<CharacterData>($"Units/Cat Units/{fullCode[0]}/{fullCode.Substring(1, 3)}/{fullCode[4]}/data");
        if (icon != null && cd != null)
        {
            btn.SetOutfit(KiOutfit.Border, rality + 1);
            btn.SetFrameColorPersistent(UXPref.GetRarityFrameColor(rality));
            btn.SetCover(icon);
            btn.SetText(cd.Cost + " $");
        }
        else
        {
            btn.SetCover(null);
            btn.SetOutfit(KiOutfit.TransparentCenter, 0);
            btn.SetFrameColorPersistent(UXPref.GetRarityFrameColor(0));
            btn.SetText(string.Empty);
        }
    }

    private static Sprite ResolveIconSprite(string fullCode)
    {
        if (string.IsNullOrEmpty(fullCode) || fullCode.Length < 5) return null;
        return Resources.Load<Sprite>($"Units/Cat Units/{fullCode[0]}/{fullCode.Substring(1, 3)}/{fullCode[4]}/icon_deploy");
    }

    private void HandleSlotClicked(int index)
    {
        if (onSlotClicked != null)
        {
            onSlotClicked.Invoke(index);
            return;
        }

        if (index < 0 || index >= cachedCharCodes.Length) return;
        if (string.IsNullOrEmpty(cachedCharCodes[index])) return;
        currentModifyingSlot = index;
        ShowSwapPanel(index);
    }

    private void HandleSwapTargetClicked(int index)
    {
        if (onSwapTargetClicked != null)
        {
            onSwapTargetClicked.Invoke(index);
            return;
        }

        HideSwapPanel();
        if (currentModifyingSlot == index) return;
        if (currentModifyingSlot < 0 || currentModifyingSlot >= cachedCharCodes.Length) return;
        if (index < 0 || index >= cachedCharCodes.Length) return;
        if (currentModifyingSlot >= 10) return;

        string destination = cachedCharCodes[index];
        cachedCharCodes[index] = cachedCharCodes[currentModifyingSlot];
        cachedCharCodes[currentModifyingSlot] = destination;
        SaveCurrentTeamState();
        RefreshSlots(cachedCharCodes);
        PlaySlotChangeFeedback(currentModifyingSlot);
        PlaySlotChangeFeedback(index);
    }

    private void HandleRemoveClicked()
    {
        if (onRemoveClicked != null)
        {
            onRemoveClicked.Invoke();
            return;
        }

        HideSwapPanel();
        if (currentModifyingSlot < 0 || currentModifyingSlot >= cachedCharCodes.Length) return;
        cachedCharCodes[currentModifyingSlot] = string.Empty;
        SaveCurrentTeamState();
        RefreshSlots(cachedCharCodes);
    }

    private void HandleChangeTeamClicked(bool after)
    {
        if (onChangeTeamClicked != null)
        {
            onChangeTeamClicked.Invoke(after);
            return;
        }

        SaveCurrentTeamState();
        int addon = after ? 1 : -1;
        currentTeamIndex = (currentTeamIndex + addon) % SelectionsSave.TeamNum;
        if (currentTeamIndex < 0) currentTeamIndex = SelectionsSave.TeamNum - 1;
        PlayerPrefs.SetInt(SelectionsSave.pref_teamnum, currentTeamIndex);
        LoadCurrentTeamFromPrefs();
    }

    private void LoadCurrentTeamFromPrefs()
    {
        currentTeamIndex = Mathf.Clamp(PlayerPrefs.GetInt(SelectionsSave.pref_teamnum, 0), 0, SelectionsSave.TeamNum - 1);
        string[] codes = SelectionsSave.GetRow(currentTeamIndex);
        currentTeamName = TeamNameSave.GetTeamNameOrDefault(currentTeamIndex);
        SetTeamDisplay(currentTeamIndex, currentTeamName);
        RefreshSlots(codes);
        NotifyTeamStateChanged();
    }

    private void SaveCurrentTeamState()
    {
        SelectionsSave.SetRow(currentTeamIndex, cachedCharCodes);
        TeamNameSave.SetTeamName(currentTeamIndex, currentTeamName);
        NotifyTeamStateChanged();
    }

    private void NotifyTeamStateChanged()
    {
        onTeamStateChanged?.Invoke(currentTeamIndex, currentTeamName);
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
