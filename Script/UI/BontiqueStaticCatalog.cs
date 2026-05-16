using System;
using System.Collections.Generic;

public static class BontiqueStaticCatalog
{
    public static List<BontiqueShopItem> GetTemplateItems()
    {
        // Template catalog organized by current BontiqueType.
        // Non-event items keep LimitStart/LimitEnd as null. Event items use month-day windows (year ignored).
        return new List<BontiqueShopItem>
        {
            new BontiqueShopItem
            {
                bid = "shop_daily_xp",
                b_name_id = "shop_name_daily_xp",
                Category = BontiqueType.Dayly,
                RewardKind = RewardType.item,
                gainId = 11,
                ObtainAmount = 5000,
                CurrencyId = 12,
                CurrencyAmount = 100,
                Limit = LimitType.Day,
                LimitCount = 3,
                LimitStart = null,
                LimitEnd = null
            },
            new BontiqueShopItem
            {
                bid = "shop_weekly_ticket",
                b_name_id = "shop_name_weekly_ticket",
                Category = BontiqueType.Weekly,
                RewardKind = RewardType.item,
                gainId = 6,
                ObtainAmount = 1,
                CurrencyId = 12,
                CurrencyAmount = 300,
                Limit = LimitType.Week,
                LimitCount = 2,
                LimitStart = null,
                LimitEnd = null
            },
            new BontiqueShopItem
            {
                bid = "shop_monthly_catfruit",
                b_name_id = "shop_name_monthly_catfruit",
                Category = BontiqueType.Monthly,
                RewardKind = RewardType.item,
                gainId = 39,
                ObtainAmount = 2,
                CurrencyId = 12,
                CurrencyAmount = 800,
                Limit = LimitType.Month,
                LimitCount = 1,
                LimitStart = null,
                LimitEnd = null
            },
            new BontiqueShopItem
            {
                bid = "shop_supplies_cans_pack",
                b_name_id = "shop_name_supplies_cans_pack",
                Category = BontiqueType.Supplies,
                RewardKind = RewardType.item,
                gainId = 12,
                ObtainAmount = 200,
                CurrencyId = 11,
                CurrencyAmount = 20000,
                Limit = LimitType.None,
                LimitCount = 0,
                LimitStart = null,
                LimitEnd = null
            },
            new BontiqueShopItem
            {
                bid = "shop_characters_starter",
                b_name_id = "shop_name_characters_starter",
                Category = BontiqueType.Characters,
                RewardKind = RewardType.character,
                gainId = 1000,
                ObtainAmount = 1,
                CurrencyId = 12,
                CurrencyAmount = 2500,
                Limit = LimitType.None,
                LimitCount = 0,
                LimitStart = null,
                LimitEnd = null
            },
            new BontiqueShopItem
            {
                bid = "shop_onlyonce_catfruit_bundle",
                b_name_id = "shop_name_onlyonce_catfruit_bundle",
                Category = BontiqueType.OnlyOnce,
                RewardKind = RewardType.item,
                gainId = 57,
                ObtainAmount = 50,
                CurrencyId = 12,
                CurrencyAmount = 1200,
                Limit = LimitType.OnlyOnce,
                LimitCount = 1,
                LimitStart = null,
                LimitEnd = null
            },
            new BontiqueShopItem
            {
                bid = "shop_event_golden_ticket",
                b_name_id = "shop_name_event_golden_ticket",
                Category = BontiqueType.Event,
                RewardKind = RewardType.item,
                gainId = 7,
                ObtainAmount = 1,
                CurrencyId = 12,
                CurrencyAmount = 500,
                Limit = LimitType.Event,
                LimitCount = 1,
                LimitStart = new DateTime(2000, 3, 1, 0, 0, 0),
                LimitEnd = new DateTime(2000, 3, 31, 23, 59, 59)
            },
            new BontiqueShopItem
            {
                bid = "shop_others_yearly_legend",
                b_name_id = "shop_name_others_yearly_legend",
                Category = BontiqueType.Others,
                RewardKind = RewardType.item,
                gainId = 10,
                ObtainAmount = 1,
                CurrencyId = 12,
                CurrencyAmount = 3000,
                Limit = LimitType.Year,
                LimitCount = 1,
                LimitStart = null,
                LimitEnd = null
            }
        };
    }
}
