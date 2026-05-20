using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Chatbox : MonoBehaviour
{
    private const float CgFadeDuration = 0.5f;
    //private const string CgShaderName = "UI/DialogueCgFill";

    [SerializeField] GameObject ChatWindow;
    [SerializeField] GameObject NameBox;
    [SerializeField] TMP_Text name_text;
    [SerializeField] TMP_Text dialogue_text;
    [SerializeField] Image character_left;
    [SerializeField] Image character_right;
    //[SerializeField] GameObject cg_cover;
    [SerializeField] Image chat_cg;
    [SerializeField] Button skip_once_btn;
    [SerializeField] Button skip_ALL_btn;

    private bool finish_display=false;
    private bool next_display=false;
    [SerializeField]private AudioSource se;
    protected string contentID;
    protected Dialogue[] dialogues;
    //
    private string[] cachedDialogueContents;
    private bool isDialoguePreloadCompleted = false;
    private string currentCgName = string.Empty;
    private bool waitingForCgContinue = false;
    private bool cgContinueRequested = false;
    private bool isCgTransitioning = false;
    //private Material runtimeCgMaterial;
    //
    private void Start()
    {
        InitializeCgState();
        skip_once_btn.onClick.AddListener(ForceFinish);
        skip_ALL_btn.onClick.AddListener(SkipAll);
    }
    //private void OnDestroy()
    //{
    //    if (runtimeCgMaterial != null)
    //    {
    //        Destroy(runtimeCgMaterial);
    //        runtimeCgMaterial = null;
    //    }
    //}
    public IEnumerator ShowAllDialogue()
    {
        while (!isDialoguePreloadCompleted)
            yield return null;

        int di = 0;
        while (di < dialogues.Length)
        {
            yield return StartCoroutine(ShowDialogueRoutine(dialogues[di], di));
            di++;
        }
        GameObject.Find("Level Initializer").GetComponent<LevelController>().ExitPlot();
        Destroy(gameObject);
    }
    private IEnumerator ShowDialogueRoutine(Dialogue dd, int i)
    {
        finish_display = false;
        next_display = false;

        yield return StartCoroutine(HandleCgTransition(dd));

        SetDialogueVisualsActive(true);
        SetDialogueName(dd);
        SetDialogueImage(dd);

        string dialogueContent = cachedDialogueContents[i];
        if (string.IsNullOrEmpty(dialogueContent))
        {
            dialogueContent = $"{contentID}:{i}";
        }

        yield return StartCoroutine(TextFlow(dialogueContent));
        while (!next_display) yield return null;
    }
    public void ShowDialogue(Dialogue dd, int i)
    {
        StartCoroutine(ShowDialogueRoutine(dd, i));
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
        isDialoguePreloadCompleted = false;
        currentCgName = string.Empty;
        waitingForCgContinue = false;
        cgContinueRequested = false;
        isCgTransitioning = false;
        InitializeCgState();

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

        isDialoguePreloadCompleted = true;
    }
    public bool GetFinishState() => finish_display;
    public void ForceFinish()
    {
        if (isCgTransitioning) return;
        if (waitingForCgContinue)
        {
            cgContinueRequested = true;
            return;
        }

        if (!finish_display) finish_display = true;
        else next_display = true;
    }
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
    private void InitializeCgState()
    {
        if (chat_cg == null) return;
        //EnsureCgMaterial();
        chat_cg.gameObject.SetActive(false);
        SetCgAlpha(0f);
        chat_cg.sprite = null;
    }
    private void SetDialogueVisualsActive(bool show)
    {
        if (character_left != null) character_left.gameObject.SetActive(show);
        if (character_right != null) character_right.gameObject.SetActive(show);
        if (!show && NameBox != null) NameBox.SetActive(false);
        ShowChatbox(show);
    }
    private IEnumerator HandleCgTransition(Dialogue dd)
    {
        if (chat_cg == null)
        {
            yield break;
        }

        //EnsureCgMaterial();

        string nextCgName = dd != null && !string.IsNullOrWhiteSpace(dd.cg) ? dd.cg.Trim() : string.Empty;
        if (string.IsNullOrEmpty(nextCgName))
        {
            currentCgName = string.Empty;
            if (chat_cg.gameObject.activeSelf)
            {
                yield return StartCoroutine(FadeOutAndDisableCg());
            }
            yield break;
        }

        Sprite nextCgSprite = Resources.Load<Sprite>($"CG/{nextCgName}");
        if (nextCgSprite == null)
        {
            Debug.LogWarning($"[Chatbox] CG not found at Resources/CG/{nextCgName}");
            yield break;
        }

        if (chat_cg.gameObject.activeSelf && currentCgName == nextCgName)
        {
            yield break;
        }

        SetDialogueVisualsActive(false);
        if (chat_cg.gameObject.activeSelf)
        {
            yield return StartCoroutine(SwapCg(nextCgSprite));
        }
        else
        {
            yield return StartCoroutine(FadeInCg(nextCgSprite));
        }

        currentCgName = nextCgName;
        yield return StartCoroutine(WaitForCgContinue());
    }
    private IEnumerator FadeInCg(Sprite nextCgSprite)
    {
        isCgTransitioning = true;
        chat_cg.sprite = nextCgSprite;
        chat_cg.gameObject.SetActive(true);
        yield return StartCoroutine(FadeCgAlpha(0f, 1f));
        isCgTransitioning = false;
    }
    private IEnumerator SwapCg(Sprite nextCgSprite)
    {
        isCgTransitioning = true;
        yield return StartCoroutine(FadeCgAlpha(chat_cg.color.a, 0f));
        chat_cg.sprite = nextCgSprite;
        yield return StartCoroutine(FadeCgAlpha(0f, 1f));
        isCgTransitioning = false;
    }
    private IEnumerator FadeOutAndDisableCg()
    {
        isCgTransitioning = true;
        yield return StartCoroutine(FadeCgAlpha(chat_cg.color.a, 0f));
        chat_cg.sprite = null;
        chat_cg.gameObject.SetActive(false);
        isCgTransitioning = false;
    }
    private IEnumerator FadeCgAlpha(float from, float to)
    {
        float elapsed = 0f;
        SetCgAlpha(from);
        while (elapsed < CgFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetCgAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / CgFadeDuration)));
            yield return null;
        }
        SetCgAlpha(to);
    }
    private void SetCgAlpha(float alpha)
    {
        if (chat_cg == null) return;
        Color color = chat_cg.color;
        color.a = alpha;
        chat_cg.color = color;
    }
    //private void EnsureCgMaterial()
    //{
    //    if (chat_cg == null) return;

    //    if (runtimeCgMaterial == null)
    //    {
    //        Shader cgShader = Shader.Find(CgShaderName);
    //        if (cgShader == null)
    //        {
    //            Debug.LogWarning($"[Chatbox] Shader not found: {CgShaderName}");
    //            return;
    //        }

    //        runtimeCgMaterial = new Material(cgShader)
    //        {
    //            name = "DialogueCgFill (Runtime)"
    //        };
    //    }

    //    if (chat_cg.material != runtimeCgMaterial)
    //    {
    //        chat_cg.material = runtimeCgMaterial;
    //    }

    //    Rect rect = chat_cg.rectTransform.rect;
    //    float height = Mathf.Max(1f, rect.height);
    //    float width = Mathf.Max(1f, rect.width);
    //    runtimeCgMaterial.SetFloat("_ContainerAspect", width / height);
    //}
    private IEnumerator WaitForCgContinue()
    {
        waitingForCgContinue = true;
        cgContinueRequested = false;
        while (!cgContinueRequested)
        {
            yield return null;
        }
        waitingForCgContinue = false;
        cgContinueRequested = false;
    }
    public void SkipAll() { 
        StopAllCoroutines();
        GameObject.Find("Level Initializer").GetComponent<LevelController>().ExitPlot();
        Destroy(gameObject);
    }
}
