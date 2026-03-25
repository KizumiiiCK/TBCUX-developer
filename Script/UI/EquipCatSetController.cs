using System;
using UnityEngine;
using UnityEngine.UI;

public class EquipCatSetController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private KiButton characterButton;
    [SerializeField] private Button switchButton;

    private int rality;
    private string code = "000";
    private bool[] unlocked = Array.Empty<bool>();
    private Sprite[] tireIcons = Array.Empty<Sprite>();
    private int[] tireCosts = Array.Empty<int>();
    private int currentTire;
    private int maxUnlockedTire;
    private Action<int, string, int> onSelect;

    private void Awake()
    {
        // AutoCacheRefs();
        BindEvents();
    }

    public void Configure(
        int rality,
        string code,
        bool[] unlocked,
        Sprite[] tireIcons,
        int[] tireCosts,
        Action<int, string, int> onSelect,
        int initialTire = -1)
    {
        this.rality = rality;
        this.code = code ?? "000";
        this.unlocked = unlocked ?? Array.Empty<bool>();
        this.tireIcons = tireIcons ?? Array.Empty<Sprite>();
        this.tireCosts = tireCosts ?? Array.Empty<int>();
        this.onSelect = onSelect;

        maxUnlockedTire = FindMaxUnlockedTire();
        if (initialTire >= 0)
        {
            currentTire = Mathf.Clamp(initialTire, 0, maxUnlockedTire);
        }
        else
        {
            currentTire = Mathf.Clamp(maxUnlockedTire, 0, 3); // default: highest unlocked tier
        }

        RefreshVisuals();
    }

    private void OnCharacterButtonClicked()
    {
        onSelect?.Invoke(rality, code, currentTire);
    }

    private void OnSwitchButtonClicked()
    {
        if (maxUnlockedTire <= 0) return; // only tier 1 unlocked => no switch
        int max = maxUnlockedTire + 1;
        currentTire = (currentTire + 1) % max;
        RefreshVisuals();
        onSelect?.Invoke(rality, code, currentTire);
    }

    private void RefreshVisuals()
    {
        if (characterButton != null)
        {
            characterButton.SetOutfit(KiOutfit.Border, rality + 1);
            Sprite icon = GetCurrentIcon();
            characterButton.SetCover(icon);
            characterButton.SetText($"{GetCurrentCost()} $");
        }

        if (switchButton != null) switchButton.gameObject.SetActive(maxUnlockedTire > 0);
    }

    private Sprite GetCurrentIcon()
    {
        if (currentTire >= 0 && currentTire < tireIcons.Length) return tireIcons[currentTire];
        return null;
    }

    private int GetCurrentCost()
    {
        if (currentTire >= 0 && currentTire < tireCosts.Length) return tireCosts[currentTire];
        return 0;
    }

    private int FindMaxUnlockedTire()
    {
        int max = 0;
        for (int i = 0; i < unlocked.Length; i++)
        {
            if (unlocked[i]) max = i;
        }
        return max;
    }

    //private void AutoCacheRefs()
    //{
    //    if (switchButton == null)
    //    {
    //        Button[] allButtons = GetComponentsInChildren<Button>(true);
    //        for (int i = 0; i < allButtons.Length; i++)
    //        {
    //            if (allButtons[i] == null) continue;
    //            switchButton = allButtons[i];
    //            break;
    //        }
    //    }

    //    if (characterButton == null)
    //    {
    //        KiButton[] allKiButtons = GetComponentsInChildren<KiButton>(true);
    //        if (allKiButtons != null)
    //        {
    //            for (int i = 0; i < allKiButtons.Length; i++)
    //            {
    //                var kb = allKiButtons[i];
    //                if (kb == null) continue;
    //                if (switchButton != null && kb == switchButton) continue;
    //                if (kb.transform.Find("Cover") != null)
    //                {
    //                    characterButton = kb;
    //                    break;
    //                }
    //            }
    //            if (characterButton == null)
    //            {
    //                for (int i = 0; i < allKiButtons.Length; i++)
    //                {
    //                    var kb = allKiButtons[i];
    //                    if (kb == null) continue;
    //                    if (switchButton != null && kb == switchButton) continue;
    //                    characterButton = kb;
    //                    break;
    //                }
    //            }
    //        }
    //    }

    //    if (switchButton == null)
    //    {
    //        Button[] allButtons = GetComponentsInChildren<Button>(true);
    //        for (int i = 0; i < allButtons.Length; i++)
    //        {
    //            if (allButtons[i] == null) continue;
    //            if (characterButton != null && allButtons[i] == characterButton) continue;
    //            switchButton = allButtons[i];
    //            break;
    //        }
    //    }
    //}

    private void BindEvents()
    {
        if (characterButton != null)
        {
            characterButton.onClick.RemoveListener(OnCharacterButtonClicked);
            characterButton.onClick.AddListener(OnCharacterButtonClicked);
        }
        if (switchButton != null)
        {
            switchButton.onClick.RemoveListener(OnSwitchButtonClicked);
            switchButton.onClick.AddListener(OnSwitchButtonClicked);
        }
    }
}
