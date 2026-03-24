using UnityEngine;
using UnityEngine.UI;

public class StrategySelector : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private KiButton standardButton;
    [SerializeField] private KiButton defenseButton;
    [SerializeField] private KiButton attackButton;

    private const string StrategyKey = "Strategy";
    private const float SelectedScaleMultiplier = 1.15f;

    private static readonly Color StandardColor = Color.yellow;
    private static readonly Color DefenseColor = new Color(0.35f, 0.65f, 1f, 1f);
    private static readonly Color AttackColor = new Color(1f, 0.35f, 0.35f, 1f);
    private static readonly Color UnselectedColor = new Color(1f, 1f, 1f, 0.5f);

    private Vector3 standardBaseScale = Vector3.one;
    private Vector3 defenseBaseScale = Vector3.one;
    private Vector3 attackBaseScale = Vector3.one;

    private void Awake()
    {
        CacheRefs();
        CacheBaseScales();
        BindEvents();
        SetStrategy(0);
    }

    private void CacheRefs()
    {
        if (standardButton == null || defenseButton == null || attackButton == null)
        {
            KiButton[] all = GetComponentsInChildren<KiButton>(true);
            if (all != null && all.Length >= 3)
            {
                if (standardButton == null) standardButton = all[0];
                if (defenseButton == null) defenseButton = all[1];
                if (attackButton == null) attackButton = all[2];
            }
        }
    }

    private void CacheBaseScales()
    {
        if (standardButton != null) standardBaseScale = standardButton.transform.localScale;
        if (defenseButton != null) defenseBaseScale = defenseButton.transform.localScale;
        if (attackButton != null) attackBaseScale = attackButton.transform.localScale;
    }

    private void BindEvents()
    {
        if (standardButton != null)
        {
            standardButton.onClick.RemoveListener(OnStandardClicked);
            standardButton.onClick.AddListener(OnStandardClicked);
        }
        if (defenseButton != null)
        {
            defenseButton.onClick.RemoveListener(OnDefenseClicked);
            defenseButton.onClick.AddListener(OnDefenseClicked);
        }
        if (attackButton != null)
        {
            attackButton.onClick.RemoveListener(OnAttackClicked);
            attackButton.onClick.AddListener(OnAttackClicked);
        }
    }

    private void OnStandardClicked() => SetStrategy(0);
    private void OnDefenseClicked() => SetStrategy(1);
    private void OnAttackClicked() => SetStrategy(2);

    private void SetStrategy(int strategy)
    {
        int clamped = Mathf.Clamp(strategy, 0, 2);
        PlayerPrefs.SetInt(StrategyKey, clamped);
        ApplyVisuals(clamped);
    }

    private void ApplyVisuals(int selectedStrategy)
    {
        ApplyButtonVisual(standardButton, standardBaseScale, selectedStrategy == 0, StandardColor);
        ApplyButtonVisual(defenseButton, defenseBaseScale, selectedStrategy == 1, DefenseColor);
        ApplyButtonVisual(attackButton, attackBaseScale, selectedStrategy == 2, AttackColor);
    }

    private void ApplyButtonVisual(KiButton button, Vector3 baseScale, bool selected, Color selectedColor)
    {
        if (button == null) return;
        Color c = selected ? selectedColor : UnselectedColor;
        button.SetFrameColorPersistent(c);
        button.SetCoverColor(c);
        button.transform.localScale = baseScale * (selected ? SelectedScaleMultiplier : 1f);
    }
}
