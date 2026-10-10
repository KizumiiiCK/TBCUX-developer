using UnityEngine;

/// <summary>
/// 设置页里纯表现类开关的运行时缓存。
/// NoBattleEffects 要在每次特效生成时判定（一场战斗几千次），
/// PlayerPrefs.GetInt 是 native 调用，不能走在那条路上。
/// </summary>
public static class GameOptions
{
    private static bool loaded;
    private static bool skipRepeatPlot;
    private static bool noBattleEffects;
    private static bool noBattleEmotion;
    private static bool noPauseInspect;
    private static bool defaultDoubleSpeed;

    /// <summary>重复挑战已通关的关卡时不播剧情。</summary>
    public static bool SkipRepeatPlot
    {
        get { EnsureLoaded(); return skipRepeatPlot; }
        set { skipRepeatPlot = value; loaded = true; PlayerPrefs.SetInt(UXPref.OptSkipRepeatPlot, value ? 1 : 0); }
    }

    /// <summary>低端机优化：除效果标记外不生成任何战斗特效。</summary>
    public static bool NoBattleEffects
    {
        get { EnsureLoaded(); return noBattleEffects; }
        set { noBattleEffects = value; loaded = true; PlayerPrefs.SetInt(UXPref.OptNoBattleEffects, value ? 1 : 0); }
    }

    /// <summary>不显示战斗表情系统。</summary>
    public static bool NoBattleEmotion
    {
        get { EnsureLoaded(); return noBattleEmotion; }
        set { noBattleEmotion = value; loaded = true; PlayerPrefs.SetInt(UXPref.OptNoBattleEmotion, value ? 1 : 0); }
    }

    /// <summary>禁用暂停时点击单位查看数值。</summary>
    public static bool NoPauseInspect
    {
        get { EnsureLoaded(); return noPauseInspect; }
        set { noPauseInspect = value; loaded = true; PlayerPrefs.SetInt(UXPref.OptNoPauseInspect, value ? 1 : 0); }
    }

    /// <summary>关卡开局和取消暂停后直接进二倍速。</summary>
    public static bool DefaultDoubleSpeed
    {
        get { EnsureLoaded(); return defaultDoubleSpeed; }
        set { defaultDoubleSpeed = value; loaded = true; PlayerPrefs.SetInt(UXPref.OptDefaultDoubleSpeed, value ? 1 : 0); }
    }

    /// <summary>从 PlayerPrefs 重读。进战斗前调一次，兜住本进程里没经过设置页的情况。</summary>
    public static void Refresh()
    {
        skipRepeatPlot = PlayerPrefs.GetInt(UXPref.OptSkipRepeatPlot, 0) != 0;
        noBattleEffects = PlayerPrefs.GetInt(UXPref.OptNoBattleEffects, 0) != 0;
        noBattleEmotion = PlayerPrefs.GetInt(UXPref.OptNoBattleEmotion, 0) != 0;
        noPauseInspect = PlayerPrefs.GetInt(UXPref.OptNoPauseInspect, 0) != 0;
        defaultDoubleSpeed = PlayerPrefs.GetInt(UXPref.OptDefaultDoubleSpeed, 0) != 0;
        loaded = true;
    }

    private static void EnsureLoaded()
    {
        if (!loaded) Refresh();
    }
}
