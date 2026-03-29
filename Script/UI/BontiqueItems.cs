using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BontiqueItems : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private KiPanel kiPanel;
    [SerializeField] private TMP_Text remainingText;
    [SerializeField] private FrameCurrencyItem currencyDisplay;
    [SerializeField] private KiButton redeemButton;

    private BontiqueShopItem boundItem;

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

    public void Configure(BontiqueShopItem item)
    {
        boundItem = item;
        CacheRefs();
        // set cover based on reward kind and id
        Sprite cover = null;
        if (item.RewardKind == RewardType.item)
        {
            cover = StorageImageHelper.GetItemImageByOrder(item.gainId);
        }
        // else leave null
        kiPanel.SetCover(cover);
        kiPanel.SetText($"x{item.ObtainAmount}");
        // remaining - TODO: use real shop state storage; display LimitCount as fallback
        remainingText.text = item.Limit == LimitType.None ? "∞" : item.LimitCount.ToString();
        // currency display
        if (currencyDisplay != null) currencyDisplay.SetData(item.CurrencyId, RewardingSystem.GetAmount(item.CurrencyId));
        // button
        if (redeemButton != null)
        {
            redeemButton.onClick.RemoveAllListeners();
            redeemButton.onClick.AddListener(() => OnRedeemClicked());
        }
    }

    private void OnRedeemClicked()
    {
        if (boundItem == null) return;
        // simplistic: check currency and reduce (no persistence implemented)
        int have = RewardingSystem.GetAmount(boundItem.CurrencyId);
        if (have < boundItem.CurrencyAmount)
        {
            Debug.Log("Not enough currency");
            return;
        }
        RewardingSystem.ConsumeItem(boundItem.CurrencyId, -boundItem.CurrencyAmount);
        RewardingSystem.AddAmount(boundItem.gainId, boundItem.ObtainAmount);
        Debug.Log($"Redeemed item {boundItem.gainId} x{boundItem.ObtainAmount}");
        // update UI
        if (currencyDisplay != null) currencyDisplay.SetData(boundItem.CurrencyId, RewardingSystem.GetAmount(boundItem.CurrencyId));
    }
}
