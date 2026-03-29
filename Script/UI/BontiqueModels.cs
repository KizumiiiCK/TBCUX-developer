using System;

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

    public static BontiqueShopItem FromCsvRow(string[] cols)
    {
        // Expect at least 10 columns: category, rewardType, id, obtainAmount, currencyId, currencyAmount, limitType, limitCount, start, end
        var item = new BontiqueShopItem();
        try
        {
            if (cols.Length > 0) item.bid = cols[0];
            if (cols.Length > 1 && int.TryParse(cols[1], out int c)) item.Category = Enum.IsDefined(typeof(BontiqueType), c) ? (BontiqueType)c : BontiqueType.Unknown;
            if (cols.Length > 2 && int.TryParse(cols[2], out int r)) item.RewardKind = Enum.IsDefined(typeof(RewardType), r) ? (RewardType)r : RewardType.item;
            if (cols.Length > 3) int.TryParse(cols[3], out item.gainId);
            if (cols.Length > 4) int.TryParse(cols[4], out item.ObtainAmount);
            if (cols.Length > 5) int.TryParse(cols[5], out item.CurrencyId);
            if (cols.Length > 6) int.TryParse(cols[6], out item.CurrencyAmount);
            if (cols.Length > 7 && int.TryParse(cols[7], out int lt)) item.Limit = Enum.IsDefined(typeof(LimitType), lt) ? (LimitType)lt : LimitType.None;
            if (cols.Length > 8) int.TryParse(cols[8], out item.LimitCount);
            if (cols.Length > 9 && DateTime.TryParse(cols[9], out DateTime s)) item.LimitStart = s; else item.LimitStart = DateTime.MinValue;
            if (cols.Length > 10 && DateTime.TryParse(cols[10], out DateTime e)) item.LimitEnd = e; else item.LimitEnd = DateTime.MaxValue;
        }
        catch
        {
            // ignore and return partially filled
        }
        return item;
    }
}
