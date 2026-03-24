using UnityEngine;

/// <summary>
/// 角色目标管理器集成示例
/// 展示如何在现有Character系统中集成新的统一管理器
/// </summary>
public static class CharacterTargetManagerIntegration
{
    /// <summary>
    /// 在Character初始化时调用，注册到管理器
    /// 建议在Character的Awake或Start方法中调用
    /// </summary>
    public static void RegisterCharacterToManager(Character character)
    {
        if (character == null) return;
        CharacterTargetManager.Instance.RegisterCharacter(character);
    }

    /// <summary>
    /// 在Character销毁时调用，从管理器注销
    /// 建议在Character的OnDestroy方法中调用
    /// </summary>
    public static void UnregisterCharacterFromManager(Character character)
    {
        if (character == null) return;
        CharacterTargetManager.Instance.UnregisterCharacter(character);
    }
}

/// <summary>
/// Character基类的扩展方法示例
/// 可以添加到Character类中，或作为扩展方法使用
/// </summary>
public static class CharacterExtensions
{
    /// <summary>
    /// 注册角色到目标管理器
    /// </summary>
    public static void RegisterToTargetManager(this Character character)
    {
        CharacterTargetManagerIntegration.RegisterCharacterToManager(character);
    }

    /// <summary>
    /// 从目标管理器注销角色
    /// </summary>
    public static void UnregisterFromTargetManager(this Character character)
    {
        CharacterTargetManagerIntegration.UnregisterCharacterFromManager(character);
    }
}
