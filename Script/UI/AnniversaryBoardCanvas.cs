using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 1st Anniversary "light up 8x8" board page.
/// Board state lives in <see cref="FirstAnniversarySave"/>; this class only draws, consumes and refreshes.
/// </summary>
public class AnniversaryBoardCanvas : UICanvasMain
{
    public static readonly Color CellLitColor = new Color(0.30f, 0.95f, 0.40f);      // newly / already lit
    public static readonly Color CellDuplicateColor = new Color(0.25f, 0.65f, 1.00f); // hit again by the latest draw
    public static readonly Color CellIdleColor = Color.white;
    public static readonly Color CellSelectableColor = new Color(1.00f, 0.90f, 0.45f); // pickable while in select mode
    public static readonly Color CellScrambleColor = new Color(1.00f, 0.85f, 0.20f);  // swept over while the draw spins

    public static readonly Color DrawTextScrambleColor = new Color(0.55f, 0.55f, 0.55f);

    public static readonly Color SlotReadyColor = new Color(1.000f, 0.569f, 0.984f, 1.000f);
    public static readonly Color SlotLockedColor = new Color(0.204f, 0.243f, 0.337f, 1.000f);
    public static readonly Color SlotClaimedColor = new Color(0.000f, 1.000f, 0.298f, 1.000f);

    /// <summary>Select tickets needed to light one chosen cell.</summary>
    public const int SelectTicketCost = 10;

    [System.Serializable]
    public class RewardEntry
    {
        public RewardType kind = RewardType.item;
        public int gainId;   // item -> RewardNumMap order, character -> unit code
        public int amount = 1;
    }

    [System.Serializable]
    public class MilestoneEntry
    {
        public int litRequirement = 10;
        public RewardEntry reward = new RewardEntry();
    }

    [Header("Board")]
    [Tooltip("Parent whose children are the 64 BingoBtn panels in order 1..64.")]
    [SerializeField] private Transform boardRoot;

    [Header("Actions")]
    [SerializeField] private Button drawButton;
    [SerializeField] private Button selectButton;
    [Tooltip("Optional. Swapped to the cancel wording while picking a cell.")]
    [SerializeField] private TMP_Text selectButtonLabel;
    [Tooltip("How long the numbers spin before the already-settled result is shown.")]
    [SerializeField] private float scrambleDuration = 1f;

    [Header("Status Texts")]
    [Tooltip("Total lit cells, rendered as \"unlocked: n / 64\".")]
    [SerializeField] private TMP_Text litCountText;
    [Tooltip("The drawn number. Grey while spinning, green when it lit a new cell, white on a repeat.")]
    [SerializeField] private TMP_Text drawDisplayText;
    [SerializeField] private TMP_Text hintText;

    [Header("Line Rewards (18, ordered: rows 1-8, cols 1-8, main diagonal, anti diagonal)")]
    [Tooltip("The \"Lines\" node. Needs row / col / diag children holding 8 / 8 / 2 BingoLines.")]
    [SerializeField] private Transform linesRoot;
    [Tooltip("The \"Rewards\" node. Same row / col / diag layout, holding the 18 BingoRewards.")]
    [SerializeField] private Transform rewardsRoot;
    [SerializeField] private List<RewardEntry> lineRewards = new List<RewardEntry>();

    [Header("Milestone Rewards")]
    [Tooltip("BingoStackItem slots, same order as the milestone list below.")]
    [SerializeField] private List<AnniversaryRewardSlot> milestoneSlots = new List<AnniversaryRewardSlot>();
    [SerializeField] private List<MilestoneEntry> milestoneRewards = new List<MilestoneEntry>();

    [Header("Stage Shortcut")]
    [Tooltip("Opens the anniversary stage directly, skipping the chapter and section screens.")]
    [SerializeField] private Button stageButton;

    [Header("Development")]
    [Tooltip("Dev only. Both ticket types become free and unlimited, and the board is stored in a " +
             "separate \"test-\" save so testing never touches the player's real progress.")]
    [SerializeField] private bool testMode;

