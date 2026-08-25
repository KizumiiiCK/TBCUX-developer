using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine.UI;

public class BaseMessage : MonoBehaviour
{
    [SerializeField] private TMP_Text message;
    [SerializeField] private RectTransform Box;
    [SerializeField] private Image character_img;
    [SerializeField] private Button SwitchMessage_Btn;
    [SerializeField] private FrameUIAnimations frameUIAnimations;
    // Backward-compatible fallback for old prefabs
    [SerializeField] private RectTransform Doors;

    private bool onChanging = false;
    private int ml = 20;
    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(StartRoutine());
        SwitchMessage_Btn.onClick.AddListener(LoadRandomMessage);
    }

    /// <summary>
    /// 立绘目录与章节门图需要先异步拉取，之后再读取显示。
    /// </summary>
    private IEnumerator StartRoutine()
    {
        yield return DialoguePortraitCatalog.EnsureLoadedRoutine();
        LoadRandomCharacter();
        LoadRandomMessage();
        yield return ChangeDoorsRoutine();
    }
    private void LoadRandomCharacter()
    {
        IReadOnlyList<Sprite> portraits = DialoguePortraitCatalog.GetVisiblePortraits();
        if (portraits.Count == 0)
        {
            Debug.LogWarning("BaseMessage: no visible portraits found in DialogueImage.");
            character_img.sprite = null;
            PlayerPrefs.SetInt("base_character", 0);
            return;
        }

        int p = Random.Range(0, portraits.Count);
        PlayerPrefs.SetInt("base_character", p);
        character_img.sprite = portraits[p];
        //character_img.sprite = Resources.Load<Sprite>("DialogueImage/XPM_smile");
    }
    private void LoadRandomMessage()
    {
        if (onChanging) return;
        onChanging = true;
        StartCoroutine(ChangeMessage());
    }
    private IEnumerator ChangeMessage()
    {
        message.text = string.Empty;
        float sy = Box.localScale.y;
        Box.localScale = new Vector3(Box.localScale.x, sy * 0.8f, Box.localScale.z);
        yield return new WaitForFixedUpdate();
        Box.localScale = new Vector3(Box.localScale.x, sy, Box.localScale.z);
        yield return new WaitForFixedUpdate();
        LocalizationHelper.GetLocalizedText(UXPref.Localized_BM, Random.Range(0, ml).ToString(),
            localizedText => message.text = localizedText ?? "???");
        yield return new WaitForFixedUpdate();
        onChanging = false;
    }
    private IEnumerator ChangeDoorsRoutine()
    {
        string cpt_name = PlayerPrefs.GetString(UXPref.ChapterName);
        if (string.IsNullOrEmpty(cpt_name)) yield break;

        string address = $"Background/Doors/door_{cpt_name}";
        var list = new BundledAddressables.PrewarmList();
        list.AddSpriteSheet(address);
        yield return BundledAddressables.PrewarmRoutine(list);

        Sprite[] ds = BundledAddressables.LoadSpriteSheetSync(address);
        if (ds == null || ds.Length <= 1)
        {
            Debug.Log("No Image");
            yield break;
        }

        if (frameUIAnimations != null)
        {
            frameUIAnimations.SetDoorSprites(ds[0], ds[1]);
        }
        else if (Doors != null && Doors.childCount > 1)
        {
            Doors.GetChild(0).GetComponent<Image>().sprite = ds[0];
            Doors.GetChild(1).GetComponent<Image>().sprite = ds[1];
        }
    }
}
