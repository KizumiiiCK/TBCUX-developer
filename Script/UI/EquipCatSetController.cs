using System;
using System.Collections;
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
    private int[] tireCosts = Array.Empty<int>();
    private int currentTire;
    private int maxUnlockedTire;
    private bool isSelected;
    private Action<int, string, int> onSelect;
    private Coroutine rainbowCoroutine;
    private float rainbowSpeed = 0.6f;

    private void Awake()
    {
        // AutoCacheRefs();
        BindEvents();
    }

    public void Configure(
        int rality,
        string code,
        bool[] unlocked,
        int[] tireCosts,
        Action<int, string, int> onSelect,
        int initialTire = -1,
        bool isSelected = false)
    {
        this.rality = rality;
        this.code = code ?? "000";
        this.unlocked = unlocked ?? Array.Empty<bool>();
        this.tireCosts = tireCosts ?? Array.Empty<int>();
        this.onSelect = onSelect;
        this.isSelected = isSelected;

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
            // 图标按需异步加载：先留空，到位后由回调填充
            RequestCurrentIcon();
            characterButton.SetText($"{GetCurrentCost()} $");
            UpdateSelectionFrameEffect();
        }

        if (switchButton != null) switchButton.gameObject.SetActive(maxUnlockedTire > 0);
    }

    private void UpdateSelectionFrameEffect()
    {
        if (characterButton == null) return;
        if (isSelected)
        {
            if (rainbowCoroutine == null)
            {
                rainbowCoroutine = StartCoroutine(RainbowFrameRoutine());
            }
            return;
        }

        StopRainbowFrameRoutine();
        characterButton.SetFrameColorPersistent(UXPref.GetRarityFrameColor(0));
    }

    private IEnumerator RainbowFrameRoutine()
    {
        float hue = 0f;
        while (true)
        {
            hue += Time.deltaTime * Mathf.Max(0.01f, rainbowSpeed);
            if (hue > 1f) hue -= 1f;
            characterButton.SetFrameColorPersistent(Color.HSVToRGB(hue, 1f, 1f));
            yield return null;
        }
    }

    private void StopRainbowFrameRoutine()
    {
        if (rainbowCoroutine == null) return;
        StopCoroutine(rainbowCoroutine);
        rainbowCoroutine = null;
    }

    private void OnDisable()
    {
        StopRainbowFrameRoutine();
        if (characterButton != null) characterButton.SetFrameColorPersistent(UXPref.GetRarityFrameColor(0));
    }

    /// <summary>
    /// 当前 tire 的图标地址。图标按需异步加载，切换 tire 时重新请求。
    /// </summary>
    private string GetCurrentIconAddress()
    {
        if (currentTire < 0) return null;
        return $"Units/Cat Units/{rality}/{code}/{currentTire}/icon_deploy";
    }

    /// <summary>
    /// 异步拉取当前 tire 图标。以本组件为 owner，格子被回收或切换 tire 时旧请求自动作废。
    /// </summary>
    private void RequestCurrentIcon()
    {
        if (characterButton == null) return;
        AsyncIconLoader.Instance.Load(this, GetCurrentIconAddress(),
            sprite => { if (characterButton != null) characterButton.SetCover(sprite); });
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
