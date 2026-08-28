using System;
using System.Globalization;

public enum BontiqueType
{
    Daily = 0,
    Weekly = 1,
    Monthly = 2,
    Supplies = 3,
    Characters = 4,
    Event = 5,
    Others = 6,
    Builda = 7,
    Unknown = -1
}
public enum LimitType
{
    None = 0,
    Day = 1,
    Week = 2,
    Month = 3,
    Year = 4,
    OnlyOnce = 5,
    Event = 6
}

[Serializable]
public class BontiqueShopItem
{
    public string bid;
    public BontiqueType Category;
    public RewardType RewardKind;
    public int gainId;
    public int ObtainAmount;
    public int CurrencyId;
    public int CurrencyAmount;
    public LimitType Limit;
    public int LimitCount;
    public DateTime? LimitStart;
    public DateTime? LimitEnd;
    /// <summary>
    /// Non-empty: this SKU is a Builda <c>payPoints</c> product. Redeem calls
    /// <c>PayShowPanel</c> instead of consuming in-game currency. Must match
    /// <c>^[A-Za-z0-9_-]{1,64}$</c> (no colon).
    /// </summary>
    public string PayId;

    public bool IsPlatformPay => !string.IsNullOrEmpty(PayId);

    public bool IsInActiveWindow(DateTime now)
    {
        if (Limit == LimitType.Event)
        {
            if (!LimitStart.HasValue || !LimitEnd.HasValue) return false;
            int nowMd = now.Month * 100 + now.Day;
            int startMd = LimitStart.Value.Month * 100 + LimitStart.Value.Day;
            int endMd = LimitEnd.Value.Month * 100 + LimitEnd.Value.Day;
            if (startMd <= endMd)
            {
                return nowMd >= startMd && nowMd <= endMd;
            }
            // Support wrapped windows like Dec -> Jan.
            return nowMd >= startMd || nowMd <= endMd;
        }
        return true;
    }

    public bool IsPeriodExpired(DateTime firstPurchaseDate, DateTime now)
    {
        if (Limit == LimitType.Day) return firstPurchaseDate.Date != now.Date;
        if (Limit == LimitType.Week) return GetWeekStart(firstPurchaseDate) != GetWeekStart(now);
        if (Limit == LimitType.Month) return firstPurchaseDate.Year != now.Year || firstPurchaseDate.Month != now.Month;
        if (Limit == LimitType.Year) return firstPurchaseDate.Year != now.Year;
        return false;
    }

    public static BontiqueShopItem FromCsvRow(string[] cols)
    {
        // columns (new): bid, category, rewardType, gainId, obtainAmount, currencyId, currencyAmount, limitType, limitCount, start, end
        // columns (legacy): bid, b_name_id, category, rewardType, gainId, obtainAmount, currencyId, currencyAmount, limitType, limitCount, start, end
        int offset = HasLegacyNameColumn(cols) ? 1 : 0;
        var item = new BontiqueShopItem
        {
            bid = GetTrimmed(cols, 0),
            Category = ToBontiqueType(ParseInt(cols, 1 + offset)),
            RewardKind = ToRewardType(ParseInt(cols, 2 + offset)),
            gainId = ParseInt(cols, 3 + offset),
            ObtainAmount = ParseInt(cols, 4 + offset),
            CurrencyId = ParseInt(cols, 5 + offset),
            CurrencyAmount = ParseInt(cols, 6 + offset),
            Limit = ToLimitType(ParseInt(cols, 7 + offset)),
            LimitCount = ParseInt(cols, 8 + offset),
            LimitStart = ParseDate(cols, 9 + offset),
            LimitEnd = ParseDate(cols, 10 + offset)
        };
        return item;
    }

    private static bool HasLegacyNameColumn(string[] cols)
    {
        if (cols == null || cols.Length < 3) return false;
        return !int.TryParse(cols[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }

    private static string GetTrimmed(string[] cols, int index)
    {
        if (cols == null || index < 0 || index >= cols.Length || cols[index] == null) return string.Empty;
        return cols[index].Trim();
    }

    private static int ParseInt(string[] cols, int index)
    {
        if (cols == null || index < 0 || index >= cols.Length) return 0;
        return int.TryParse(cols[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;
    }

    private static DateTime? ParseDate(string[] cols, int index)
    {
        if (cols == null || index < 0 || index >= cols.Length) return null;
        if (string.IsNullOrWhiteSpace(cols[index])) return null;
        return DateTime.TryParse(cols[index], CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime d) ? d : (DateTime?)null;
    }

    private static BontiqueType ToBontiqueType(int value)
    {
        return value >= 0 && value <= 7 ? (BontiqueType)value : BontiqueType.Unknown;
    }

    private static RewardType ToRewardType(int value)
    {
        return Enum.IsDefined(typeof(RewardType), value) ? (RewardType)value : RewardType.item;
    }

    private static LimitType ToLimitType(int value)
    {
        return value >= 0 && value <= 6 ? (LimitType)value : LimitType.None;
    }

    private static DateTime GetWeekStart(DateTime dt)
    {
        // Monday as week start
        int diff = ((int)dt.DayOfWeek + 6) % 7;
        return dt.Date.AddDays(-diff);
    }
}
