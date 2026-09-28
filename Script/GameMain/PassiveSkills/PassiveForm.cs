using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Aux_BossWave : PassiveSkill
{
    private static readonly List<Character> catBuffer = new List<Character>(32);

    public static void EnsureOn(Character character)
    {
        if (character == null || character.GetPassive<Aux_BossWave>() != null) return;
        AbilityInstaller.Install(character, new CharacterAbility { name = AbilityName.Aux_BossWave });
    }

    public static void TransferFrom(Character source, Character target)
    {
        if (source == null || target == null) return;
        if (source.GetPassive<Aux_BossWave>() == null) return;
        EnsureOn(target);
        if (target.GetComponent<BossPositionLimit>() == null)
            target.gameObject.AddComponent<BossPositionLimit>();
    }

    public override void OnDeployUnit(Character character)
    {
        if (character == null) return;
        character.StartCoroutine(PlayBossWaveRoutine());
    }

    private IEnumerator PlayBossWaveRoutine()
    {
        yield return new WaitForFixedUpdate();
        GameObject dogeBase = GameObject.Find("DogeBase");
        if (dogeBase != null)
        {
            GameObject shockPrefab = Resources.Load<GameObject>("Effects/boss_shock");
            if (shockPrefab != null)
                UnityEngine.Object.Instantiate(shockPrefab, dogeBase.transform.position, Quaternion.identity);
        }

        CharacterTargetManager mgr = CharacterTargetManager.Instance;
        if (mgr == null) yield break;
        int count = mgr.FillTeamCharacters(true, catBuffer);
        for (int i = 0; i < count; i++)
        {
            Character cat = catBuffer[i];
            if (cat != null) cat.StartKBCoroutine(KB_Type.bossShock);
        }
        catBuffer.Clear();
    }
}

public class ZombieDiveAddon : AnimationDrivingPassive
{
    private const int TransitionFrames = 30;

    private int remainingDiveTimes;
    private bool initialized;

    public override void OnAddingAbility(Character character)
    {
        if (initialized) return;
        initialized = true;
        remainingDiveTimes = probability;
    }

    public override void OnStartAttack(Character character)
    {
        if (character == null || driving) return;
        if (remainingDiveTimes == 0) return;
        if (character.GetExtraAnimIndex(CharacterVisualLoader.ExtraAnim.In) < 0) return;
        if (CanAttackBaseNow(character)) return;

        if (remainingDiveTimes > 0) remainingDiveTimes--;
        character.RequestCancelAttackStart();
        character.StartCoroutine(RunDrive(character, character.GetExtraAnimIndex(CharacterVisualLoader.ExtraAnim.In)));
    }

    public override void OnAfterKB(Character character)
    {
        // 若 KB 期间仍持有控制权，RunDrive 会在下一帧自行释放（finally -> OnDriveEnd）；
        // 这里只需在配额耗尽时移除被动。
        if (remainingDiveTimes == 0 && character != null)
        {
            CharacterTargetManager.Instance.SetCharacterUndetectable(character, false);
            character.RemovePassiveEffect(this);
        }
    }

    protected override IEnumerator DriveRoutine(Character character)
    {
        // In：潜入前摇，原地不动。
        int speedBeforeIn = ResolveTransitExitSpeed(character);
        character.ChangeSpeed(0);
        int t = 0;
        while (t < TransitionFrames && !CanAttackBaseNow(character))
        {
            t += character.GetFrameStep();
            yield return new WaitForFixedUpdate();
        }

        // Diving：潜地推进，不可被检测。
        character.ChangeSpeed(speedBeforeIn);
        CharacterTargetManager.Instance.SetCharacterUndetectable(character, true);
        int diveAnim = character.GetExtraAnimIndex(CharacterVisualLoader.ExtraAnim.Dive);
        SetPhaseAnim(character, diveAnim >= 0 ? diveAnim : character.GetExtraAnimIndex(CharacterVisualLoader.ExtraAnim.In));
        int moveDir = character.IsCat() ? -1 : 1;
        int moveFrames = Mathf.Max(0, duration) * 2;
        for (int i = 0; i < moveFrames; i++)
        {
            if (CanAttackBaseNow(character)) break;
            int currentSpeed = Mathf.Max(0, character.GetRealSpeed());
            character.transform.Translate(new Vector2(character.TBCspeedTranslator(currentSpeed) * moveDir * Time.deltaTime, 0));
            yield return new WaitForFixedUpdate();
        }

        // Out：钻出后摇，恢复可检测。
        CharacterTargetManager.Instance.SetCharacterUndetectable(character, false);
        character.ChangeSpeed(0);
        int outAnim = character.GetExtraAnimIndex(CharacterVisualLoader.ExtraAnim.Out);
        SetPhaseAnim(character, outAnim >= 0 ? outAnim : character.GetExtraAnimIndex(CharacterVisualLoader.ExtraAnim.In));
        t = 0;
        while (t < TransitionFrames)
        {
            t += character.GetFrameStep();
            yield return new WaitForFixedUpdate();
        }
    }

