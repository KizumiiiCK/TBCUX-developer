using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.SmartFormat.Core.Parsing;
using UnityEngine.UI;

public class EnemyIndexCanvas : UICanvasMain
{
    private struct EnemyListEntry
    {
        public string Code;
        public string IconAddress;
        public bool Unlocked;
    }

    private GameObject mainCamera;
    public string current_code = "e002";
    private GameObject current_display_character;
    [SerializeField] private Button Animation_switch_btn;
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text name_txt;
    [SerializeField] private Sprite UnknownImage;
    [Header("Instantiators")]
    [SerializeField] private RectTransform HeadIcon_ScrollingArea;
    [SerializeField] private ScrollRect headIconScrollRect;
    [SerializeField] private GameObject EnemyHeadIcon;
    private int headIconColumns = 4;
    private float headIconCellWidth = 128f;
    private float headIconCellHeight = 128f;
    private int headIconPreloadRows = 10;
    //[SerializeField] private CustomScrollbar scrollbar_setting;
    [SerializeField] private GameObject indexUnit;
    [SerializeField] private IndexViewer IV;
    private bool UnityAnimated = false;
    private int current_animation_num = 0;
    private int playableAnimCount = 4;
    [Header("Main Controllers")]
    private BaseCanvas baseCanvas;
    //

    private static bool indexShowAll = false;
    private readonly List<EnemyListEntry> enemyListEntries = new List<EnemyListEntry>();
    private VirtualizedScrollGrid<EnemyListEntry> headIconGrid;
    private Coroutine showCharacterRoutine;

    // Start is called before the first frame update
    void Start()
    {
        mainCamera = GameObject.Find("Main Camera");
        GetComponent<Canvas>().worldCamera=mainCamera.GetComponent<Camera>();
        baseCanvas=GameObject.Find("BaseCanvas").GetComponent<BaseCanvas>();
        InitializeButtons();
        InitializeHeadIconGrid();
        LoadEnemies();
        UpdateBackground();
    }
    public void LoadEnemies()
    {
        enemyListEntries.Clear();
        for (int i = 2; i < 1000; i++)
        {
            string ucformat = "e" + i.ToString("000");
            string iconAddress = $"Units/Enemy Units/{ucformat}/enemy_icon";
            // Catalog lookup only - no download, so the list structure is available instantly.
            if (!BundledAddressables.Exists(iconAddress, typeof(Sprite)))
                continue;

            bool unlocked = indexShowAll ? true : EnemyMeetSave.GetUnlocked(i);
            enemyListEntries.Add(new EnemyListEntry
            {
                Code = ucformat,
                IconAddress = iconAddress,
                Unlocked = unlocked
            });
        }

        if (headIconGrid != null) headIconGrid.SetData(enemyListEntries, true);
        ShowCertainCharacter("e002");
    }

    /// <summary>
    /// 显示某个敌人。资源改为异步按需拉取，对外入口只启动协程。
    /// </summary>
    public void ShowCertainCharacter(string char_code, bool resetAnimation = true)
    {
        if (showCharacterRoutine != null) StopCoroutine(showCharacterRoutine);
        showCharacterRoutine = StartCoroutine(ShowCertainCharacterRoutine(char_code, resetAnimation));
    }

