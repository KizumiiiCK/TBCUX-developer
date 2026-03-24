using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BaseCannonSwitch : MonoBehaviour
{
    [SerializeField] private Button mainButton;
    [SerializeField] private Transform optionsRoot;

    private readonly List<Button> optionButtons = new List<Button>();
    private const string BaseHeadKey = "base_head";

    private void Awake()
    {
        CacheRefs();
        BindEvents();
        SetOptionsVisible(false);
    }

    private void CacheRefs()
    {
        if (mainButton == null) mainButton = GetComponentInChildren<Button>(true);
        if (optionsRoot == null && transform.childCount > 0) optionsRoot = transform.GetChild(0);

        optionButtons.Clear();
        if (optionsRoot == null) return;
        for (int i = 0; i < optionsRoot.childCount; i++)
        {
            var btn = optionsRoot.GetChild(i).GetComponent<Button>();
            if (btn != null) optionButtons.Add(btn);
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
        SetOptionsVisible(!optionsRoot.gameObject.activeSelf);
    }

    private void OnOptionClicked(int index)
    {
        PlayerPrefs.SetInt(BaseHeadKey, index);
        PlayerPrefs.Save();

        var baseGo = GameObject.Find("CatBase");
        if (baseGo != null)
        {
            var catBase = baseGo.GetComponent<CatBase>();
            if (catBase != null) catBase.SetCannonHead(index);
        }

        SetOptionsVisible(false);
    }

    private void SetOptionsVisible(bool visible)
    {
        if (optionsRoot != null) optionsRoot.gameObject.SetActive(visible);
    }
}