    protected override void OnDriveEnd(Character character)
    {
        if (character == null) return;
        // 无论正常结束还是被 KB 打断，都恢复可检测并还原速度。
        CharacterTargetManager.Instance.SetCharacterUndetectable(character, false);
        character.ChangeSpeed(ResolveTransitExitSpeed(character));
        if (remainingDiveTimes == 0)
        {
            character.RemovePassiveEffect(this);
        }
    }

    private bool CanAttackBaseNow(Character character)
    {
        if (character == null) return false;
        GameObject baseTarget = character.BaseTarget;
        return baseTarget != null && baseTarget.activeInHierarchy;
    }

    private int ResolveTransitExitSpeed(Character character, int fallback = -1)
    {
        if (character == null) return 0;
        if (character.GetComponent<Stop>() != null) return 0;
        if (character.GetComponent<Slow>() != null) return 1;

        int current = character.GetRealSpeed();
        if (current > 0) return current;
        if (fallback >= 0) return fallback;
        return Mathf.Max(0, character.Speed);
    }
}

public class ChangePhase : PassiveSkill
{
    private bool changing;

    public override void OnDead(Character character)
    {
        if (character == null || changing) return;
        Character successor = SpawnNextPhase(character);
        if (successor == null) return;

        changing = true;
        character.SetHealth(1);
        character.MarkSkipDestroyCombatAccounting();
        UnityEngine.Object.Destroy(character.gameObject);
        successor.StartCoroutine(PlayPhaseIntro(successor));
    }

    private Character SpawnNextPhase(Character source)
    {
        UnitIdentity currentIdentity = source.SpawnIdentity;
        if (!currentIdentity.IsValid)
        {
            if (!CharacterPlacer.TryParse(source.NameCode, source.IsCat(), out currentIdentity) || !currentIdentity.IsValid)
                return null;
        }

        int phase = Mathf.Max(1, probability);
        string nextCode = CharacterVisualLoader.BuildPhaseCharacterCode(currentIdentity.CharacterCode, phase);
        UnitIdentity nextIdentity = new UnitIdentity(
            currentIdentity.HostIsCat,
            currentIdentity.AssetIsCat,
            currentIdentity.IsOpposite,
            nextCode);

        CharacterData data = CharacterPlacer.LoadData(nextIdentity);
        if (data == null)
        {
            Debug.LogError($"[ChangePhase] Next phase data not found: {nextCode}");
            return null;
        }

        // 本能挂在角色根目录，不随阶段和 -pN 变，所以变身只要沿用「上一形态叠过本能」这个结论重新叠一次即可。
        // 战斗中不去碰存档：解锁与否在出击时就已经定下来了。
        bool inheritTalent = source.HasTalent;
        if (inheritTalent)
        {
            TalentData talent = TalentData.Load(nextIdentity.CharacterCode);
            if (talent != null) talent.ApplyTo(data);
        }

        if (source.levelController != null)
        {
            if (nextIdentity.HostIsCat)
                LevelRestrictionHelper.ApplyCatCharacterDataRestrictions(source.levelController.LevelRestrictions, data);
            else
                LevelRestrictionHelper.ApplyEnemyCharacterDataRestrictions(source.levelController.LevelRestrictions, data);
        }

        AnimDecryptPack pack = CharacterPlacer.Decrypt(nextIdentity, data);
        float treasureCount = Mathf.Max(0f, (source.TreasureBonus - 1f) * 100f);
        GameObject spawned = CharacterPlacer.Place(
            nextIdentity,
            data,
            pack,
            source.transform.position,
            source.levelController,
            source.CombatLevel,
            treasureCount,
            source.Power,
            source.SpawnUaOrder,
            source.SpawnAdOrder);
        if (spawned == null) return null;

        Character successor = spawned.GetComponent<Character>();
        if (successor == null) return null;

        if (inheritTalent) successor.MarkTalentApplied();

        if (source is EnemyCharacter srcEnemy && successor is EnemyCharacter dstEnemy)
            dstEnemy.strengthenRate = srcEnemy.strengthenRate;

        Aux_BossWave.TransferFrom(source, successor);

        successor.RefreshStartPos();
        return successor;
    }

