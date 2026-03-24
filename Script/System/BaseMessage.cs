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
        LoadRandomCharacter();
        LoadRandomMessage();
        ChangeDoors();
        SwitchMessage_Btn.onClick.AddListener(LoadRandomMessage);
    }
    private void LoadRandomCharacter()
    {
        Sprite[] chars_img = Resources.LoadAll<Sprite>("DialogueImage");
        int p = Random.Range(0, chars_img.Length);
        PlayerPrefs.SetInt("base_character", p);
        character_img.sprite = chars_img[p];
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
    private void ChangeDoors()
    {
        string cpt_name = PlayerPrefs.GetString(UXPref.ChapterName);
        if (cpt_name != null)
        {
            Sprite[] ds = Resources.LoadAll<Sprite>($"Background/Doors/door_{cpt_name}");
            if (ds != null) if(ds.Length>1)
            {
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
            else Debug.Log("No Image");
        }
    }
}
