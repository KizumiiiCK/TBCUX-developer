using System;
using System.Globalization;

public enum BontiqueType
{
    Type0 = 0,
    Type1 = 1,
    Type2 = 2,
    Type3 = 3,
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
    public string b_name_id;
    public BontiqueType Category;
    public RewardType RewardKind;
    public int gainId;
    public int ObtainAmount;
    public int CurrencyId;
    public int CurrencyAmount;
    public LimitType Limit;
    public int LimitCount;
    public DateTime LimitStart;
    public DateTime LimitEnd;

    public bool IsInActiveWindow(DateTime now)
    {
        if (Limit == LimitType.Event)
        {
            return now >= LimitStart && now <= LimitEnd;
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
        // columns: bid, b_name_id, category, rewardType, gainId, obtainAmount, currencyId, currencyAmount, limitType, limitCount, start, end
        var item = new BontiqueShopItem
        {
            bid = GetTrimmed(cols, 0),
            b_name_id = GetTrimmed(cols, 1),
            Category = ToBontiqueType(ParseInt(cols, 2)),
            RewardKind = ToRewardType(ParseInt(cols, 3)),
            gainId = ParseInt(cols, 4),
            ObtainAmount = ParseInt(cols, 5),
            CurrencyId = ParseInt(cols, 6),
            CurrencyAmount = ParseInt(cols, 7),
            Limit = ToLimitType(ParseInt(cols, 8)),
            LimitCount = ParseInt(cols, 9),
            LimitStart = ParseDate(cols, 10, DateTime.MinValue),
            LimitEnd = ParseDate(cols, 11, DateTime.MaxValue)
        };
        return item;
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

    private static DateTime ParseDate(string[] cols, int index, DateTime fallback)
    {
        if (cols == null || index < 0 || index >= cols.Length) return fallback;
        return DateTime.TryParse(cols[index], CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime d) ? d : fallback;
    }

    private static BontiqueType ToBontiqueType(int value)
    {
        return value >= 0 && value <= 3 ? (BontiqueType)value : BontiqueType.Unknown;
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
