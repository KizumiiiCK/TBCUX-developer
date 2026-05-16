using UnityEngine;

public class FrameCurrencyItem : MonoBehaviour
{
    private const int FIXED_DIGIT_LENGTH = 15;

    [SerializeField] private KiPanel currencyPanel;
    [SerializeField] private KiPanel amountPanel;
    [SerializeField] private bool reverse_display = false;

    private void Awake()
    {
        CachePanels();
    }

    private void OnValidate()
    {
        if (Application.isPlaying) return;
        CachePanels();
    }
    public void SetData(int rewardId, int amount, Color? amountColor = null, string amountOverrideText = null)
    {
        CachePanels();
        if (currencyPanel != null)
        {
            currencyPanel.SetCover(StorageImageHelper.GetItemImageByOrder(rewardId));
        }
        if (amountPanel != null)
        {
            string AL = string.IsNullOrEmpty(amountOverrideText) ? amount.ToString() : amountOverrideText;
            amountPanel.SetText(AL, c: amountColor);
            amountPanel.SetSize(30*AL.Length, 10);
            amountPanel.GetComponent<RectTransform>().anchoredPosition = 
            new Vector2(90 + FIXED_DIGIT_LENGTH * (AL.Length - 1), 0) * (reverse_display ? -1 : 1);
        }
    }

    private void CachePanels()
    {
        if (currencyPanel != null && amountPanel != null) return;

        var panels = GetComponentsInChildren<KiPanel>(true);
        if (panels == null || panels.Length == 0) return;

        if (currencyPanel == null) currencyPanel = panels[0];
        if (amountPanel == null && panels.Length > 1) amountPanel = panels[1];
    }
}
