using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EquipCanvas : UICanvasMain
{
    private struct EquipListEntry
    {
        public string Code;
        public int Rality;
        public bool[] Unlocked;
        public Sprite[] TireIcons;
        public int[] TireCosts;
    }

    private GameObject mainCamera;
    public int rality = 0;
    public int team_num = 0;
    private string[] char_codes = new string[13];
    [SerializeField] private RalityOption ralityOption;
    [SerializeField] private Button GoToCatIndex_btn;
    [SerializeField] private EquipTeamSelectionPanel teamSelectionPanel;
    [SerializeField] private Image background;
    [SerializeField] private ShowEnemyBoard SEB;
    [SerializeField] private LevelRestrictionBoard levelRestrictionBoard;
    [Header("Instantiators")]
    [SerializeField] private RectTransform HeadIcon_ScrollingArea;
    [SerializeField] private ScrollRect headIconScrollRect;
    [SerializeField] private GameObject EquipHeadIcon;
    [SerializeField] private int headIconColumns = 1;
    [SerializeField] private float headIconCellWidth = 270f;
    [SerializeField] private float headIconCellHeight = 200f;
    [SerializeField] private int headIconPreloadRows = 2;
    //[SerializeField] private CustomScrollbar scrollbar_setting;
    [Header("Main Controllers")]
    private int current_modifying_slot = 0;
    private string currentTeamName;
    private GameObject current_display_character;
    private string[] currentRestrictions;
    private LevelRestrictionHelper.RestrictionRules activeRestrictionRules;
    private readonly List<EquipListEntry> equipListEntries = new List<EquipListEntry>();
    private readonly Dictionary<string, int> displayTireByCharacter = new Dictionary<string, int>();
    private VirtualizedScrollGrid<EquipListEntry> headIconGrid;
    private const string CatIndexCanvasPrefab = "UpgradeCanvas";

    // Start is called before the first frame update
    void Start()
    {
        mainCamera = GameObject.Find("Main Camera");
        GetComponent<Canvas>().worldCamera=mainCamera.GetComponent<Camera>();
        InitializeButtons();
        InitializeHeadIconGrid();
        UpdateBackground();
        InitializeRalityOption();
        team_num = PlayerPrefs.GetInt(SelectionsSave.pref_teamnum, 0);
        char_codes = SelectionsSave.GetRow(team_num);
        BuildDisplayTireCacheFromTeam();
        currentTeamName = TeamNameSave.GetTeamNameOrDefault(team_num);
        teamSelectionPanel?.SetTeamDisplay(team_num, currentTeamName);
        ShowSelectionList();
        MarkCharacters();
        ApplyRestrictionContext(currentRestrictions);
        ShowFirstSlotCharacter();
    }
    private void OnDisable()
    {
        if(current_display_character != null)
        {
            Destroy(current_display_character);
            current_display_character = null;
        }
    }
    // Update is called once per frame
    //void Update()
    //{

    //}
    public void LoadCharatersFromRality(int R)
    {
        rality = R;
        displayTireByCharacter.Clear();
        BuildDisplayTireCacheFromTeam();
        equipListEntries.Clear();
        for(int i=0;i<1000;i++)
        {
            string ucformat = i.ToString("000");
            Sprite ccd = BundledAddressables.LoadSync<Sprite>($"Units/Cat Units/{rality}/{ucformat}/0/icon_deploy");
            if (ccd == null) { continue; }
            bool[] unlocked = CharacterUpgradeSave.GetDetails($"{rality}{ucformat}").tire_unlocked;
            if (!unlocked[0]) { continue; }
            var entry = new EquipListEntry
            {
                Code = ucformat,
                Rality = rality,
                Unlocked = new bool[4],
                TireIcons = new Sprite[4],
                TireCosts = new int[4]
            };

            for (int j = 0; j < 4; j++)
            {
                entry.Unlocked[j] = unlocked[j];
                if (!unlocked[j]) continue;

                Sprite otccd = BundledAddressables.LoadSync<Sprite>($"Units/Cat Units/{rality}/{ucformat}/{j}/icon_deploy");
                CharacterData cdot = BundledAddressables.LoadSync<CharacterData>($"Units/Cat Units/{rality}/{ucformat}/{j}/data");
                entry.TireIcons[j] = otccd;
                entry.TireCosts[j] = cdot != null ? cdot.Cost : 0;
                if (otccd == null || cdot == null) entry.Unlocked[j] = false;
            }

            equipListEntries.Add(entry);
        }
        if (headIconGrid != null) headIconGrid.SetData(equipListEntries, true);
        MarkCharacters();
    }
    public void InitializeButtons()
    {
        if (teamSelectionPanel == null) teamSelectionPanel = GetComponentInChildren<EquipTeamSelectionPanel>(true);
        if (teamSelectionPanel != null)
        {
            teamSelectionPanel.Initialize(
                InTeamButtonOnClick,
                SwitchPositionOnClick,
                RemoveChosenCharacter,
                ChangeTeam,
                OnTeamNameChanged
            );
        }
        if (GoToCatIndex_btn != null) GoToCatIndex_btn.onClick.AddListener(OpenCatIndexFromEquip);
    }

    private void InitializeRalityOption()
    {
        ralityOption = GetComponentInChildren<RalityOption>(true);
        if (ralityOption != null)
        {
            ralityOption.Initialize(OnRalitySelected, 0);
        }
        else
        {
            LoadCharatersFromRality(0);
        }
    }

    private void OnRalitySelected(int selectedRarity)
    {
        LoadCharatersFromRality(selectedRarity);
    }
    private void OpenCatIndexFromEquip()
    {
        if (FrameUI == null) return;
        FrameUI.OpenPage(
            CatIndexCanvasPrefab,
            page =>
            {
                var catIndex = page != null ? page.GetComponent<CatIndexCanvas>() : null;
                if (catIndex != null) catIndex.SetReturnToEquipMode(true);
            },
            null,
            FrameUIDisplayer.DoorAction.None
        );
    }
    private void ShowSelectionList()
    {
        if (teamSelectionPanel != null) teamSelectionPanel.RefreshSlots(char_codes);
    }
    private void ShowFirstSlotCharacter()
    {
        if (char_codes == null || char_codes.Length == 0) return;
        if (!CharacterPlacer.TryParse(char_codes[0], true, out UnitIdentity identity) || !identity.IsValid) return;
        if (!identity.AssetIsCat || identity.CharacterCode.Length < 5) return;
        ShowCertainCharacter(identity.CharacterCode[0] - '0', identity.CharacterCode.Substring(1, 3), identity.CharacterCode[4] - '0');
    }
    private void MarkCharacters(bool selecting=true)
    {
        MarkSelectedCharacter(null, selecting);
    }
    public void AddCharacterOrSwitchTire(int rality, string code, int tire)
    {
        string cc = rality.ToString() + code;
        string fullcc = cc + tire.ToString();
        if (LevelRestrictionHelper.TryAssignMatchingForcedSlots(activeRestrictionRules, cc, char_codes))
        {
            for (int i = 0; i < LevelRestrictionHelper.ForcedSlotCount; i++)
            {
                if (!LevelRestrictionHelper.ForcedSlotMatchesUnit(activeRestrictionRules, i, cc)) continue;
                teamSelectionPanel?.PlaySlotChangeFeedback(i);
            }
            ShowSelectionList();
            SaveCurrentTeamState();
            MarkSelectedCharacter($"{rality}{code}{tire}");
            ShowCertainCharacter(rality, code, tire);
            return;
        }

        int upperbound = 10;
        int lowerbound = 0;
        if (rality == 6) { upperbound = 13; lowerbound = 10; }

        bool has_empty_slot = false;
        for (int i = lowerbound; i < upperbound; i++)
        {
            if (LevelRestrictionHelper.IsSlotForced(activeRestrictionRules, i)) continue;
            if (char_codes[i] == string.Empty || char_codes[i] == null) {has_empty_slot = true; continue; }
            if (char_codes[i].Substring(0, 4) == cc)
            {
                Debug.Log($"Matched at pos {i}");
                char_codes[i] = fullcc;
                ShowSelectionList();
                SaveCurrentTeamState();
                MarkSelectedCharacter($"{rality}{code}{tire}");
                ShowCertainCharacter(rality, code, tire);
                teamSelectionPanel?.PlaySlotChangeFeedback(i);
                return;
            }
        }
        if (has_empty_slot)
        {
            for(int i = lowerbound; i < upperbound; i++)
            {
                if (LevelRestrictionHelper.IsSlotForced(activeRestrictionRules, i)) continue;
                if(char_codes[i] == string.Empty || char_codes[i] == null) 
                { 
                    char_codes[i] = fullcc;
                    ShowSelectionList();
                    SaveCurrentTeamState();
                    MarkSelectedCharacter($"{rality}{code}{tire}");
                    ShowCertainCharacter(rality, code, tire);
                    teamSelectionPanel?.PlaySlotChangeFeedback(i);
                    break;
                }
            }
        }
        else
        {

        }
    }
    private void InTeamButtonOnClick(int position)
    {
        if (char_codes[position] == string.Empty || char_codes[position] == null) return;
        current_modifying_slot = position;
        teamSelectionPanel?.ShowSwapPanel(position);
    }
    private void SwitchPositionOnClick(int position)
    {
        teamSelectionPanel?.HideSwapPanel();
        if (current_modifying_slot == position) return;
        if (current_modifying_slot >= 10) return;

        string destination_code = char_codes[position];
        char_codes[position] = char_codes[current_modifying_slot];
        char_codes[current_modifying_slot] = destination_code;
        SaveCurrentTeamState();
        ShowSelectionList();
        teamSelectionPanel?.PlaySlotChangeFeedback(current_modifying_slot);
        teamSelectionPanel?.PlaySlotChangeFeedback(position);
    }
    private void RemoveChosenCharacter()
    {
        teamSelectionPanel?.HideSwapPanel();
        char_codes[current_modifying_slot] = string.Empty;
        SaveCurrentTeamState();
        ShowSelectionList();
        MarkCharacters();
    }
    public void MarkSelectedCharacter(string code, bool selecting=true)
    {
        if (headIconGrid != null)
        {
            headIconGrid.RebindVisible();
            return;
        }

        if (code == string.Empty || code == null) return;
        if (rality != (code[0] - '0')) return;
    }
    private void ChangeTeam(bool after)
    {
        SaveCurrentTeamState();
        int addon=after ? 1 : -1;
        team_num = (team_num + addon) % 10;
        if (team_num == -1) team_num = 9;
        PlayerPrefs.SetInt(SelectionsSave.pref_teamnum, team_num);
        char_codes = SelectionsSave.GetRow(team_num);
        BuildDisplayTireCacheFromTeam();
        currentTeamName = TeamNameSave.GetTeamNameOrDefault(team_num);
        teamSelectionPanel?.SetTeamDisplay(team_num, currentTeamName);
        ShowSelectionList();
        MarkCharacters();
        ShowFirstSlotCharacter();
    }

    private void OnTeamNameChanged(string inputName)
    {
        currentTeamName = TeamNameSave.NormalizeTeamName(team_num, inputName);
        SaveCurrentTeamState();
    }

    private void SaveCurrentTeamState()
    {
        SelectionsSave.SetRow(team_num, char_codes);
        TeamNameSave.SetTeamName(team_num, currentTeamName);
    }
    public void ShowCertainCharacter(int r, string code, int tire)
    {
        Application.targetFrameRate = 30;
        if (current_display_character != null) DestroyImmediate(current_display_character.gameObject);
        string loadPath = $"Units/Cat Units/{r}/{code}/{tire}/";
        CharacterData CD = BundledAddressables.LoadSync<CharacterData>(loadPath + "data");
        current_display_character = CharacterSummoner.CreateACharacter(true, $"{r}{code}{tire}", true);
        CharacterSummoner.SetCharacterPosition(current_display_character,
            mainCamera.transform.position + new Vector3(CD.UNITYAnimated ? -2 : 0, -4, 10));
        CharacterVisualLoader.ResetAnimationOrderLayer(current_display_character, "UI", 3);
        CharacterVisualLoader.SwitchAnimation(current_display_character, CD.UNITYAnimated, 2);

        current_display_character.transform.localScale *= 1.5f;
    }
    public void ShowChangeBGPage()
    {
        Transform t = Instantiate(Resources.Load<GameObject>("UI/ChangeBackgroundPage")).transform;
        t.SetParent(gameObject.transform);
        t.position = Vector3.zero;
        t.localScale = Vector3.one;
    }
    public void UpdateBackground()
    {
        int bgn=PlayerPrefs.GetInt(UXPref.Localized_BGnum, 0);
        background.sprite = BundledAddressables.LoadSync<Sprite>($"Background/Maps/{bgn}");
    }
    private void OnDestroy()
    {
        SaveCurrentTeamState();
        Destroy(current_display_character);
        if (headIconGrid != null) headIconGrid.Dispose();
    }
    public void ChanageSEBShowInfo(string[] enemyAppears, string[] restrictions = null, bool blindEnemyIcons = false)
    {
        currentRestrictions = restrictions;
        if (SEB != null) SEB.ShowEnemies(enemyAppears, null, blindEnemyIcons);
        ApplyRestrictionContext(restrictions);
    }

    private void ApplyRestrictionContext(string[] restrictions)
    {
        activeRestrictionRules = LevelRestrictionHelper.Parse(restrictions);
        teamSelectionPanel?.ApplyLevelRestrictions(activeRestrictionRules);

        if (levelRestrictionBoard == null) return;

        bool hasLines = restrictions != null && restrictions.Length > 0;
        levelRestrictionBoard.gameObject.SetActive(hasLines);
        if (hasLines) levelRestrictionBoard.ShowRestrictions(restrictions);
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
        if (current_display_character != null)
        {
            Destroy(current_display_character);
            current_display_character = null;
        }
        if (FrameUI != null)
        {
            FrameUI.CloseDoor();
            yield return new WaitForSecondsRealtime(FrameUIAnimations.DoorDuration);
        }
    }

    private void InitializeHeadIconGrid()
    {
        if (HeadIcon_ScrollingArea == null || EquipHeadIcon == null) return;
        if (headIconScrollRect == null) headIconScrollRect = HeadIcon_ScrollingArea.GetComponentInParent<ScrollRect>();
        if (headIconScrollRect == null) return;

        headIconGrid = new VirtualizedScrollGrid<EquipListEntry>(
            new VirtualizedScrollGrid<EquipListEntry>.Settings
            {
                Content = HeadIcon_ScrollingArea,
                ScrollRect = headIconScrollRect,
                ItemPrefab = EquipHeadIcon,
                Columns = Mathf.Max(1, headIconColumns),
                CellWidth = Mathf.Max(1f, headIconCellWidth),
                CellHeight = Mathf.Max(1f, headIconCellHeight),
                PreloadRows = Mathf.Max(0, headIconPreloadRows),
                DisableAutoLayout = true
            },
            BindEquipHeadIcon
        );
        headIconGrid.Initialize();
    }

    private void BindEquipHeadIcon(GameObject iconGO, int _, EquipListEntry entry)
    {
        if (iconGO == null) return;
        iconGO.name = entry.Code;
        var setController = iconGO.GetComponent<EquipCatSetController>();
        if (setController == null) setController = iconGO.AddComponent<EquipCatSetController>();
        string key = $"{entry.Rality}{entry.Code}";
        int preferredTire = displayTireByCharacter.TryGetValue(key, out int cachedTire) ? cachedTire : -1;
        setController.Configure(
            entry.Rality,
            entry.Code,
            entry.Unlocked,
            entry.TireIcons,
            entry.TireCosts,
            OnEquipSetRequestedSelection,
            preferredTire,
            IsCharacterSelected(entry.Rality, entry.Code)
        );
    }

    private void OnEquipSetRequestedSelection(int rality, string code, int tire)
    {
        displayTireByCharacter[$"{rality}{code}"] = tire;
        AddCharacterOrSwitchTire(rality, code, tire);
    }

    private void BuildDisplayTireCacheFromTeam()
    {
        displayTireByCharacter.Clear();
        if (char_codes == null) return;
        for (int i = 0; i < char_codes.Length; i++)
        {
            string cc = char_codes[i];
            if (!CharacterPlacer.TryParse(cc, true, out UnitIdentity identity) || !identity.IsValid) continue;
            if (identity.IsOpposite || !identity.AssetIsCat || identity.CharacterCode.Length < 5) continue;
            string key = identity.CharacterCode.Substring(0, 4);
            int tire = identity.CharacterCode[4] - '0';
            displayTireByCharacter[key] = tire;
        }
    }

    private bool IsCharacterSelected(int rarity, string code)
    {
        if (char_codes == null) return false;
        string key = $"{rarity}{code}";
        for (int i = 0; i < char_codes.Length; i++)
        {
            string cc = char_codes[i];
            if (!CharacterPlacer.TryParse(cc, true, out UnitIdentity identity) || !identity.IsValid) continue;
            if (identity.IsOpposite || !identity.AssetIsCat || identity.CharacterCode.Length < 4) continue;
            if (identity.CharacterCode.Substring(0, 4) == key) return true;
        }
        return false;
    }

}
