using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Chatbox : MonoBehaviour
{
    [SerializeField] GameObject ChatWindow;
    [SerializeField] GameObject NameBox;
    [SerializeField] TMP_Text name_text;
    [SerializeField] TMP_Text dialogue_text;
    [SerializeField] Image character_left;
    [SerializeField] Image character_right;
    [SerializeField] Button skip_once_btn;
    [SerializeField] Button skip_ALL_btn;

    private bool finish_display=false;
    private bool next_display=false;
    [SerializeField]private AudioSource se;
    protected string contentID;
    protected Dialogue[] dialogues;
    //
    private string[] cachedDialogueContents;
    //
    private void Start()
    {
        skip_once_btn.onClick.AddListener(ForceFinish);
        skip_ALL_btn.onClick.AddListener(SkipAll);
    }
    public IEnumerator ShowAllDialogue()
    {
        int di = 0;
        while (di < dialogues.Length)
        {
            finish_display = false;
            next_display = false;
            ShowDialogue(dialogues[di], di);
            while(!next_display) yield return null;
            di++;
        }
        GameObject.Find("Level Initializer").GetComponent<LevelController>().ExitPlot();
        Destroy(gameObject);
    }
    public void ShowDialogue(Dialogue dd, int i)
    {
        SetDialogueName(dd);
        SetDialogueImage(dd);

        string dialogueContent = cachedDialogueContents[i];
        StartCoroutine(TextFlow(dialogueContent));
    }
    private IEnumerator TextFlow(string full_content)
    {
        Debug.Log(full_content);
        finish_display = false;
        next_display = false;
        string display_content = string.Empty;
        se.Play();
        for (int i = 0; i < full_content.Length; i++)
        {
            display_content += full_content[i];
            dialogue_text.text = display_content;
            se.Play();
            if (finish_display) break;
            else yield return new WaitForFixedUpdate();
        }
        dialogue_text.text = full_content;
        finish_display = true;
    }
    public void ShowChatbox(bool show)
    {
        ChatWindow.SetActive(show);
    }
    public void SetFullDialogue(GamePlot gp)
    {
        dialogues = gp.dialogues;
        contentID = gp.contentID;

        cachedDialogueContents = new string[dialogues.Length];
        StartCoroutine(PreloadAllDialogueTexts());
    }
    private IEnumerator PreloadAllDialogueTexts()
    {
        int loadedCount = 0;

        for (int i = 0; i < dialogues.Length; i++)
        {
            int index = i;
            string cid = $"{contentID}:{index}";

            LocalizationHelper.GetLocalizedText(
                UXPref.Localized_Dialogue,
                cid,
                localizedText =>
                {
                    cachedDialogueContents[index] = localizedText ?? cid;
                    loadedCount++;
                });
        }

        // Wait until all lines are loaded
        while (loadedCount < dialogues.Length)
            yield return null;
    }
    public bool GetFinishState() => finish_display;
    public void ForceFinish() { if (!finish_display) finish_display = true; else next_display = true; }
    private void SetDialogueName(Dialogue dd)
    {
        if (dd.DialoguerName != string.Empty)
        {
            NameBox.SetActive(true);
            string ns = $"char:{dd.DialoguerName}";
            LocalizationHelper.GetLocalizedText(UXPref.Localized_DialogueNames, ns,
                localizedText => name_text.text = localizedText ?? ns);
        }
        else
        {
            NameBox.SetActive(false);
        }
    }
    private void SetDialogueImage(Dialogue dd)
    {
        Color colorShow = new Color(1, 1, 1, 1);
        Color colorHide = new Color(1, 1, 1, 0);
        if (dd.clearImage)
        {
            character_left.color = colorHide;
            character_right.color = colorHide;
        }
        if (dd.DialoguerImage != string.Empty)
        {
            Sprite DI= Resources.Load<Sprite>($"DialogueImage/{dd.DialoguerImage}");
            if (dd.faceToRight)//Image on the left
            {
                character_left.color = colorShow;
                character_left.sprite = DI;
            }
            else
            {
                character_right.color = colorShow;
                character_right.sprite = DI;
            }
        }
        
    }
    public void SkipAll() { 
        StopAllCoroutines();
        GameObject.Find("Level Initializer").GetComponent<LevelController>().ExitPlot();
        Destroy(gameObject);
    }
}
