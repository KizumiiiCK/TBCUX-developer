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
        public Sprite Icon;
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
        BundledAddressables.EnsureInitialized();
        for (int i = 2; i < 1000; i++)
        {
            string ucformat = "e" + i.ToString("000");
            string iconAddress = $"Units/Enemy Units/{ucformat}/enemy_icon";
            if (!BundledAddressables.Exists(iconAddress, typeof(Sprite)))
                continue;

            bool unlocked = indexShowAll ? true : EnemyMeetSave.GetUnlocked(i);
            Sprite ccd = BundledAddressables.LoadSync<Sprite>(iconAddress);
            if (ccd == null) continue;
            if (!unlocked) ccd = UnknownImage;
            enemyListEntries.Add(new EnemyListEntry
            {
                Code = ucformat,
                Icon = ccd,
                Unlocked = unlocked
            });
        }

        if (headIconGrid != null) headIconGrid.SetData(enemyListEntries, true);
        ShowCertainCharacter("e002");
    }
    public void ShowCertainCharacter(string char_code, bool resetAnimation=true)
    {
        current_code = char_code;
        if (resetAnimation)
        {
            Application.targetFrameRate = 30;
            if (current_display_character != null) DestroyImmediate(current_display_character.gameObject);
            CharacterData CD = CharacterVisualLoader.LoadCharacterData(false, current_code);
            if (CD == null)
            {
                Debug.LogWarning($"[EnemyIndexCanvas] Missing enemy data for {current_code}");
                return;
            }
            current_display_character = CharacterSummoner.CreateACharacter(false,current_code, true);
            CharacterSummoner.SetCharacterPosition(current_display_character,
                mainCamera.transform.position + new Vector3(CD.UNITYAnimated ? 2 : 0, -4, 10));
            CharacterVisualLoader.ResetAnimationOrderLayer(current_display_character, "UI", 3);
            UnityAnimated = CD.UNITYAnimated;
            current_animation_num = 0;
            playableAnimCount = CharacterVisualLoader.GetPlayableAnimCount(
                current_display_character, UnityAnimated, false, current_code, CD);
            CharacterVisualLoader.SwitchAnimation(current_display_character,UnityAnimated,current_animation_num);
            if (UnityAnimated) current_display_character.transform.localScale *= 1.25f;
            IV.ShowCharacterDetails(CD,false,1);
            LocalizationHelper.GetLocalizedText(UXPref.Localized_UnitNames, current_code, localizedText => name_txt.text = localizedText ?? current_code);
        }
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
        int bgn = PlayerPrefs.GetInt(UXPref.Localized_BGnum, 0);
        background.sprite = BundledAddressables.LoadSync<Sprite>($"Background/Maps/{bgn}");
        ShowCertainCharacter(current_code);
    }
    private void OnDestroy()
    {
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
        if (image != null) image.sprite = data.Icon;

        var button = iconGO.GetComponent<Button>();
        if (button != null)
        {
            button.interactable = data.Unlocked;
            button.onClick.RemoveAllListeners();
            if (data.Unlocked) button.onClick.AddListener(() => ShowCertainCharacter(data.Code));
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
