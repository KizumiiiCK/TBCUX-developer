using System.Collections.Generic;

public class Toxic : E
{
    // 毒伤按自己的攻击类型结算：Metal 靠 AttackType.toxic 豁免，同时 atkTypeResis
    // 里也能单独配毒抗。可以共用一份静态表，因为受击方只读 atkTypes，不改写它
    //（会改写的是攻击方的 OnAttacking，那条路拿的是 ref List）。
    private static readonly List<AttackType> ToxicHitTypes = new List<AttackType> { AttackType.toxic };

    public override void EffectInitializer()
    {
        effectName = EffectName.toxic;
    }

    /// <summary>
    /// 毒伤放在 EffectOperation 而不是 EffectInitializer：后者只在 Start 跑一次，
    /// 而 EffectInstaller.Inflict 对已中毒的目标只复用组件、刷新 duration，
    /// 伤害就再也不会触发。EffectOperation 由 Inflict 每次显式调用（和 Weaken 同一套路），
    /// 所以每一发带毒的攻击都结算一次。
    /// 首次施加时本方法先于 Start 执行、etarget 还是 null，因此这里自己取 Character。
    ///
    /// duration 承载的是「最大生命百分比」而不是时间，且已被 TakeEffects 按效果抗性缩放过，
    /// 直接用即可：每次都按当次的百分比算，不累积。
    /// </summary>
    public override void EffectOperation()
    {
        // duration 是百分比，非正数不结算。抗性表里的 probability 没有 0-100 钳制，
        // 填超过 100 会让 CounterT 变负，负百分比在这里就是「毒把目标治好」，
        // 而毒又穿透 Metal，没有任何后续环节会拦住它。
        if (duration <= 0) return;
        Character c = GetComponent<Character>();
        if (c == null) return;
        if (c.EM != null)
        {
            c.EM.InstantiateBattleObject(
                c.IsCat() ? SEnums.toxic : SEnums.toxic_e,
                c.transform.position.x,
                c.transform.position.y);
        }
        c.ReceiveAttack(c.GetMaxHealth() * duration / 100, null, null, null, null, null, ToxicHitTypes);
    }
}
