using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;

public class BontiqueItems : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private KiPanel kiPanel;
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text remainingText;
    [SerializeField] private FrameCurrencyItem currencyDisplay;
    [SerializeField] private KiButton redeemButton;

    private BontiqueShopItem boundItem;
    private Action<BontiqueShopItem> onRedeemRequested;

    private void Awake()
    {
        CacheRefs();
    }

    private void CacheRefs()
    {
        if (kiPanel == null) kiPanel = GetComponentInChildren<KiPanel>(true);
        if (remainingText == null) remainingText = GetComponentInChildren<TMP_Text>(true);
        if (currencyDisplay == null) currencyDisplay = GetComponentInChildren<FrameCurrencyItem>(true);
        if (redeemButton == null) redeemButton = GetComponentInChildren<KiButton>(true);
    }

    public void Configure(BontiqueShopItem item, int remaining, bool interactable, Action<BontiqueShopItem> onRedeemRequested)
    {
        boundItem = item;
        this.onRedeemRequested = onRedeemRequested;
        CacheRefs();

        Sprite cover = null;
        if (item.RewardKind == RewardType.item)
        {
            cover = StorageImageHelper.GetItemImageByOrder(item.gainId);
        }
        kiPanel.SetCover(cover);
        kiPanel.SetText($"x{item.ObtainAmount}");

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

        if (remainingText != null)
            remainingText.text = remaining < 0 ? "∞" : Mathf.Max(0, remaining).ToString();

        if (currencyDisplay != null) currencyDisplay.SetData(item.CurrencyId, RewardingSystem.GetAmount(item.CurrencyId));

        if (redeemButton != null)
        {
            redeemButton.onClick.RemoveAllListeners();
            redeemButton.interactable = interactable;
            redeemButton.onClick.AddListener(OnRedeemClicked);
        }
    }

    private void OnRedeemClicked()
    {
        if (boundItem == null) return;
        onRedeemRequested?.Invoke(boundItem);
    }
}
