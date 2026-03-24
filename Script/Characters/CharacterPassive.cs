using System;
using System.Collections.Generic;
using System.Linq;

public abstract partial class Character
{
    private readonly List<PassiveNode> passiveEffects = new List<PassiveNode>();

    public void AddPassiveEffect(PassiveNode effect) => passiveEffects.Add(effect);
    public void RemovePassiveEffect(PassiveNode effect) => passiveEffects.Remove(effect);

    //
    private void InvokePassive(Action<PassiveNode> action)
    {
        foreach (var ef in passiveEffects.ToArray())
            action?.Invoke(ef);
    }

    protected void Passive_OnDeployUnit() => InvokePassive(n => n?.OnDeployUnit(this));
    protected void Passive_OnBeforeTakeDamage(ref float dmg, List<AttackType> types) { var snapshot = passiveEffects.ToArray(); foreach (var effect in snapshot) { effect?.OnBeforeTakeDamage(this, ref dmg, types); } }
    protected void Passive_OnAfterTakeDamage() => InvokePassive(n => n?.OnAfterTakeDamage(this));
    protected void Passive_OnStartAttack() { var snapshot = passiveEffects.ToArray(); foreach (var effect in snapshot) { effect?.OnStartAttack(this); } }
    protected void Passive_OnFinishAttack() { var snapshot = passiveEffects.ToArray(); foreach (var effect in snapshot) { effect?.OnFinishAttack(this); } }
    protected void Passive_OnAttacking(ref float dmg, ref List<AttackType> types) { var snapshot = passiveEffects.ToArray(); foreach (var effect in snapshot) { effect?.OnAttacking(this, ref dmg, ref types); } }
    protected void Passive_OnAfterAttack(float dmg, List<CharacterEffect> ces, List<AttackType> types) { var snapshot = passiveEffects.ToArray(); foreach (var effect in snapshot) { effect?.OnAfterAttack(this, dmg, ces, types); } }
    protected void Passive_OnBeforeKB() { var snapshot = passiveEffects.ToArray(); foreach (var effect in snapshot) { effect?.OnBeforeKB(this); } }
    protected void Passive_OnAfterKB() { var snapshot = passiveEffects.ToArray(); foreach (var effect in snapshot) { effect?.OnAfterKB(this); } }
    protected void Passive_OnDead() { var snapshot = passiveEffects.ToArray(); foreach (var effect in snapshot) { effect?.OnDead(this); } }

}