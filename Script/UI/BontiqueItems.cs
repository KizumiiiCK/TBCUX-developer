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
    private Action<BontiqueShopItem> onRedeemClickedSignal;
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

    public void Configure(
        BontiqueShopItem item,
        int remaining,
        bool interactable,
        DateTime now,
        Action<BontiqueShopItem> onRedeemClickedSignal,
        Action<BontiqueShopItem> onRedeemRequested)
    {
        boundItem = item;
        this.onRedeemClickedSignal = onRedeemClickedSignal;
        this.onRedeemRequested = onRedeemRequested;
        CacheRefs();

        bool isCharacterReward = item.RewardKind == RewardType.character;
        if (rewardImage != null)
        {
            rewardImage.rectTransform.sizeDelta = isCharacterReward ? CharacterImageSize : ItemImageSize;
            if (isCharacterReward)
            {
                // 角色图标走 Addressables，按需异步加载
                string cid = item.gainId.ToString("0000");
                rewardImage.enabled = false;
                AsyncIconLoader.Instance.Load(gameObject,
                    $"Units/Cat Units/{cid[0]}/{cid.Substring(1, 3)}/0/icon_deploy",
                    sprite =>
                    {
                        if (rewardImage == null) return;
                        rewardImage.sprite = sprite;
                        rewardImage.enabled = sprite != null;
                    });
            }
            else
            {
                // 道具图标在 Resources 中，随包内置，可同步取
                Sprite rewardSprite = item.gainId >= 0
                    ? StorageImageHelper.GetItemImageByOrder(item.gainId)
                    : null;
                rewardImage.sprite = rewardSprite;
                rewardImage.enabled = rewardSprite != null;
            }
        }
        if (obtainAmountText != null) obtainAmountText.text = $"x{item.ObtainAmount}";

        if (itemNameText != null)
        {
            string expectedNameId = item.bid;
            itemNameText.text = expectedNameId;
            LocalizationHelper.GetLocalizedText(
                UXPref.Localized_Bontiques,
                expectedNameId,
                localizedText =>
                {
                    if (boundItem == null) return;
                    string currentExpected = boundItem.bid;
                    if (!string.Equals(currentExpected, expectedNameId, StringComparison.Ordinal)) return;
                    itemNameText.text = string.IsNullOrEmpty(localizedText) ? expectedNameId : localizedText;
                });
        }

        RefreshPurchaseState(item, remaining, interactable, now);
    }

    public void RefreshPurchaseState(BontiqueShopItem item, int remaining, bool interactable, DateTime now)
    {
        if (item == null) return;
        boundItem = item;
        CacheRefs();
        int currentCurrencyAmount = RewardingSystem.GetAmount(item.CurrencyId);
        UpdateRemainingAndPurchaseUi(item, remaining, interactable, now, currentCurrencyAmount);
    }

    private void UpdateRemainingAndPurchaseUi(BontiqueShopItem item, int remaining, bool interactable, DateTime now, int currentCurrencyAmount)
    {
        if (remainingText != null)
        {
            string remainingLabel = "Remaining: " + (remaining < 0 ? "∞" : Mathf.Max(0, remaining).ToString());
            if (item.Limit == LimitType.Event && item.IsInActiveWindow(now))
            {
                int daysLeft = GetEventDaysLeft(item, now);
                remainingLabel += $"\n<color=#FF6060>{daysLeft}</color> DAYS  LEFT!";
            }
            remainingText.text = remainingLabel;
        }
        if (currencyDisplay != null)
        {
            string costAndOwned;
            int displayCurrencyId = item.CurrencyId;
            if (item.CurrencyId == 11 || item.CurrencyId == 12 || item.IsPlatformPay)
            {
                costAndOwned = item.IsPlatformPay
                    ? $"¥{Mathf.Max(0, item.CurrencyAmount)}"
                    : Mathf.Max(0, item.CurrencyAmount).ToString();
            }
            else
            {
                costAndOwned = $"{Mathf.Max(0, item.CurrencyAmount)} / {Mathf.Max(0, currentCurrencyAmount)}";
            }
            currencyDisplay.SetData(displayCurrencyId, currentCurrencyAmount, null, costAndOwned);
        }
        if (redeemButton != null)
        {
            bool currencyEnough = item.IsPlatformPay || currentCurrencyAmount >= item.CurrencyAmount;
            bool canRedeem = interactable && currencyEnough;
            Color targetColor = canRedeem ? CanBuyButtonColor : CannotBuyButtonColor;
            redeemButton.SetFrameColorPersistent(targetColor);
            redeemButton.SetCoverColor(targetColor);
            redeemButton.onClick.RemoveAllListeners();
            redeemButton.interactable = canRedeem;
            redeemButton.onClick.AddListener(OnRedeemClicked);
        }
    }

    private static int GetEventDaysLeft(BontiqueShopItem item, DateTime now)
    {
        if (item == null || !item.LimitStart.HasValue || !item.LimitEnd.HasValue) return 0;
        DateTime today = now.Date;
        int nowMd = today.Month * 100 + today.Day;
        int startMd = item.LimitStart.Value.Month * 100 + item.LimitStart.Value.Day;
        int endMd = item.LimitEnd.Value.Month * 100 + item.LimitEnd.Value.Day;

        int endYear = today.Year;
        if (startMd > endMd && nowMd >= startMd) endYear += 1;
        DateTime endDate = new DateTime(endYear, item.LimitEnd.Value.Month, item.LimitEnd.Value.Day);

        int days = (endDate - today).Days + 1;
        return Mathf.Max(1, days);
    }

    private void OnRedeemClicked()
    {
        if (boundItem == null) return;
        onRedeemClickedSignal?.Invoke(boundItem);
        onRedeemRequested?.Invoke(boundItem);
    }
}