    private IEnumerator PlayPhaseIntro(Character successor)
    {
        yield return null;
        if (successor == null) yield break;

        int introAnim = successor.GetExtraAnimIndex(CharacterVisualLoader.ExtraAnim.Phase);
        successor.SetExternalAnimControl(true);
        successor.RemoveAllTarget();
        successor.ChangeSpeed(0);
        CharacterTargetManager.Instance.SetCharacterUndetectable(successor, true);
        if (introAnim >= 0) successor.SwitchAnimation(introAnim);

        int frames = Mathf.Max(0, duration);
        for (int i = 0; i < frames; i++)
        {
            if (successor == null) yield break;
            yield return new WaitForFixedUpdate();
        }

        if (successor == null) yield break;
        CharacterTargetManager.Instance.SetCharacterUndetectable(successor, false);
        successor.ChangeSpeed(successor.Speed);
        successor.SetExternalAnimControl(false);
        successor.SwitchAnimation(0);
    }
}

public class ZombieReviveAddon : PassiveSkill
{
    private const int TransitionFrames = 15;
    private const string CorpseEffectName = "corpse";

    private bool initialized;
    private bool reviving;
    private int remainingRevives;
    private bool purified;
    private float reviveHealthBase;

    public override void OnAddingAbility(Character character)
    {
        if (initialized) return;
        initialized = true;
        remainingRevives = probability;
        if (character != null) reviveHealthBase = character.GetMaxHealth();
    }

    public override void OnBeforeTakeDamage(Character character, ref float DMG, List<AttackType> atkTypes)
    {
        if (character == null) return;
        bool hasZombieKiller = atkTypes != null && atkTypes.Contains(AttackType.zombieKiller);
        purified = hasZombieKiller && character.GetHealth() - DMG <= 0f;
        if (purified) character.EM?.InstantiateBattleObject(SEnums.zombieKiller, character.transform.position.x, character.transform.position.y);
    }

    public override void OnDead(Character character)
    {
        if (character == null || reviving) return;
        if (purified)
        {
            purified = false;
            return;
        }
        if (remainingRevives == 0) return;

        if (remainingRevives > 0) remainingRevives--;

        int hpPercent = Mathf.Max(1, intensity);
        float baseHp = reviveHealthBase > 0f ? reviveHealthBase : character.GetMaxHealth();
        int revivedHp = Mathf.Max(1, Mathf.RoundToInt(baseHp * hpPercent / 100f));
        if (revivedHp > character.GetMaxHealth()) character.SetMaxHealth(revivedHp);
        character.SetHealth(revivedHp);
        character.SyncKBStateToHealth();
        character.StartCoroutine(ReviveRoutine(character));
    }

    private IEnumerator ReviveRoutine(Character character)
    {
        if (character == null) yield break;
        reviving = true;
        int originalBaseSpeed = Mathf.Max(0, character.Speed);
        int originalRealSpeed = Mathf.Max(0, character.GetRealSpeed());

        CharacterTargetManager.Instance.SetCharacterUndetectable(character, true);
        SetCharacterRenderersVisible(character, false);
        character.SetExternalAnimControl(true);
        // 尸体阶段强制不可移动，避免被击退后恢复位移。
        character.Speed = 0;
        character.ChangeSpeed(0);
        character.RemoveAllTarget();

        AnimationDisplayer corpse = CreateCorpseOnCharacter(character);
        if (corpse != null)
        {
            corpse.SetMaanimPointer(0);
            yield return WaitFixedFrames(Mathf.Max(0, duration)+ TransitionFrames, character);
            corpse.SetMaanimPointer(1);
            yield return WaitFixedFrames(TransitionFrames, character);
            if (character != null && character.EM != null) character.EM.RecycleBattleObject(corpse, CorpseEffectName);
            else GameObject.Destroy(corpse.gameObject);
        }
        else
        {
            yield return WaitFixedFrames(Mathf.Max(0, duration + TransitionFrames * 2), character);
        }

        if (character == null) yield break;

        SetCharacterRenderersVisible(character, true);
        CharacterTargetManager.Instance.SetCharacterUndetectable(character, false);
        character.Speed = originalBaseSpeed;
        if (character.GetComponent<Stop>() != null) character.ChangeSpeed(0);
        else if (character.GetComponent<Slow>() != null) character.ChangeSpeed(1);
        else character.ChangeSpeed(originalRealSpeed > 0 ? originalRealSpeed : originalBaseSpeed);
        character.SetExternalAnimControl(false);
        character.SwitchAnimation(0);
        reviving = false;
    }

    private IEnumerator WaitFixedFrames(int frameCount, Character character)
    {
        for (int i = 0; i < frameCount; i++)
        {
            if (character == null) yield break;
            yield return new WaitForFixedUpdate();
        }
    }

    private void SetCharacterRenderersVisible(Character character, bool visible)
    {
        if (character == null) return;
        character.transform.GetChild(0).gameObject.SetActive(visible);
    }

