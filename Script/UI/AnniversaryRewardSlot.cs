using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One claimable entry of the anniversary board (a line reward or a lit-count milestone).
/// Purely a view: the canvas owns all state and calls Configure on every refresh.
/// <para>
/// Put this on a BingoReward root (line reward) or a BingoStackItem root (milestone) and wire
/// only the parts that prefab actually has — every field is optional and skipped when unset.
/// </para>
/// </summary>
public class AnniversaryRewardSlot : MonoBehaviour
{
    [Header("Claim")]
    [Tooltip("Button that takes the reward. Interactable only while unlocked and unclaimed.")]
    [SerializeField] private Button claimButton;
    [Tooltip("Image tinted to show the state: locked / ready / claimed. Usually the rhombus cover.")]
    [SerializeField] private Image colorTarget;

    [Header("Reward Content")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text amountText;

    [Header("Optional Labels")]
    [Tooltip("Leave empty on BingoStackItem: its desc text is driven by LocalizeStringEvent.")]
    [SerializeField] private TMP_Text titleText;
    [Tooltip("Progress readout, e.g. the milestone's \"32 / 40\".")]
    [SerializeField] private TMP_Text progressText;
    [Tooltip("Shown once the reward has been taken. The BingoStackItem tick.")]
    [SerializeField] private GameObject claimedMark;

    private System.Action onClaim;
    private bool listenerBound;
    private Vector2 iconBaseSize;
    private bool iconBaseSizeCached;

    private const float CharacterPortraitAspect = 85f / 110f;

    /// <param name="unlocked">Condition met; the reward may or may not be taken yet.</param>
    /// <param name="claimed">Reward already taken.</param>
    /// <param name="claimEnabled">
    /// False while the page owns input (drawing, or picking a cell with a select ticket). Blocks the
    /// button without touching the colour, so a ready slot still reads as ready.
    /// </param>
    public void Configure(RewardType kind, int gainId, int amount,
                          bool unlocked, bool claimed, bool claimEnabled,
                          string title, string progress,
                          System.Action claimCallback)
    {
        onClaim = claimCallback;

        if (titleText != null) titleText.text = title ?? string.Empty;
        if (progressText != null) progressText.text = progress ?? string.Empty;
        if (amountText != null) amountText.text = amount > 0 ? $"x{amount}" : string.Empty;
        if (claimedMark != null) claimedMark.SetActive(claimed);
        if (icon != null)
        {
            bool isCharacterPortrait = kind != RewardType.item;
            icon.sprite = isCharacterPortrait
                ? BundledAddressables.LoadSync<Sprite>(RewardIconHelper.GetCatDeployIconPath(gainId.ToString("0000"), 0))
                : StorageImageHelper.GetItemImageByOrder(gainId);
            icon.enabled = icon.sprite != null;
            ApplyIconFrame(isCharacterPortrait);
        }
        if (colorTarget != null)
        {
            colorTarget.color = claimed
                ? AnniversaryBoardCanvas.SlotClaimedColor
                : unlocked ? AnniversaryBoardCanvas.SlotReadyColor : AnniversaryBoardCanvas.SlotLockedColor;
        }

        if (claimButton == null) return;
        if (!listenerBound)
        {
            claimButton.onClick.AddListener(HandleClaim);
            listenerBound = true;
        }
        claimButton.interactable = unlocked && !claimed && claimEnabled;
    }

    private void ApplyIconFrame(bool isCharacterPortrait)
    {
        RectTransform rt = icon.rectTransform;
        if (!iconBaseSizeCached)
        {
            iconBaseSize = rt.sizeDelta;
            iconBaseSizeCached = true;
        }
        Vector2 size = iconBaseSize;
        if (isCharacterPortrait) size.y = size.x * CharacterPortraitAspect;
        rt.sizeDelta = size;
    }

    private void HandleClaim() => onClaim?.Invoke();
}
