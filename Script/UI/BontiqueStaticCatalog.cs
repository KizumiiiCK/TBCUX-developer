using System;
using System.Collections.Generic;
using UnityEngine;

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
        },
        new BontiqueShopItem
        {
            bid = "day:can2xp-20", Category = BontiqueType.Daily, Limit = LimitType.Day, RewardKind = RewardType.item,
            gainId = 11, ObtainAmount = 125000,
            CurrencyId = 12, CurrencyAmount = 20,
            LimitCount = 1,
        },
        new BontiqueShopItem
        {
            bid = "day:can2xp-50", Category = BontiqueType.Daily, Limit = LimitType.Day, RewardKind = RewardType.item,
            gainId = 11, ObtainAmount = 400000,
            CurrencyId = 12, CurrencyAmount = 50,
            LimitCount = 1,
        },
        new BontiqueShopItem
        {
            bid = "day:can2xp-100", Category = BontiqueType.Daily, Limit = LimitType.Day, RewardKind = RewardType.item,
            gainId = 11, ObtainAmount = 1000000,
            CurrencyId = 12, CurrencyAmount = 90,
            LimitCount = 1,
        },
        #endregion
        #region Weekly
        new BontiqueShopItem
        {
            bid = "week:can-sup", Category = BontiqueType.Weekly, Limit = LimitType.Week, RewardKind = RewardType.item,
            gainId = 12, ObtainAmount = 150,
            CurrencyId = 11, CurrencyAmount = 1,
            LimitCount = 1,
        },
        new BontiqueShopItem
        {
            bid = "week:xp2cf-p", Category = BontiqueType.Weekly, Limit = LimitType.Week, RewardKind = RewardType.item,
            gainId = 34, ObtainAmount = 1,
            CurrencyId = 11, CurrencyAmount = 240000,
            LimitCount = 3,
        },
        new BontiqueShopItem
        {
            bid = "week:xp2cf-r", Category = BontiqueType.Weekly, Limit = LimitType.Week, RewardKind = RewardType.item,
            gainId = 35, ObtainAmount = 1,
            CurrencyId = 11, CurrencyAmount = 240000,
            LimitCount = 3,
        },
        new BontiqueShopItem
        {
            bid = "week:xp2cf-b", Category = BontiqueType.Weekly, Limit = LimitType.Week, RewardKind = RewardType.item,
            gainId = 36, ObtainAmount = 1,
            CurrencyId = 11, CurrencyAmount = 240000,
            LimitCount = 3,
        },
        new BontiqueShopItem
        {
            bid = "week:xp2cf-g", Category = BontiqueType.Weekly, Limit = LimitType.Week, RewardKind = RewardType.item,
            gainId = 37, ObtainAmount = 1,
            CurrencyId = 11, CurrencyAmount = 240000,
            LimitCount = 3,
        },
        new BontiqueShopItem
        {
            bid = "week:xp2cf-y", Category = BontiqueType.Weekly, Limit = LimitType.Week, RewardKind = RewardType.item,
            gainId = 38, ObtainAmount = 1,
            CurrencyId = 11, CurrencyAmount = 240000,
            LimitCount = 3,
        },
        new BontiqueShopItem
        {
            bid = "week:xp2cfs-p", Category = BontiqueType.Weekly, Limit = LimitType.Week, RewardKind = RewardType.item,
            gainId = 26, ObtainAmount = 1,
            CurrencyId = 11, CurrencyAmount = 60000,
            LimitCount = 5,
        },
        new BontiqueShopItem
        {
            bid = "week:xp2cfs-r", Category = BontiqueType.Weekly, Limit = LimitType.Week, RewardKind = RewardType.item,
            gainId = 27, ObtainAmount = 1,
            CurrencyId = 11, CurrencyAmount = 60000,
            LimitCount = 5,
        },
        new BontiqueShopItem
        {
            bid = "week:xp2cfs-b", Category = BontiqueType.Weekly, Limit = LimitType.Week, RewardKind = RewardType.item,
            gainId = 28, ObtainAmount = 1,
            CurrencyId = 11, CurrencyAmount = 60000,
            LimitCount = 5,
        },
        new BontiqueShopItem
        {
            bid = "week:xp2cfs-g", Category = BontiqueType.Weekly, Limit = LimitType.Week, RewardKind = RewardType.item,
            gainId = 29, ObtainAmount = 1,
            CurrencyId = 11, CurrencyAmount = 60000,
            LimitCount = 5,
        },
        new BontiqueShopItem
        {
            bid = "week:xp2cfs-y", Category = BontiqueType.Weekly, Limit = LimitType.Week, RewardKind = RewardType.item,
            gainId = 30, ObtainAmount = 1,
            CurrencyId = 11, CurrencyAmount = 60000,
            LimitCount = 5,
        },
        new BontiqueShopItem
        {
            bid = "week:cf-p2cf-rb", Category = BontiqueType.Weekly, Limit = LimitType.Week, RewardKind = RewardType.item,
            gainId = 39, ObtainAmount = 1,
            CurrencyId = 34, CurrencyAmount = 8,
            LimitCount = 1,
        },
        new BontiqueShopItem
        {
            bid = "week:cf-r2cf-rb", Category = BontiqueType.Weekly, Limit = LimitType.Week, RewardKind = RewardType.item,
            gainId = 39, ObtainAmount = 1,
            CurrencyId = 35, CurrencyAmount = 8,
            LimitCount = 1,
        },
        new BontiqueShopItem
        {
            bid = "week:cf-b2cf-rb", Category = BontiqueType.Weekly, Limit = LimitType.Week, RewardKind = RewardType.item,
            gainId = 39, ObtainAmount = 1,
            CurrencyId = 36, CurrencyAmount = 8,
            LimitCount = 1,
        },
        new BontiqueShopItem
        {
            bid = "week:cf-g2cf-rb", Category = BontiqueType.Weekly, Limit = LimitType.Week, RewardKind = RewardType.item,
            gainId = 39, ObtainAmount = 1,
            CurrencyId = 37, CurrencyAmount = 8,
            LimitCount = 1,
        },
        new BontiqueShopItem
        {
            bid = "week:cf-y2cf-rb", Category = BontiqueType.Weekly, Limit = LimitType.Week, RewardKind = RewardType.item,
            gainId = 39, ObtainAmount = 1,
            CurrencyId = 38, CurrencyAmount = 8,
            LimitCount = 1,
        },
        new BontiqueShopItem
        {
            bid = "week:can2gt", Category = BontiqueType.Weekly, Limit = LimitType.Week, RewardKind = RewardType.item,
            gainId = 7, ObtainAmount = 10,
            CurrencyId = 12, CurrencyAmount = 1200,
            LimitCount = 1,
        },
        new BontiqueShopItem
        {
            bid = "week:xp2bw", Category = BontiqueType.Weekly, Limit = LimitType.Week, RewardKind = RewardType.item,
            gainId = 110, ObtainAmount = 1,
            CurrencyId = 11, CurrencyAmount = 750000,
            LimitCount = 2,
        },
        #endregion
        #region Monthly
        new BontiqueShopItem
        {
            bid = "month:can-sup", Category = BontiqueType.Monthly, Limit = LimitType.Month, RewardKind = RewardType.item,
            gainId = 12, ObtainAmount = 500,
            CurrencyId = 11, CurrencyAmount = 1,
            LimitCount = 1,
        },
        new BontiqueShopItem
        {
            bid = "month:can2pt", Category = BontiqueType.Monthly, Limit = LimitType.Month, RewardKind = RewardType.item,
            gainId = 8, ObtainAmount = 1,
            CurrencyId = 12, CurrencyAmount = 2250,
            LimitCount = 1,
        },
        #endregion
        #region Characters
        new BontiqueShopItem
        {
            bid = "char:1000", Category = BontiqueType.Characters, Limit = LimitType.OnlyOnce, RewardKind = RewardType.character,
            gainId = 1000, ObtainAmount = 1,
            CurrencyId = 12, CurrencyAmount = 350,
            LimitCount = 1,
        },
        new BontiqueShopItem
        {
            bid = "char:1001", Category = BontiqueType.Characters, Limit = LimitType.OnlyOnce, RewardKind = RewardType.character,
            gainId = 1001, ObtainAmount = 1,
            CurrencyId = 12, CurrencyAmount = 300,
            LimitCount = 1,
        },
        new BontiqueShopItem
        {
            bid = "char:1002", Category = BontiqueType.Characters, Limit = LimitType.OnlyOnce, RewardKind = RewardType.character,
            gainId = 1002, ObtainAmount = 1,
            CurrencyId = 12, CurrencyAmount = 350,
            LimitCount = 1,
        },
        new BontiqueShopItem
        {
            bid = "char:1003", Category = BontiqueType.Characters, Limit = LimitType.OnlyOnce, RewardKind = RewardType.character,
            gainId = 1003, ObtainAmount = 1,
            CurrencyId = 12, CurrencyAmount = 400,
            LimitCount = 1,
        },
        new BontiqueShopItem
        {
            bid = "char:1004", Category = BontiqueType.Characters, Limit = LimitType.OnlyOnce, RewardKind = RewardType.character,
            gainId = 1004, ObtainAmount = 1,
            CurrencyId = 12, CurrencyAmount = 500,
            LimitCount = 1,
        },
        new BontiqueShopItem
        {
            bid = "char:1005", Category = BontiqueType.Characters, Limit = LimitType.OnlyOnce, RewardKind = RewardType.character,
            gainId = 1005, ObtainAmount = 1,
            CurrencyId = 12, CurrencyAmount = 150,
            LimitCount = 1,
        },
        new BontiqueShopItem
        {
            bid = "char:1006", Category = BontiqueType.Characters, Limit = LimitType.OnlyOnce, RewardKind = RewardType.character,
            gainId = 1006, ObtainAmount = 1,
            CurrencyId = 12, CurrencyAmount = 475,
            LimitCount = 1,
        },
        new BontiqueShopItem
        {
            bid = "char:1007", Category = BontiqueType.Characters, Limit = LimitType.OnlyOnce, RewardKind = RewardType.character,
            gainId = 1007, ObtainAmount = 1,
            CurrencyId = 12, CurrencyAmount = 600,
            LimitCount = 1,
        },
        new BontiqueShopItem
        {
            bid = "char:1008", Category = BontiqueType.Characters, Limit = LimitType.OnlyOnce, RewardKind = RewardType.character,
            gainId = 1008, ObtainAmount = 1,
            CurrencyId = 12, CurrencyAmount = 600,
            LimitCount = 1,
        },
        new BontiqueShopItem
        {
            bid = "char:1009", Category = BontiqueType.Characters, Limit = LimitType.OnlyOnce, RewardKind = RewardType.character,
            gainId = 1009, ObtainAmount = 1,
            CurrencyId = 12, CurrencyAmount = 500,
            LimitCount = 1,
        },
        new BontiqueShopItem
        {
            bid = "char:1010", Category = BontiqueType.Characters, Limit = LimitType.OnlyOnce, RewardKind = RewardType.character,
            gainId = 1010, ObtainAmount = 1,
            CurrencyId = 12, CurrencyAmount = 600,
            LimitCount = 1,
        },
        new BontiqueShopItem
        {
            bid = "char:1011", Category = BontiqueType.Characters, Limit = LimitType.OnlyOnce, RewardKind = RewardType.character,
            gainId = 1011, ObtainAmount = 1,
            CurrencyId = 12, CurrencyAmount = 350,
            LimitCount = 1,
        },
        new BontiqueShopItem
        {
            bid = "char:1012", Category = BontiqueType.Characters, Limit = LimitType.OnlyOnce, RewardKind = RewardType.character,
            gainId = 1012, ObtainAmount = 1,
            CurrencyId = 12, CurrencyAmount = 400,
            LimitCount = 1,
        },
        new BontiqueShopItem
        {
            bid = "char:1013", Category = BontiqueType.Characters, Limit = LimitType.OnlyOnce, RewardKind = RewardType.character,
            gainId = 1013, ObtainAmount = 1,
            CurrencyId = 12, CurrencyAmount = 300,
            LimitCount = 1,
        },
        new BontiqueShopItem
        {
            bid = "char:1014", Category = BontiqueType.Characters, Limit = LimitType.OnlyOnce, RewardKind = RewardType.character,
            gainId = 1014, ObtainAmount = 1,
            CurrencyId = 12, CurrencyAmount = 350,
            LimitCount = 1,
        },
        new BontiqueShopItem
        {
            bid = "char:1400", Category = BontiqueType.Characters, Limit = LimitType.OnlyOnce, RewardKind = RewardType.character,
            gainId = 1400, ObtainAmount = 1,
            CurrencyId = 12, CurrencyAmount = 2400,
            LimitCount = 1,
        },
        #endregion
        #region Event
        //new BontiqueShopItem
        //{
        //    bid = "once:open2026", Category = BontiqueType.Event, Limit = LimitType.Event, RewardKind = RewardType.item,
        //    gainId = 12, ObtainAmount = 2026,
        //    CurrencyId = 11, CurrencyAmount = 1,
        //    LimitCount = 1,
        //    LimitStart = new DateTime(2026, 5, 27), LimitEnd = new DateTime(2026, 6, 14)
        //},
        #endregion
        #region Regular
        new BontiqueShopItem
        {
            bid = "reg:s2f-p", Category = BontiqueType.Supplies, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 34, ObtainAmount = 1,
            CurrencyId = 26, CurrencyAmount = 5,
        },
        new BontiqueShopItem
        {
            bid = "reg:s2f-r", Category = BontiqueType.Supplies, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 35, ObtainAmount = 1,
            CurrencyId = 27, CurrencyAmount = 5,
        },
        new BontiqueShopItem
        {
            bid = "reg:s2f-b", Category = BontiqueType.Supplies, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 36, ObtainAmount = 1,
            CurrencyId = 28, CurrencyAmount = 5,
        },
        new BontiqueShopItem
        {
            bid = "reg:s2f-g", Category = BontiqueType.Supplies, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 37, ObtainAmount = 1,
            CurrencyId = 29, CurrencyAmount = 5,
        },
        new BontiqueShopItem
        {
            bid = "reg:s2f-y", Category = BontiqueType.Supplies, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 38, ObtainAmount = 1,
            CurrencyId = 30, CurrencyAmount = 5,
        },
        new BontiqueShopItem
        {
            bid = "reg:cf-p2xp", Category = BontiqueType.Supplies, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 11, ObtainAmount = 50000,
            CurrencyId = 34, CurrencyAmount = 1,
        },
        new BontiqueShopItem
        {
            bid = "reg:cf-r2xp", Category = BontiqueType.Supplies, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 11, ObtainAmount = 50000,
            CurrencyId = 35, CurrencyAmount = 1,
        },
        new BontiqueShopItem
        {
            bid = "reg:cf-b2xp", Category = BontiqueType.Supplies, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 11, ObtainAmount = 50000,
            CurrencyId = 36, CurrencyAmount = 1,
        },
        new BontiqueShopItem
        {
            bid = "reg:cf-g2xp", Category = BontiqueType.Supplies, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 11, ObtainAmount = 50000,
            CurrencyId = 37, CurrencyAmount = 1,
        },
        new BontiqueShopItem
        {
            bid = "reg:cf-y2xp", Category = BontiqueType.Supplies, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 11, ObtainAmount = 50000,
            CurrencyId = 38, CurrencyAmount = 1,
        },
        new BontiqueShopItem
        {
            bid = "reg:cfs-p2xp", Category = BontiqueType.Supplies, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 11, ObtainAmount = 7500,
            CurrencyId = 26, CurrencyAmount = 1,
        },
        new BontiqueShopItem
        {
            bid = "reg:cfs-r2xp", Category = BontiqueType.Supplies, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 11, ObtainAmount = 7500,
            CurrencyId = 27, CurrencyAmount = 1,
        },
        new BontiqueShopItem
        {
            bid = "reg:cfs-b2xp", Category = BontiqueType.Supplies, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 11, ObtainAmount = 7500,
            CurrencyId = 28, CurrencyAmount = 1,
        },
        new BontiqueShopItem
        {
            bid = "reg:cfs-g2xp", Category = BontiqueType.Supplies, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 11, ObtainAmount = 7500,
            CurrencyId = 29, CurrencyAmount = 1,
        },
        new BontiqueShopItem
        {
            bid = "reg:cfs-y2xp", Category = BontiqueType.Supplies, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 11, ObtainAmount = 7500,
            CurrencyId = 30, CurrencyAmount = 1,
        },
        new BontiqueShopItem
        {
            bid = "reg:np2xp", Category = BontiqueType.Supplies, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 11, ObtainAmount = 250000,
            CurrencyId = 57, CurrencyAmount = 50,
        },
        new BontiqueShopItem
        {
            bid = "reg:xpwaste1", Category = BontiqueType.Supplies, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 11, ObtainAmount = 0,
            CurrencyId = 11, CurrencyAmount = 1,
        },
        new BontiqueShopItem
        {
            bid = "reg:xpwaste10", Category = BontiqueType.Supplies, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 11, ObtainAmount = 0,
            CurrencyId = 11, CurrencyAmount = 10,
        },
        new BontiqueShopItem
        {
            bid = "reg:xpwaste100", Category = BontiqueType.Supplies, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 11, ObtainAmount = 0,
            CurrencyId = 11, CurrencyAmount = 100,
        },
        new BontiqueShopItem
        {
            bid = "reg:xpwaste1000", Category = BontiqueType.Supplies, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 11, ObtainAmount = 0,
            CurrencyId = 11, CurrencyAmount = 1000,
        },
        new BontiqueShopItem
        {
            bid = "reg:xpwaste10000", Category = BontiqueType.Supplies, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 11, ObtainAmount = 0,
            CurrencyId = 11, CurrencyAmount = 10000,
        },
        new BontiqueShopItem
        {
            bid = "reg:xpwaste100000", Category = BontiqueType.Supplies, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 11, ObtainAmount = 0,
            CurrencyId = 11, CurrencyAmount = 100000,
        },
        #endregion