    private AnimationDisplayer CreateCorpseOnCharacter(Character character)
    {
        if (character == null || character.EM == null) return null;
        return character.EM.InstantiateAttachedBattleObject(
            CorpseEffectName,
            character.transform.position-new Vector3(0, 0.5f, 0),
            character.transform,
            worldPositionStays: true,
            playSound: false);
    }

}

public class BaseCharacter : PassiveSkill
{
    private const int IdleAnimIndex = 3;
    private const int FixedSortingOrder = 2000;
    private const float FixedSortingYOffset = FixedSortingOrder / 10000f;
    private Coroutine monitorBaseRoutine;
    private Coroutine breakdownRoutine;
    private bool baseDefeatHandled;

    public override void OnAddingAbility(Character character)
    {
        if (character == null) return;
        if (character.IsCat()) return; // 我方先预留空逻辑
        character.SetSkipTargetRegistration(true);
    }

    public override void OnDeployUnit(Character character)
    {
        if (character == null) return;
        if (character.IsCat()) return; // 我方先预留空逻辑

        character.SetSkipTargetRegistration(true);
        character.Speed = 0;
        character.ChangeSpeed(0);
        character.RemoveAllTarget();
        CharacterTargetManager manager = CharacterTargetManager.Instance;
        manager.UnregisterCharacter(character);

        DogeBase dogeBase = UnityEngine.Object.FindObjectOfType<DogeBase>();
        if (dogeBase == null) return;

        SnapToBaseWithFixedSorting(character, dogeBase.transform.position);
        ApplyFixedSortingLayer(character);
        if (dogeBase.transform.childCount > 0)
        {
            dogeBase.transform.GetChild(0).gameObject.SetActive(false);
        }
        manager.RefreshTargetsForCharacter(character);

        if (monitorBaseRoutine != null) character.StopCoroutine(monitorBaseRoutine);
        monitorBaseRoutine = character.StartCoroutine(MonitorBaseDefeat(character, dogeBase, manager));
    }

    public override void OnDead(Character character)
    {
        if (character == null) return;
        if (monitorBaseRoutine != null)
        {
            character.StopCoroutine(monitorBaseRoutine);
            monitorBaseRoutine = null;
        }
        if (breakdownRoutine != null)
        {
            character.StopCoroutine(breakdownRoutine);
            breakdownRoutine = null;
        }
    }

    private IEnumerator MonitorBaseDefeat(Character character, DogeBase dogeBase, CharacterTargetManager manager)
    {
        while (character != null && dogeBase != null && dogeBase.GetHealthPercentage() > 0f)
        {
            SnapToBaseWithFixedSorting(character, dogeBase.transform.position, refreshStartPos: false);
            manager?.RefreshTargetsForCharacter(character);
            yield return new WaitForFixedUpdate();
        }

        monitorBaseRoutine = null;
        if (character == null || baseDefeatHandled) yield break;
        baseDefeatHandled = true;

        character.SetExternalAnimControl(true);
        character.Speed = 0;
        character.ChangeSpeed(0);
        character.RemoveAllTarget();
        character.SwitchAnimation(IdleAnimIndex);

        if (breakdownRoutine != null) character.StopCoroutine(breakdownRoutine);
        breakdownRoutine = character.StartCoroutine(BreakingDown(character));
    }

    private IEnumerator BreakingDown(Character character)
    {
        const float width = 6f;
        const float height = 6f;
        while (character != null && character.EM != null)
        {
            float dx = -UnityEngine.Random.Range(0f, width);
            float dy = UnityEngine.Random.Range(0f, height);
            character.EM.InstantiateBattleObject(SEnums.bite, character.transform.position.x + dx, character.transform.position.y + dy, false);
            yield return new WaitForFixedUpdate();
        }
    }

    private static void SnapToBaseWithFixedSorting(Character character, Vector3 basePos, bool refreshStartPos = true)
    {
        character.transform.position = new Vector3(basePos.x, basePos.y - FixedSortingYOffset, basePos.z);
        if (refreshStartPos) character.RefreshStartPos();
    }

    private static void ApplyFixedSortingLayer(Character character)
    {
        if (character == null) return;
        if (character.UNITYAnimated)
        {
            if (character.SPINEAnimated)
                CharacterVisualLoader.ResetSpineOrderLayer(character.gameObject, "Units", FixedSortingOrder);
            else
                CharacterVisualLoader.ResetAnimationOrderLayer(character.gameObject, "Units", FixedSortingOrder);
            return;
        }

        AnimationDisplayer ad = character.GetComponent<AnimationDisplayer>();
        if (ad != null) CharacterVisualLoader.ResetAnimationOrderLayer(ad, FixedSortingOrder);
    }
}
