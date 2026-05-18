using System;
using System.Collections.Generic;

public static class BontiqueStaticCatalog
{
    private static readonly List<BontiqueShopItem> Catalog = new List<BontiqueShopItem>
    {
        // Non-event items keep LimitStart/LimitEnd as null.
        // Event items use month-day windows (year ignored).
        #region Daily
        new BontiqueShopItem
        {
            bid = "day:can-sup", Category = BontiqueType.Daily, Limit = LimitType.Day, RewardKind = RewardType.item,
            gainId = 12, ObtainAmount = 50,
            CurrencyId = 11, CurrencyAmount = 1,
            LimitCount = 1,
            LimitStart = null, LimitEnd = null
        },
        #endregion
        #region Weekly
        new BontiqueShopItem
        {
            bid = "week:can-sup", Category = BontiqueType.Weekly, Limit = LimitType.Week, RewardKind = RewardType.item,
            gainId = 12, ObtainAmount = 150,
            CurrencyId = 11, CurrencyAmount = 1,
            LimitCount = 1,
            LimitStart = null, LimitEnd = null
        },
        #endregion
        #region Monthly
        new BontiqueShopItem
        {
            bid = "month:can-sup", Category = BontiqueType.Monthly, Limit = LimitType.Month, RewardKind = RewardType.item,
            gainId = 12, ObtainAmount = 500,
            CurrencyId = 11, CurrencyAmount = 1,
            LimitCount = 1,
            LimitStart = null, LimitEnd = null
        },
        #endregion
        #region OnlyOnce
        new BontiqueShopItem
        {
            bid = "once:open2026", Category = BontiqueType.Event, Limit = LimitType.OnlyOnce, RewardKind = RewardType.item,
            gainId = 12, ObtainAmount = 2026,
            CurrencyId = 11, CurrencyAmount = 1,
            LimitCount = 1,
            LimitStart = null, LimitEnd = null
        },
        #endregion
    };

    private static readonly Dictionary<string, BontiqueShopItem> ByBid = new Dictionary<string, BontiqueShopItem>(StringComparer.Ordinal);
    private static readonly Dictionary<BontiqueType, List<BontiqueShopItem>> ByCategory = new Dictionary<BontiqueType, List<BontiqueShopItem>>();
    private static readonly IReadOnlyList<BontiqueShopItem> EmptyList = new List<BontiqueShopItem>();

    static BontiqueStaticCatalog()
    {
        for (int i = 0; i < Catalog.Count; i++)
        {
            BontiqueShopItem item = Catalog[i];
            if (item == null) continue;

            if (!string.IsNullOrEmpty(item.bid) && !ByBid.ContainsKey(item.bid))
            {
                ByBid[item.bid] = item;
            }

            if (!ByCategory.TryGetValue(item.Category, out List<BontiqueShopItem> list))
            {
                list = new List<BontiqueShopItem>();
                ByCategory[item.Category] = list;
            }
            list.Add(item);
        }
    }

    public static IReadOnlyList<BontiqueShopItem> GetAllItems()
    {
        return Catalog;
    }

    public static bool TryGetItemByBid(string bid, out BontiqueShopItem item)
    {
        if (string.IsNullOrEmpty(bid))
        {
            item = null;
            return false;
        }
        return ByBid.TryGetValue(bid, out item);
    }

    public static BontiqueShopItem GetItemByBid(string bid)
    {
        return TryGetItemByBid(bid, out BontiqueShopItem item) ? item : null;
    }

    public static IReadOnlyList<BontiqueShopItem> GetItemsByCategory(BontiqueType category)
    {
        return ByCategory.TryGetValue(category, out List<BontiqueShopItem> list) ? list : EmptyList;
    }
}
