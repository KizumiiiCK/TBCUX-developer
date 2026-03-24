//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;
//using UnityEngine.Localization;
//using UnityEngine.Localization.Settings;
//public class LocaleSelector : MonoBehaviour
//{
//    private bool active = false;
//    // 方法：根据 LocaleID 更改语言环境
//    public void ChangeLocale(int localeID)
//    {
//        Debug.Log($"ChangeLocale({localeID}) start:");
//        if (active) return;
//        StartCoroutine(SetLocale(localeID));
//    }

//    // 协程：异步设置语言环境
//    IEnumerator SetLocale(int localeID)
//    {
//        Debug.Log($"[SetLocale] 开始 locale={localeID}");
//        active = true;

//        var initOp = LocalizationSettings.InitializationOperation;
//        Debug.Log($"[SetLocale] 等待初始化... 状态={initOp.IsDone}");
//        yield return initOp;

//        Debug.Log($"[SetLocale] 初始化完成，开始设置 SelectedLocale");
//        var locales = LocalizationSettings.AvailableLocales.Locales;
//        if (localeID < 0 || localeID >= locales.Count)
//        {
//            Debug.LogError($"[SetLocale] localeID 越界 {localeID}/{locales.Count}");
//            active = false;
//            yield break;
//        }

//        LocalizationSettings.SelectedLocale = locales[localeID];
//        Debug.Log($"[SetLocale] 设置成功 {locales[localeID].Identifier.Code}");
//        active = false;
//    }
//}
#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEditor;

public static class EditorLanguageSwitcher
{
    [MenuItem("Tools/Switch Language/English")]
    static void SetEn() => SetLocale(0);

    [MenuItem("Tools/Switch Language/中文")]
    static void SetZh() => SetLocale(1);

    [MenuItem("Tools/Switch Language/日本語")]
    static void SetJa() => SetLocale(2);

    static void SetLocale(int idx)
    {
        if (LocalizationSettings.AvailableLocales.Locales.Count <= idx) return;
        LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[idx];
        Debug.Log($"[Editor] 已切换语言 → {LocalizationSettings.SelectedLocale.Identifier.Code}");
    }
}
#endif