    [Header("Audio")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip drawClip;
    [SerializeField] private AudioClip lineClip;

    private const string RewardCanvasPath = "UI/Pages/RewardCanvas";

    private const string RowGroupName = "row";
    private const string ColGroupName = "col";
    private const string DiagGroupName = "diag";
    // Each diagonal is named after the corner pair it spans: dR2uL runs down-Right to up-Left, which
    // is the save's main diagonal (cells 1, 10, ... 64); dL2uR runs down-Left to up-Right, the anti
    // diagonal (cells 8, 15, ... 57). Matched by name rather than by sibling index because the node
    // lists the anti diagonal first, the reverse of the save's line order.
    private const string MainDiagonalName = "dR2uL";
    private const string AntiDiagonalName = "dL2uR";

    // The stage this page links to. Mirrors a chapter folder under Resources/LevelData/Chapters.
    private const string StageChapter = "Anniversary";
    private const string StageSection = "1st";
    private const int StageDifficulty = 0;

    private readonly Button[] cellButtons = new Button[FirstAnniversarySave.Size];
    private readonly Image[] cellImages = new Image[FirstAnniversarySave.Size];
    /// <summary>
    /// The 18 BingoLine objects in <see cref="FirstAnniversarySave"/> line order. Held as whole
    /// objects rather than graphics because a line is shown by existing, not by its colour, so a
    /// BingoLine may be built out of however many child sprites it likes.
    /// </summary>
    private readonly GameObject[] lineObjects = new GameObject[FirstAnniversarySave.LineCount];
    /// <summary>The 18 BingoReward slots, in the same line order as <see cref="lineObjects"/>.</summary>
    private readonly AnniversaryRewardSlot[] lineSlots = new AnniversaryRewardSlot[FirstAnniversarySave.LineCount];
    /// <summary>Scratch buffer for one row / col / diag walk. Reused by lines and reward slots.</summary>
    private readonly Transform[] lineOrderedNodes = new Transform[FirstAnniversarySave.LineCount];
    private readonly List<int> runtimeExtraCurrencyIds = new List<int>();
    /// <summary>Set once the frame's currency row has been built for this page.</summary>
    private bool currencyRowPublished;
    private readonly List<int> drawBuffer = new List<int>(1);
    private bool selectMode;
    private bool busy;
    /// <summary>Set while the stage shortcut is shutting the door and handing off to the level map.</summary>
    private bool jumpingToStage;

    /// <summary>Cell index of the latest draw, or -1 before the first one. Drives the number display.</summary>
    private int lastDrawnIndex = -1;
    private bool lastDrawWasNew;

    private void Awake()
    {
        // Claimed before anything can read the board: Refresh may run from OnEnter ahead of Start.
        FirstAnniversarySave.TestMode = testMode;
    }

    private void Start()
    {
        CacheCells();
        CacheLines();
        CacheLineSlots();
        BindActionButtons();
        Refresh();
    }

    /// <summary>Ticket balance check. Always passes in test mode, so dev play never runs dry.</summary>
    private bool HasTickets(RewardName ticket, int cost) =>
        testMode || RewardingSystem.GetAmount(ticket) >= cost;

    /// <summary>Spends tickets. In test mode they are free and nothing is deducted.</summary>
    private bool SpendTickets(RewardName ticket, int cost) =>
        testMode || RewardingSystem.ConsumeItem(ticket, cost);

    #region Setup

    private void CacheCells()
    {
        if (boardRoot == null)
        {
            Debug.LogError("[AnniversaryBoardCanvas] boardRoot is not assigned.");
            return;
        }
        if (boardRoot.childCount < FirstAnniversarySave.Size)
            Debug.LogWarning($"[AnniversaryBoardCanvas] boardRoot has {boardRoot.childCount} children, expected {FirstAnniversarySave.Size}.");

        int count = Mathf.Min(boardRoot.childCount, FirstAnniversarySave.Size);
        for (int i = 0; i < count; i++)
        {
            Transform cell = boardRoot.GetChild(i);
            Button button = cell.GetComponent<Button>();
            cellButtons[i] = button;
            // Tint the button's own graphic, so the panel art doubles as the state indicator.
            cellImages[i] = cell.GetComponent<Image>() ?? button?.targetGraphic as Image;

            TMP_Text label = cell.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = (i + 1).ToString();

            if (button == null)
            {
                Debug.LogWarning($"[AnniversaryBoardCanvas] Cell {i} has no Button.");
                continue;
            }
            int index = i;
            button.onClick.AddListener(() => OnCellClicked(index));
        }
    }

    /// <summary>
    /// Collects the 18 BingoLine objects out of the "Lines" node into save line order.
    /// </summary>
    private void CacheLines()
    {
        if (!ResolveLineOrderedNodes(linesRoot, "Lines")) return;

        for (int line = 0; line < FirstAnniversarySave.LineCount; line++)
        {
            Transform node = lineOrderedNodes[line];
            lineObjects[line] = node != null ? node.gameObject : null;
        }
    }

    /// <summary>
    /// Collects the 18 BingoReward slots out of the "Rewards" node into save line order, so line
    /// rewards line up with <see cref="lineRewards"/> without eighteen manual drags — and without
    /// the chance of the two diagonals being wired the wrong way round.
    /// </summary>
    private void CacheLineSlots()
    {
        if (!ResolveLineOrderedNodes(rewardsRoot, "Rewards")) return;

        for (int line = 0; line < FirstAnniversarySave.LineCount; line++)
        {
            Transform node = lineOrderedNodes[line];
            if (node == null) continue;

            lineSlots[line] = node.GetComponent<AnniversaryRewardSlot>();
            if (lineSlots[line] == null)
                Debug.LogWarning($"[AnniversaryBoardCanvas] '{node.name}' has no AnniversaryRewardSlot; line {line} cannot be claimed.");
        }
    }

    /// <summary>
    /// Walks a "row / col / diag" node into <see cref="lineOrderedNodes"/> in
    /// <see cref="FirstAnniversarySave"/> line order: rows top-to-bottom, columns left-to-right,
    /// then the two diagonals resolved by name (see <see cref="MainDiagonalName"/>).
    /// </summary>
    /// <returns>False when the root is unassigned and there is nothing to walk.</returns>
    private bool ResolveLineOrderedNodes(Transform root, string label)
    {
        // Left unqualified elsewhere on purpose: `using System` would make every bare `Random` in
        // this file ambiguous against UnityEngine.Random.
        System.Array.Clear(lineOrderedNodes, 0, lineOrderedNodes.Length);
        if (root == null) return false;

        CacheGroup(root, label, RowGroupName, 0);
        CacheGroup(root, label, ColGroupName, FirstAnniversarySave.Width);

        Transform diag = root.Find(DiagGroupName);
        if (diag == null)
        {
            Debug.LogWarning($"[AnniversaryBoardCanvas] {label} has no '{DiagGroupName}' child.");
            return true;
        }
        int mainLine = FirstAnniversarySave.Width * 2;
        lineOrderedNodes[mainLine] = FindDiagonal(diag, MainDiagonalName, label);
        lineOrderedNodes[mainLine + 1] = FindDiagonal(diag, AntiDiagonalName, label);
        return true;
    }

    private void CacheGroup(Transform root, string label, string groupName, int firstLine)
    {
        Transform group = root.Find(groupName);
        if (group == null)
        {
            Debug.LogWarning($"[AnniversaryBoardCanvas] {label} has no '{groupName}' child.");
            return;
        }
        if (group.childCount != FirstAnniversarySave.Width)
            Debug.LogWarning($"[AnniversaryBoardCanvas] {label}/'{groupName}' holds {group.childCount} entries, expected {FirstAnniversarySave.Width}.");

        int count = Mathf.Min(group.childCount, FirstAnniversarySave.Width);
        for (int i = 0; i < count; i++) lineOrderedNodes[firstLine + i] = group.GetChild(i);
    }

    private static Transform FindDiagonal(Transform group, string nameFragment, string label)
    {
        for (int i = 0; i < group.childCount; i++)
        {
            Transform child = group.GetChild(i);
            if (child.name.Contains(nameFragment)) return child;
        }
        Debug.LogWarning($"[AnniversaryBoardCanvas] {label} has no '*{nameFragment}*' under '{DiagGroupName}'.");
        return null;
    }

    private void BindActionButtons()
    {
        if (drawButton != null) drawButton.onClick.AddListener(OnDrawClicked);
        if (selectButton != null) selectButton.onClick.AddListener(OnSelectClicked);
        if (stageButton != null) stageButton.onClick.AddListener(OnStageClicked);
    }

    #endregion

    #region Refresh

    /// <summary>Repaints the whole page from the save. Called on enter and after every state change.</summary>
    public void Refresh()
    {
        long lit = FirstAnniversarySave.GetLitMask();
        long duplicates = FirstAnniversarySave.GetDuplicateMask();
        int litCount = FirstAnniversarySave.CountBits(lit);
        int tickets = RewardingSystem.GetAmount(RewardName.Anniversary_Ticket);
        int selects = RewardingSystem.GetAmount(RewardName.Anniversary_Select);
        bool boardFull = litCount >= FirstAnniversarySave.Size;

        RefreshBoard(lit, duplicates);
        RefreshLineSlots(lit);
        RefreshMilestoneSlots(litCount);

        if (litCountText != null) litCountText.text = $"unlocked: {litCount} / {FirstAnniversarySave.Size}";

        // Skipped mid-spin so the scramble owns the display until the result lands.
        if (!busy)
        {
            SetDrawDisplay(
                lastDrawnIndex >= 0 ? lastDrawnIndex + 1 : 0,
                lastDrawnIndex >= 0 && lastDrawWasNew ? CellLitColor : CellIdleColor);
        }

        if (drawButton != null)
            drawButton.interactable = !busy && !selectMode && !boardFull
                                      && HasTickets(RewardName.Anniversary_Ticket, 1);
        if (selectButton != null)
        {
            bool canSelect = !busy && !boardFull && HasTickets(RewardName.Anniversary_Select, SelectTicketCost);
            // Stays live in select mode as the cancel button: page-in-progress hides the back
            // button, so this is the only way out. A full board can never be in select mode.
            selectButton.interactable = !boardFull && (canSelect || selectMode);
        }
        if (selectButtonLabel != null)
            selectButtonLabel.text = selectMode ? "CANCEL" : $"x{SelectTicketCost}";
        if (stageButton != null) stageButton.interactable = !busy && !selectMode;
        if (hintText != null) hintText.text = BuildHint(boardFull, tickets, selects);

        // Picking a cell is a committed interaction — hold the page until it resolves or is cancelled.
        SetPageInProgress(busy || selectMode);
        RefreshFrameUICurrencies();
    }

    /// <summary>
    /// Paints cells and line markers from an arbitrary mask pair, so the draw animation can
    /// reveal a partial state without touching the save.
    /// </summary>
    private void RefreshBoard(long lit, long duplicates)
    {
        for (int i = 0; i < cellButtons.Length; i++)
        {
            bool isLit = FirstAnniversarySave.IsSet(lit, i);

            if (cellImages[i] != null)
            {
                bool isDuplicate = FirstAnniversarySave.IsSet(duplicates, i);
                cellImages[i].color = isLit
                    ? (isDuplicate ? CellDuplicateColor : CellLitColor)
                    : (selectMode ? CellSelectableColor : CellIdleColor);
            }
            if (cellButtons[i] != null) cellButtons[i].interactable = selectMode && !isLit;
        }

        // A line simply is not there until it is complete, so an incomplete board shows no
        // scaffolding at all and a finished line reads as something that just appeared.
        for (int line = 0; line < lineObjects.Length; line++)
        {
            GameObject marker = lineObjects[line];
            if (marker != null) marker.SetActive(FirstAnniversarySave.IsLineComplete(line, lit));
        }
    }

    /// <summary>Colour a single cell should rest at under the given lit mask.</summary>
    private static Color RestingCellColor(long lit, int index) =>
        FirstAnniversarySave.IsSet(lit, index) ? CellLitColor : CellIdleColor;

    private void PaintCell(int index, Color color)
    {
        if (index < 0 || index >= cellImages.Length) return;
        if (cellImages[index] != null) cellImages[index].color = color;
    }

    private void SetDrawDisplay(int number, Color color)
    {
        if (drawDisplayText == null) return;
        drawDisplayText.text = number.ToString();
        drawDisplayText.color = color;
    }

    private void RefreshLineSlots(long lit)
    {
        bool claimsEnabled = !busy && !selectMode;
        for (int line = 0; line < lineSlots.Length; line++)
        {
            AnniversaryRewardSlot slot = lineSlots[line];
            if (slot == null) continue;

            RewardEntry reward = line < lineRewards.Count ? lineRewards[line] : null;
            bool complete = FirstAnniversarySave.IsLineComplete(line, lit);
            bool claimed = FirstAnniversarySave.IsLineClaimed(line);
            int missing = FirstAnniversarySave.GetLineMissingCount(line, lit);
            int capturedLine = line;

            slot.Configure(
                reward != null ? reward.kind : RewardType.item,
                reward != null ? reward.gainId : 0,
                reward != null ? reward.amount : 0,
                complete,
                claimed,
                claimsEnabled,
                FirstAnniversarySave.GetLineName(line),
                complete ? string.Empty : $"-{missing}",
                () => ClaimLineReward(capturedLine));
        }
    }

    private void RefreshMilestoneSlots(int litCount)
    {
        bool claimsEnabled = !busy && !selectMode;
        for (int i = 0; i < milestoneSlots.Count && i < milestoneRewards.Count; i++)
        {
            AnniversaryRewardSlot slot = milestoneSlots[i];
            MilestoneEntry entry = milestoneRewards[i];
            if (slot == null || entry == null) continue;

            RewardEntry reward = entry.reward ?? new RewardEntry();
            bool reached = litCount >= entry.litRequirement;
            bool claimed = FirstAnniversarySave.IsMilestoneClaimed(i);
            int captured = i;

            slot.Configure(
                reward.kind,
                reward.gainId,
                reward.amount,
                reached,
                claimed,
                claimsEnabled,
                null,
                $"{Mathf.Min(litCount, entry.litRequirement)} / {entry.litRequirement}",
                () => ClaimMilestoneReward(captured));
        }
    }

    private string BuildHint(bool boardFull, int tickets, int selects)
    {
        if (boardFull) return "BOARD COMPLETE !";
        if (selectMode) return "PICK ONE NUMBER TO LIGHT UP";
        // Standing reminder that this build spends nothing; the balance hints below would lie here.
        if (testMode) return "TEST MODE — FREE TICKETS";
        if (tickets >= 1) return string.Empty;
        if (selects >= SelectTicketCost) return "NO TICKET — USE YOUR SELECT TICKETS";
        return "NO TICKET LEFT";
    }

    /// <summary>
    /// Keeps the frame's currency row in sync with the page.
    /// <para>
    /// The row's composition is fixed — the page's own extras plus the two ticket types — so it is
    /// published once. Every refresh after that only pushes fresh numbers through
    /// <see cref="FrameUIDisplayer.RefreshCurrencyAmounts"/>, because republishing the list makes
    /// <see cref="FrameUIDisplayer"/> destroy and re-instantiate every item, which would churn a
    /// handful of GameObjects on each draw, claim and cell pick.
    /// </para>
    /// </summary>
    private void RefreshFrameUICurrencies()
    {
        if (FrameUI == null) return;

        if (!currencyRowPublished)
        {
            runtimeExtraCurrencyIds.Clear();
            if (ExtraCurrencyIds != null)
            {
                for (int i = 0; i < ExtraCurrencyIds.Count; i++)
                {
                    int id = ExtraCurrencyIds[i];
                    if (!runtimeExtraCurrencyIds.Contains(id)) runtimeExtraCurrencyIds.Add(id);
                }
            }
            AppendCurrency(RewardName.Anniversary_Ticket);
            AppendCurrency(RewardName.Anniversary_Select);

            currencyRowPublished = true;
            // Builds the row, and the items are created with current amounts already in them.
            FrameUI.SetCurrentExtraCurrencies(runtimeExtraCurrencyIds);
            return;
        }

        FrameUI.RefreshCurrencyAmounts();
    }

    /// <summary>
    /// Pushes ticket balances to the frame right now, without a full page repaint. Used at the
    /// moments a balance changes mid-flow — paying for a draw, or being handed duplicate refunds —
    /// so the number on screen never lags behind the spend by a whole animation.
    /// </summary>
    private void RefreshCurrencyDisplay()
    {
        if (FrameUI != null && currencyRowPublished) FrameUI.RefreshCurrencyAmounts();
    }

    private void AppendCurrency(RewardName rewardName)
    {
        int id = RewardingSystem.RewardNumMap[rewardName];
        if (!runtimeExtraCurrencyIds.Contains(id)) runtimeExtraCurrencyIds.Add(id);
    }

    #endregion

    #region Draw

    private void OnDrawClicked()
    {
        if (busy || selectMode) return;
        if (FirstAnniversarySave.IsBoardFull()) return;
        if (!SpendTickets(RewardName.Anniversary_Ticket, 1)) return;

        // The ticket is gone the moment it is paid, so show that before the spin starts.
        RefreshCurrencyDisplay();
        StartCoroutine(DrawRoutine());
    }

    private IEnumerator DrawRoutine()
    {
        busy = true;
        SetPageInProgress(true);
        if (drawButton != null) drawButton.interactable = false;
        if (selectButton != null) selectButton.interactable = false;

        try
        {
            long litBefore = FirstAnniversarySave.GetLitMask();
            int drawn = Random.Range(0, FirstAnniversarySave.Size);

            // Settle and persist the outcome before any animation: the spin below is pure
            // presentation, so a page teardown mid-spin cannot cost the already-paid ticket.
            drawBuffer.Clear();
            drawBuffer.Add(drawn);
            FirstAnniversarySave.ApplyDraws(drawBuffer, out int newlyLit, out int duplicates);
            // A repeat pays back in select tickets, granted here rather than through the reward
            // popup: the compensation should read as part of the draw, not interrupt it.
            if (duplicates > 0)
            {
                RewardingSystem.GainReward(RewardName.Anniversary_Select, duplicates);
                RefreshCurrencyDisplay();
            }

            lastDrawnIndex = drawn;
            lastDrawWasNew = newlyLit > 0;

            PlaySfx(drawClip);
            yield return ScrambleRoutine(litBefore);

            if (lastDrawWasNew && HasNewCompletedLine()) PlaySfx(lineClip);
        }
        finally
        {
            busy = false;
            SetPageInProgress(false);
            // Repaints from the real save: the drawn cell turns green or blue, and the number
            // display, totals, bingo lines and milestones all catch up in one pass.
            Refresh();
        }
    }

    /// <summary>
    /// Sweeps a random cell per frame for <see cref="scrambleDuration"/>, painting only that cell
    /// and the number display. Everything is drawn from <paramref name="litBefore"/> so the result
    /// already sitting in the save stays hidden until the spin ends.
    /// </summary>
    private IEnumerator ScrambleRoutine(long litBefore)
    {
        RefreshBoard(litBefore, 0L);

        int shown = -1;
        float elapsed = 0f;
        while (elapsed < scrambleDuration)
        {
            if (shown >= 0) PaintCell(shown, RestingCellColor(litBefore, shown));

            shown = Random.Range(0, FirstAnniversarySave.Size);
            PaintCell(shown, FirstAnniversarySave.IsSet(litBefore, shown)
                ? CellDuplicateColor
                : CellScrambleColor);
            SetDrawDisplay(shown + 1, DrawTextScrambleColor);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (shown >= 0) PaintCell(shown, RestingCellColor(litBefore, shown));
    }

    private bool HasNewCompletedLine()
    {
        long lit = FirstAnniversarySave.GetLitMask();
        for (int line = 0; line < FirstAnniversarySave.LineCount; line++)
        {
            if (FirstAnniversarySave.IsLineComplete(line, lit) && !FirstAnniversarySave.IsLineClaimed(line)) return true;
        }
        return false;
    }

    #endregion

    #region Select

    /// <summary>
    /// Arms a single pick, or cancels one already armed. Each activation buys exactly one cell:
    /// <see cref="OnCellClicked"/> disarms as soon as the pick lands, so spending a second batch of
    /// tickets always takes a second deliberate press of this button.
    /// </summary>
    private void OnSelectClicked()
    {
        if (busy) return;
        if (selectMode)
        {
            selectMode = false;
            Refresh();
            return;
        }
        if (FirstAnniversarySave.IsBoardFull()) return;
        if (!HasTickets(RewardName.Anniversary_Select, SelectTicketCost)) return;

        selectMode = true;
        FirstAnniversarySave.ClearDuplicateFlags();
        Refresh();
    }

    private void OnCellClicked(int index)
    {
        if (busy || !selectMode) return;
        if (FirstAnniversarySave.IsLit(index)) return;
        if (!SpendTickets(RewardName.Anniversary_Select, SelectTicketCost)) return;

        if (!FirstAnniversarySave.ApplySelect(index))
        {
            // Lost the race against another write: refund so the tickets are never silently eaten.
            // Nothing was deducted in test mode, so there is nothing to give back.
            // The armed pick survives, since it never actually landed.
            //if (!testMode) 
            RewardingSystem.GainReward(RewardName.Anniversary_Select, SelectTicketCost);
            Refresh();
            return;
        }

        PlaySfx(drawClip);
        if (HasNewCompletedLine()) PlaySfx(lineClip);
        // One pick per activation: the board goes back to being untouchable, and the cells stop
        // advertising themselves as pickable, until the select button is pressed again.
        selectMode = false;
        Refresh();
    }

    #endregion

    #region Claims

    private void ClaimLineReward(int line)
    {
        if (busy || selectMode) return;
        if (!FirstAnniversarySave.IsLineComplete(line)) return;
        RewardEntry reward = line < lineRewards.Count ? lineRewards[line] : null;
        if (!IsDeliverable(reward, $"line {line}")) return;
        if (!FirstAnniversarySave.ClaimLine(line)) return;

        Deliver(reward);
        Refresh();
    }

    private void ClaimMilestoneReward(int milestone)
    {
        if (busy || selectMode) return;
        if (milestone < 0 || milestone >= milestoneRewards.Count) return;
        MilestoneEntry entry = milestoneRewards[milestone];
        if (entry == null) return;
        if (!IsDeliverable(entry.reward, $"milestone {milestone}")) return;
        if (FirstAnniversarySave.GetLitCount() < entry.litRequirement) return;
        if (!FirstAnniversarySave.ClaimMilestone(milestone)) return;

        Deliver(entry.reward);
        Refresh();
    }

    /// <summary>
    /// Whether an entry can actually be handed over. Checked before the claim bit is written, so a
    /// reward list the inspector left half-filled costs the player nothing: the button simply does
    /// not resolve, and the slot stays claimable once the entry is authored.
    /// </summary>
    private static bool IsDeliverable(RewardEntry reward, string what)
    {
        if (reward == null)
        {
            Debug.LogWarning($"[AnniversaryBoardCanvas] No reward authored for {what}.");
            return false;
        }
        if (reward.kind != RewardType.item && reward.kind != RewardType.character && reward.kind != RewardType.UnlockTire)
        {
            Debug.LogWarning($"[AnniversaryBoardCanvas] Reward kind {reward.kind} on {what} is not deliverable.");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Grants one reward entry and shows it. Mirrors <c>LevelController.ApplyReward</c> so an
    /// anniversary reward lands in the save exactly the way the same reward would from a stage clear.
    /// The claim bit is already written by the caller, so this runs at most once per entry.
    /// </summary>
    private void Deliver(RewardEntry reward)
    {
        int amount = Mathf.Max(1, reward.amount);
        switch (reward.kind)
        {
            case RewardType.item:
                RewardingSystem.GainRewardByOrder(reward.gainId, amount);
                break;
            case RewardType.character:
                // Grants the unit itself: level 1 plus tire 0, the "obtained" state the popup draws.
                CharacterUpgradeSave.UpgradeCharacterByClear(reward.gainId.ToString("0000"));
                break;
            case RewardType.UnlockTire:
                RewardIconHelper.ParseUnlockTireId(reward.gainId, out string characterId, out int tire);
                CharacterUpgradeSave.UnlockCharacterTire(characterId, tire);
                break;
            default:
                Debug.LogWarning($"[AnniversaryBoardCanvas] Reward kind {reward.kind} is not deliverable; nothing granted.");
                return;
        }

        ShowRewardPopup(reward, amount);
        if (FrameUI != null) FrameUI.RefreshCurrencyAmounts();
    }

    private void ShowRewardPopup(RewardEntry reward, int amount)
    {
        GameObject prefab = Resources.Load<GameObject>(RewardCanvasPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[AnniversaryBoardCanvas] Missing reward prefab at Resources/{RewardCanvasPath}");
            return;
        }
        GameObject obj = Instantiate(prefab);
        RewardCanvas canvas = obj.GetComponent<RewardCanvas>();
        if (canvas == null)
        {
            Destroy(obj);
            return;
        }
        canvas.Initialize(reward.kind, reward.gainId, amount);
    }

    #endregion

    #region Stage Shortcut

    /// <summary>
    /// Force-selects the anniversary stage and opens the level map, so the player never has to walk
    /// the base → chapter → section → difficulty path to reach it.
    /// </summary>
    private void OnStageClicked()
    {
        if (busy || selectMode || jumpingToStage) return;

        // Resolved before the door starts closing: a missing asset must not leave the page locked
        // behind a shut door with nowhere to go.
        string loadPath = $"LevelData/Chapters/{StageChapter}/{StageSection}";
        MapInfo map = Resources.Load<MapInfo>(loadPath);
        if (map == null)
        {
            Debug.LogError($"[AnniversaryBoardCanvas] Missing MapInfo at Resources/{loadPath}");
            return;
        }

        BaseCanvas baseCanvas = ResolveBaseCanvas();
        if (baseCanvas == null)
        {
            Debug.LogError("[AnniversaryBoardCanvas] BaseCanvas not found; cannot open the stage.");
            return;
        }

        StartCoroutine(StageJumpRoutine(baseCanvas, map));
    }

    /// <summary>
    /// Shuts the door over the board before navigating, so the level map's world-map instantiation
    /// happens out of sight and the page cannot be poked at mid-transition.
    /// </summary>
    private IEnumerator StageJumpRoutine(BaseCanvas baseCanvas, MapInfo map)
    {
        jumpingToStage = true;
        busy = true;
        // Greys out draw / select / claims and holds the back button for the whole transition.
        Refresh();

        if (FrameUI != null)
        {
            FrameUI.CloseDoor();
            yield return new WaitForSecondsRealtime(FrameUIAnimations.DoorDuration);
        }

        // The four keys SectionButton and DifficultyBoard would have written between them.
        // LevelTiler, GameProgressSave and LevelController all read the stage out of these, and the
        // values must survive into the battle scene, so they stay overwritten after the jump.
        PlayerPrefs.SetString(UXPref.ChapterName, StageChapter);
        PlayerPrefs.SetString(UXPref.SectionName, StageSection);
        PlayerPrefs.SetInt(UXPref.SectionNum, 0);
        PlayerPrefs.SetInt(UXPref.Difficulty, StageDifficulty);

        baseCanvas.LoadMap(map);
    }

    private static BaseCanvas ResolveBaseCanvas()
    {
        GameObject root = GameObject.Find("BaseCanvas");
        return root != null ? root.GetComponent<BaseCanvas>() : null;
    }

    #endregion

    private void PlaySfx(AudioClip clip)
    {
        if (sfxSource == null || clip == null) return;
        PlatformAudio.PlayOneShot(sfxSource, clip);
    }

    public override IEnumerator OnEnter()
    {
        // The page is only deactivated, not destroyed, while the stage is open — clear the transition
        // lock the jump left behind so returning from the map gives back a live board.
        jumpingToStage = false;
        busy = false;
        Refresh();
        if (FrameUI != null)
        {
            FrameUI.OpenDoor();
            yield return new WaitForSecondsRealtime(FrameUIAnimations.DoorDuration);
        }
    }

    public override IEnumerator OnExit()
    {
        selectMode = false;
        FirstAnniversarySave.ClearDuplicateFlags();
        // The stage jump shuts the door itself before navigating; closing again would replay the
        // animation and cost a second door duration.
        if (FrameUI != null && !jumpingToStage)
        {
            FrameUI.CloseDoor();
            yield return new WaitForSecondsRealtime(FrameUIAnimations.DoorDuration);
        }
    }
}
