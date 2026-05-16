using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;

public class BontiqueItems : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Image rewardImage;
    [SerializeField] private TMP_Text obtainAmountText;
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text remainingText;
    [SerializeField] private FrameCurrencyItem currencyDisplay;
    [SerializeField] private KiButton redeemButton;

    private BontiqueShopItem boundItem;
    private Action<BontiqueShopItem> onRedeemRequested;
    private static readonly Vector2 CharacterImageSize = new Vector2(110f, 85f);
    private static readonly Vector2 ItemImageSize = new Vector2(128f, 128f);
    private static readonly Color CanBuyButtonColor = new Color(0.78f, 1f, 0.78f, 1f);
    private static readonly Color CannotBuyButtonColor = new Color(1f, 0.78f, 0.78f, 1f);

    private void Awake()
    {
        CacheRefs();
    }

    private void CacheRefs()
    {
        if (rewardImage == null) rewardImage = GetComponentInChildren<Image>(true);
        if (obtainAmountText == null || itemNameText == null || remainingText == null)
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text == null) continue;
                string loweredName = text.name.ToLowerInvariant();
                if (obtainAmountText == null && (loweredName.Contains("amount") || loweredName.Contains("count")))
                    obtainAmountText = text;
                else if (itemNameText == null && loweredName.Contains("name"))
                    itemNameText = text;
                else if (remainingText == null && (loweredName.Contains("remain") || loweredName.Contains("left")))
                    remainingText = text;
            }
        }
        if (currencyDisplay == null) currencyDisplay = GetComponentInChildren<FrameCurrencyItem>(true);
        if (redeemButton == null) redeemButton = GetComponentInChildren<KiButton>(true);
    }

    public void Configure(BontiqueShopItem item, int remaining, bool interactable, Action<BontiqueShopItem> onRedeemRequested)
    {
        boundItem = item;
        this.onRedeemRequested = onRedeemRequested;
        CacheRefs();

        bool isCharacterReward = item.RewardKind == RewardType.character;
        Sprite rewardSprite = ResolveRewardSprite(item, isCharacterReward);
        if (rewardImage != null)
        {
            rewardImage.sprite = rewardSprite;
            rewardImage.enabled = rewardSprite != null;
            rewardImage.rectTransform.sizeDelta = isCharacterReward ? CharacterImageSize : ItemImageSize;
        }
        if (obtainAmountText != null) obtainAmountText.text = $"x{item.ObtainAmount}";

        if (itemNameText != null)
        {
            string expectedNameId = string.IsNullOrEmpty(item.b_name_id) ? item.bid : item.b_name_id;
            itemNameText.text = expectedNameId;
            LocalizationHelper.GetLocalizedText(
                UXPref.Localized_UI,
                expectedNameId,
                localizedText =>
                {
                    if (boundItem == null) return;
                    string currentExpected = string.IsNullOrEmpty(boundItem.b_name_id) ? boundItem.bid : boundItem.b_name_id;
                    if (!string.Equals(currentExpected, expectedNameId, StringComparison.Ordinal)) return;
                    itemNameText.text = string.IsNullOrEmpty(localizedText) ? expectedNameId : localizedText;
                });
        }

        if (remainingText != null) remainingText.text = "Remaining: " + (remaining < 0 ? "∞" : Mathf.Max(0, remaining).ToString());
        int currentCurrencyAmount = RewardingSystem.GetAmount(item.CurrencyId);
        if (currencyDisplay != null)
        {
            string costAndOwned = $"{Mathf.Max(0, item.CurrencyAmount)} / {Mathf.Max(0, currentCurrencyAmount)}";
            currencyDisplay.SetData(item.CurrencyId, currentCurrencyAmount, null, costAndOwned);
        }
        if (redeemButton != null)
        {
            bool currencyEnough = currentCurrencyAmount >= item.CurrencyAmount;
            Color targetColor = currencyEnough ? CanBuyButtonColor : CannotBuyButtonColor;
            redeemButton.SetFrameColorPersistent(targetColor);
            redeemButton.SetCoverColor(targetColor);
            redeemButton.onClick.RemoveAllListeners();
            redeemButton.interactable = interactable;
            redeemButton.onClick.AddListener(OnRedeemClicked);
        }
    }

    private static Sprite ResolveRewardSprite(BontiqueShopItem item, bool isCharacterReward)
    {
        if (item == null) return null;
        if (isCharacterReward)
        {
            string cid = item.gainId.ToString("0000");
            return Resources.Load<Sprite>($"Units/Cat Units/{cid[0]}/{cid.Substring(1, 3)}/0/icon_deploy");
        }
        if (item.gainId >= 0 && item.gainId < RewardingSystem.RewardNumMap.Count)
        {
            return StorageImageHelper.GetItemImageByOrder(item.gainId);
        }
        return null;
    }

    private void OnRedeemClicked()
    {
        if (boundItem == null) return;
        onRedeemRequested?.Invoke(boundItem);
    }
}