#if UNITY_WEBGL
        #region Builda
        // IAP: RMB → BuildaCoin (99). PayId is the manifest payPoints id (no colon).
        new BontiqueShopItem
        {
            bid = "b:iap-10", PayId = "b-iap-10", Category = BontiqueType.Builda, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 99, ObtainAmount = 10,
            CurrencyId = 99, CurrencyAmount = 1,
        },
        new BontiqueShopItem
        {
            bid = "b:iap-55", PayId = "b-iap-55", Category = BontiqueType.Builda, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 99, ObtainAmount = 55,
            CurrencyId = 99, CurrencyAmount = 5,
        },
        new BontiqueShopItem
        {
            bid = "b:iap-120", PayId = "b-iap-120", Category = BontiqueType.Builda, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 99, ObtainAmount = 120,
            CurrencyId = 99, CurrencyAmount = 10,
        },
        new BontiqueShopItem
        {
            bid = "b:iap-400", PayId = "b-iap-400", Category = BontiqueType.Builda, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 99, ObtainAmount = 400,
            CurrencyId = 99, CurrencyAmount = 30,
        },
        new BontiqueShopItem
        {
            bid = "b:iap-1000", PayId = "b-iap-1000", Category = BontiqueType.Builda, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 99, ObtainAmount = 1000,
            CurrencyId = 99, CurrencyAmount = 68,
        },
        // Spend BuildaCoin (99) for in-game items. Not a host pay SKU.
        new BontiqueShopItem
        {
            bid = "b:can5", Category = BontiqueType.Builda, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 12, ObtainAmount = 5,
            CurrencyId = 99, CurrencyAmount = 5,
        },
        new BontiqueShopItem
        {
            bid = "b:can15", Category = BontiqueType.Builda, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 12, ObtainAmount = 15,
            CurrencyId = 99, CurrencyAmount = 12,
        },
        new BontiqueShopItem
        {
            bid = "b:can50", Category = BontiqueType.Builda, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 12, ObtainAmount = 50,
            CurrencyId = 99, CurrencyAmount = 35,
        },
        new BontiqueShopItem
        {
            bid = "b:gt1", Category = BontiqueType.Builda, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 7, ObtainAmount = 1,
            CurrencyId = 99, CurrencyAmount = 30,
        },
        new BontiqueShopItem
        {
            bid = "b:gt10", Category = BontiqueType.Builda, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 7, ObtainAmount = 10,
            CurrencyId = 99, CurrencyAmount = 250,
        },
        new BontiqueShopItem
        {
            bid = "b:xp1", Category = BontiqueType.Builda, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 11, ObtainAmount = 100000,
            CurrencyId = 99, CurrencyAmount = 10,
        },
        new BontiqueShopItem
        {
            bid = "b:cfs-p", Category = BontiqueType.Builda, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 26, ObtainAmount = 1,
            CurrencyId = 99, CurrencyAmount = 8,
        },
        new BontiqueShopItem
        {
            bid = "b:cfs-r", Category = BontiqueType.Builda, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 27, ObtainAmount = 1,
            CurrencyId = 99, CurrencyAmount = 8,
        },
        new BontiqueShopItem
        {
            bid = "b:cfs-b", Category = BontiqueType.Builda, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 28, ObtainAmount = 1,
            CurrencyId = 99, CurrencyAmount = 8,
        },
        new BontiqueShopItem
        {
            bid = "b:cfs-g", Category = BontiqueType.Builda, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 29, ObtainAmount = 1,
            CurrencyId = 99, CurrencyAmount = 8,
        },
        new BontiqueShopItem
        {
            bid = "b:cfs-y", Category = BontiqueType.Builda, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 30, ObtainAmount = 1,
            CurrencyId = 99, CurrencyAmount = 8,
        },
        new BontiqueShopItem
        {
            bid = "b:cf-p", Category = BontiqueType.Builda, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 34, ObtainAmount = 1,
            CurrencyId = 99, CurrencyAmount = 25,
        },
        new BontiqueShopItem
        {
            bid = "b:cf-r", Category = BontiqueType.Builda, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 35, ObtainAmount = 1,
            CurrencyId = 99, CurrencyAmount = 25,
        },
        new BontiqueShopItem
        {
            bid = "b:cf-b", Category = BontiqueType.Builda, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 36, ObtainAmount = 1,
            CurrencyId = 99, CurrencyAmount = 25,
        },
        new BontiqueShopItem
        {
            bid = "b:cf-g", Category = BontiqueType.Builda, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 37, ObtainAmount = 1,
            CurrencyId = 99, CurrencyAmount = 25,
        },
        new BontiqueShopItem
        {
            bid = "b:cf-y", Category = BontiqueType.Builda, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 38, ObtainAmount = 1,
            CurrencyId = 99, CurrencyAmount = 25,
        },
        new BontiqueShopItem
        {
            bid = "b:cf-rb", Category = BontiqueType.Builda, Limit = LimitType.None, RewardKind = RewardType.item,
            gainId = 39, ObtainAmount = 1,
            CurrencyId = 99, CurrencyAmount = 150,
        },
        #endregion
#endif
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
