using UnityEngine;

/// <summary>
/// 关卡机制接口 - 为未来扩展各种关卡机制提供统一接口
/// 实现此接口的类可以在关卡中作为独立的机制模块运行
/// </summary>
public interface ILevelMechanic
{
    /// <summary>
    /// 初始化机制
    /// </summary>
    /// <param name="levelController">关卡控制器引用</param>
    void Initialize(LevelController levelController);
    
    /// <summary>
    /// 每帧更新
    /// </summary>
    void UpdateMechanic();
    
    /// <summary>
    /// 机制是否激活
    /// </summary>
    bool IsActive { get; }
    
    /// <summary>
    /// 激活机制
    /// </summary>
    void Activate();
    
    /// <summary>
    /// 停用机制
    /// </summary>
    void Deactivate();
    
    /// <summary>
    /// 清理资源
    /// </summary>
    void Cleanup();
}

/// <summary>
/// 关卡事件接口 - 用于处理关卡中的各种事件
/// </summary>
public interface ILevelEventHandler
{
    /// <summary>
    /// 关卡开始事件
    /// </summary>
    void OnLevelStart();
    
    /// <summary>
    /// 关卡胜利事件
    /// </summary>
    void OnLevelVictory();
    
    /// <summary>
    /// 关卡失败事件
    /// </summary>
    void OnLevelFailed();
    
    /// <summary>
    /// 关卡暂停事件
    /// </summary>
    void OnLevelPaused();
    
    /// <summary>
    /// 关卡继续事件
    /// </summary>
    void OnLevelResumed();
}

/// <summary>
/// 金钱系统接口 - 为未来扩展不同的金钱系统提供接口
/// </summary>
public interface IMoneySystem
{
    /// <summary>
    /// 当前金钱
    /// </summary>
    float CurrentMoney { get; }
    
    /// <summary>
    /// 最大金钱
    /// </summary>
    float MaxMoney { get; }
    
    /// <summary>
    /// 增加金钱
    /// </summary>
    void AddMoney(float amount);
    
    /// <summary>
    /// 扣除金钱
    /// </summary>
    bool SpendMoney(float amount);
    
    /// <summary>
    /// 检查是否有足够的金钱
    /// </summary>
    bool CanAfford(float amount);
}

/// <summary>
/// 部署系统接口 - 为未来扩展不同的部署机制提供接口
/// </summary>
public interface IDeploymentSystem
{
    /// <summary>
    /// 是否可以部署
    /// </summary>
    bool CanDeploy();
    
    /// <summary>
    /// 部署单位
    /// </summary>
    bool DeployUnit(string unitCode);
    
    /// <summary>
    /// 移除单位
    /// </summary>
    void RemoveUnit();
    
    /// <summary>
    /// 当前部署数量
    /// </summary>
    int CurrentDeployedCount { get; }
    
    /// <summary>
    /// 最大部署数量
    /// </summary>
    int MaxDeployedCount { get; }
}
