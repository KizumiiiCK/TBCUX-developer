using System;
using System.Collections.Generic;

public abstract partial class Character
{
    private readonly List<PassiveNode> passiveEffects = new List<PassiveNode>();
    private readonly List<PassiveNode> passiveSnapshot = new List<PassiveNode>();
    private bool passiveSnapshotDirty = true;

    public void AddPassiveEffect(PassiveNode effect)
    {
        passiveEffects.Add(effect);
        passiveSnapshotDirty = true;
    }
    public void RemovePassiveEffect(PassiveNode effect)
    {
        passiveEffects.Remove(effect);
        passiveSnapshotDirty = true;
    }

    //
    private void BuildPassiveSnapshotIfNeeded()
    {
        if (!passiveSnapshotDirty) return;
        passiveSnapshot.Clear();
        for (int i = 0; i < passiveEffects.Count; i++)
        {
            passiveSnapshot.Add(passiveEffects[i]);
        }
        passiveSnapshotDirty = false;
    }
    private void InvokePassive(Action<PassiveNode> action)
    {
        BuildPassiveSnapshotIfNeeded();
        for (int i = 0; i < passiveSnapshot.Count; i++)
        {
            action?.Invoke(passiveSnapshot[i]);
        }
    }

    protected void Passive_OnDeployUnit() => InvokePassive(n => n?.OnDeployUnit(this));
    protected void Passive_OnBeforeTakeDamage(ref float dmg, List<AttackType> types)
    {
        BuildPassiveSnapshotIfNeeded();
        for (int i = 0; i < passiveSnapshot.Count; i++)
        {
            passiveSnapshot[i]?.OnBeforeTakeDamage(this, ref dmg, types);
        }
    }
    protected void Passive_OnAfterTakeDamage() => InvokePassive(n => n?.OnAfterTakeDamage(this));
    protected void Passive_OnStartAttack() => InvokePassive(n => n?.OnStartAttack(this));
    protected void Passive_OnFinishAttack() => InvokePassive(n => n?.OnFinishAttack(this));
    protected void Passive_OnAttacking(ref float dmg, ref List<AttackType> types)
    {
        BuildPassiveSnapshotIfNeeded();
        for (int i = 0; i < passiveSnapshot.Count; i++)
        {
            passiveSnapshot[i]?.OnAttacking(this, ref dmg, ref types);
        }
    }
    protected void Passive_OnAfterAttack(float dmg, List<CharacterEffect> ces, List<AttackType> types) => InvokePassive(n => n?.OnAfterAttack(this, dmg, ces, types));
    protected void Passive_OnBeforeKB() => InvokePassive(n => n?.OnBeforeKB(this));
    protected void Passive_OnAfterKB() => InvokePassive(n => n?.OnAfterKB(this));
    protected void Passive_OnDead() => InvokePassive(n => n?.OnDead(this));

}