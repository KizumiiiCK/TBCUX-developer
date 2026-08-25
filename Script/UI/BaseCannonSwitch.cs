using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BaseCannonSwitch : MonoBehaviour
{
    [SerializeField] private Button mainButton;
    [SerializeField] private GameObject showPanelRoot;
    [SerializeField] private Transform optionsRoot;
    [SerializeField] private Image currentHeadImage;

    private readonly List<Button> optionButtons = new List<Button>();

    private void Awake()
    {
        CacheRefs();
        BindEvents();
        SetOptionsVisible(false);
        RefreshCurrentHeadImage(PlayerPrefs.GetInt(UXPref.BASE_CannonNum, 0));
    }

    private void CacheRefs()
    {
        if (mainButton == null) mainButton = GetComponentInChildren<Button>(true);
        if (optionsRoot == null && transform.childCount > 0) optionsRoot = transform.GetChild(0);

        optionButtons.Clear();
        if (optionsRoot == null) return;
        // Collect all Button components under optionsRoot (including inactive).
        var buttons = optionsRoot.GetComponentsInChildren<Button>(true);
        foreach (var btn in buttons)
        {
            // Avoid accidentally treating the main button as an option if it's inside the same hierarchy
            if (btn == mainButton) continue;
            optionButtons.Add(btn);
        }
    }

    private void BindEvents()
    {
        if (mainButton != null)
        {
            mainButton.onClick.RemoveListener(ToggleOptions);
            mainButton.onClick.AddListener(ToggleOptions);
        }

        for (int i = 0; i < optionButtons.Count; i++)
        {
            int idx = i;
            var btn = optionButtons[i];
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnOptionClicked(idx));
        }
    }

    private void ToggleOptions()
    {
        if (optionsRoot == null) return;
        SetOptionsVisible(!showPanelRoot.activeSelf);
    }

    private void OnOptionClicked(int index)
    {
        bool changed;

        var baseGo = GameObject.Find("CatBase");
        if (baseGo != null)
        {
            var catBase = baseGo.GetComponent<CatBase>();
            changed = catBase != null && catBase.TrySetCannonHead(index);
        }
        else
        {
            PlayerPrefs.SetInt(UXPref.BASE_CannonNum, index);
            PlayerPrefs.Save();
            changed = true;
        }

        if (changed) RefreshCurrentHeadImage(index);

        SetOptionsVisible(false);
    }

    private void SetOptionsVisible(bool visible)
    {
        if (showPanelRoot != null) showPanelRoot.SetActive(visible);
    }

    private void RefreshCurrentHeadImage(int headIndex)
    {
        if (currentHeadImage == null) return;
        // 炮头图标按需异步加载
        AsyncIconLoader.Instance.Load(currentHeadImage.gameObject,
            $"Units/CatBases/head/{Mathf.Max(0, headIndex)}",
            sprite => { if (currentHeadImage != null) currentHeadImage.sprite = sprite; });
    }
}