    private IEnumerator ShowCertainCharacterRoutine(string char_code, bool resetAnimation)
    {
        current_code = char_code;
        if (!resetAnimation)
        {
            showCharacterRoutine = null;
            yield break;
        }

        // 展示一个敌人需要 data + 动画资源全套，按需拉取
        var list = new BundledAddressables.PrewarmList();
        BattlePrewarm.AddUnit(list, false, current_code);
        list.Add<GameObject>("Units/Enemy Units/enemyunit");
        yield return BundledAddressables.PrewarmRoutine(list);

        Application.targetFrameRate = 30;
        if (current_display_character != null) DestroyImmediate(current_display_character.gameObject);
        CharacterData CD = CharacterVisualLoader.LoadCharacterData(false, current_code);
        if (CD == null)
        {
            Debug.LogWarning($"[EnemyIndexCanvas] Missing enemy data for {current_code}");
            showCharacterRoutine = null;
            yield break;
        }
        current_display_character = CharacterSummoner.CreateACharacter(false, current_code, true);
        if (current_display_character != null)
        {
            CharacterSummoner.SetCharacterPosition(current_display_character,
                mainCamera.transform.position + new Vector3(CD.UNITYAnimated ? 2 : 0, -4, 10));
            CharacterVisualLoader.ResetAnimationOrderLayer(current_display_character, "UI", 3);
            UnityAnimated = CD.UNITYAnimated;
            current_animation_num = 0;
            playableAnimCount = CharacterVisualLoader.GetPlayableAnimCount(
                current_display_character, UnityAnimated, false, current_code, CD);
            CharacterVisualLoader.SwitchAnimation(current_display_character, UnityAnimated, current_animation_num);
            if (UnityAnimated) current_display_character.transform.localScale *= 1.25f;
        }
        IV.ShowCharacterDetails(CD, false, 1);
        LocalizationHelper.GetLocalizedText(UXPref.Localized_UnitNames, current_code, localizedText => name_txt.text = localizedText ?? current_code);
        showCharacterRoutine = null;
    }
    public void InitializeButtons()
    {
        Animation_switch_btn.onClick.AddListener(SwitchAnimation);
    }
    private void SwitchAnimation()
    {
        if (current_display_character == null) return;
        int count = playableAnimCount > 0 ? playableAnimCount : 4;
        current_animation_num = (current_animation_num + 1) % count;
        CharacterVisualLoader.SwitchAnimation(current_display_character, UnityAnimated, current_animation_num);
    }
    private void BackToBase()
    {
        //baseCanvas.EnemyBacktoBase();
        baseCanvas.SubBacktoBase();
    }
    public void ShowChangeBGPage()
    {
        Transform t = Instantiate(Resources.Load<GameObject>("UI/ChangeBackgroundPage")).transform;
        t.SetParent(gameObject.transform, false);
        t.localScale = Vector3.one;
        t.position = Vector3.zero;
        if (current_display_character != null) Destroy(current_display_character);
    }
    public void UpdateBackground()
    {
        StartCoroutine(UpdateBackgroundRoutine());
    }

    private IEnumerator UpdateBackgroundRoutine()
    {
        int bgn = PlayerPrefs.GetInt(UXPref.Localized_BGnum, 0);
        string address = $"Background/Maps/{bgn}";
        var list = new BundledAddressables.PrewarmList();
        list.Add<Sprite>(address);
        yield return BundledAddressables.PrewarmRoutine(list);
        if (background != null) background.sprite = BundledAddressables.LoadSync<Sprite>(address);
        ShowCertainCharacter(current_code);
    }
    private void OnDestroy()
    {
        if (showCharacterRoutine != null) StopCoroutine(showCharacterRoutine);
        Destroy(current_display_character);
        if (headIconGrid != null) headIconGrid.Dispose();
    }

    private void InitializeHeadIconGrid()
    {
        if (HeadIcon_ScrollingArea == null || EnemyHeadIcon == null) return;
        if (headIconScrollRect == null) headIconScrollRect = HeadIcon_ScrollingArea.GetComponentInParent<ScrollRect>();
        if (headIconScrollRect == null) return;

        headIconGrid = new VirtualizedScrollGrid<EnemyListEntry>(
            new VirtualizedScrollGrid<EnemyListEntry>.Settings
            {
                Content = HeadIcon_ScrollingArea,
                ScrollRect = headIconScrollRect,
                ItemPrefab = EnemyHeadIcon,
                Columns = Mathf.Max(1, headIconColumns),
                CellWidth = Mathf.Max(1f, headIconCellWidth),
                CellHeight = Mathf.Max(1f, headIconCellHeight),
                PreloadRows = Mathf.Max(0, headIconPreloadRows),
                DisableAutoLayout = true
            },
            BindEnemyHeadIcon
        );
        headIconGrid.Initialize();
    }

    private void BindEnemyHeadIcon(GameObject iconGO, int _, EnemyListEntry data)
    {
        if (iconGO == null) return;

        var image = iconGO.GetComponent<Image>();
        if (image != null)
        {
            if (!data.Unlocked)
            {
                // 未解锁的敌人显示为未知图标，不需要下载真实图标
                AsyncIconLoader.Instance.Cancel(iconGO);
                image.sprite = UnknownImage;
            }
            else
            {
                // 缩略图按需异步加载：格子先留空，图标到位后填充
                AsyncIconLoader.Instance.Load(iconGO, data.IconAddress,
                    sprite => { if (image != null) image.sprite = sprite; });
            }
        }

        var button = iconGO.GetComponent<Button>();
        if (button != null)
        {
            button.interactable = data.Unlocked;
            button.onClick.RemoveAllListeners();
            if (data.Unlocked)
            {
                string code = data.Code;
                button.onClick.AddListener(() => ShowCertainCharacter(code));
            }
        }
    }
    public override IEnumerator OnEnter()
    {
        if (FrameUI != null)
        {
            FrameUI.OpenDoor();
            yield return new WaitForSecondsRealtime(FrameUIAnimations.DoorDuration);
        }
    }

    public override IEnumerator OnExit()
    {
        if (FrameUI != null)
        {
            FrameUI.CloseDoor();
            yield return new WaitForSecondsRealtime(FrameUIAnimations.DoorDuration);
        }
    }
}
