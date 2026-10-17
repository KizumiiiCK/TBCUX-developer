using UnityEngine;

/// <summary>
/// 职业克制的全部可调参数。<b>要调数值只改这个类，别去改 CareerCounter 的逻辑。</b>
/// <para>
/// 设计意图：属性克制是单挑型的（超伤/超防/debuff 一击定性），职业克制是<b>集火型</b>的——
/// 一个克制者只能压出一点点，人越多压得越狠。所以这里不按「打了几下」算，而是按
/// 「<b>有几个不同的克制者在持续打我</b>」算，再把结果换成一个<b>占空比上限</b>。
/// </para>
///
/// 三层模型：
/// <list type="number">
/// <item><b>压力 P（0~1）</b>：每个<i>不同的</i>克制者每 <see cref="ShareWindowFrames"/> 帧
/// 最多贡献一次 <c>P += PressureGain * (1 - P)</c>，同时 P 以 <c>1/RecoverFrames</c> 的速度衰减。
/// 同一个攻击者打得再快也只算一个人，这是「人数轴」能成立的唯一办法。
/// N 个克制者持续集火的稳态：<c>P* = 1 - ShareWindowFrames / (RecoverFrames * N * PressureGain)</c>。</item>
///
/// <item><b>占空比 duty</b>：<c>duty = DutyMin + (DutyMax - DutyMin) * P</c>。
/// 每次命中按「距上次职业克制命中的间隔 × duty × 该职业的 DutyScale」往预算里加帧数，
/// 于是长时间看<b>效果覆盖率 ≈ duty × DutyScale，与攻速无关</b>——攻速快的人单次给得少、给得密，
/// 总量一样。这就是旧版「每打一下 +20 帧且永不衰减、快攻速必然永久锁死」的根治办法。</item>
///
/// <item><b>分块释放</b>：预算攒够 MinChunk 才放一段，放完等这一段结束再放下一段。
/// 停止（stop）会把 animator speed / frame_step 归零再恢复，碎片化地反复开关会让动画抽搐，
/// 所以必须成块释放；MinChunk 还跟攻击者的攻击周期挂钩，保证一段至少有半个攻击周期的意义。</item>
/// </list>
///
/// 参考稳态（默认参数，PressureGain=0.5 / ShareWindow=60 / Recover=150）：
/// 1 人 P≈0.20 duty≈0.27；2 人 P≈0.60 duty≈0.51；3 人 P≈0.73 duty≈0.59；4 人 P≈0.80 duty≈0.63；
/// 5 人以上封顶 duty→0.75。再乘各职业的 DutyScale（stop 只有 0.5，所以 4 人集火也只有约 31% 的时间被定住）。
/// </summary>
public static class CareerCounterConfig
{
    /* ====== 压力层（人数轴） ====== */
    /// <summary>每个新克制者的压力增量系数：<c>P += PressureGain * (1 - P)</c>。大 = 人数的边际收益更高。</summary>
    public static float PressureGain = 0.5f;
    /// <summary>同一个攻击者的压力贡献冷却（帧）。大 = 攻速快的单打独斗更吃亏，人数的权重更高。30 帧 = 1 秒。</summary>
    public static int ShareWindowFrames = 60;
    /// <summary>压力从 1 衰减到 0 所需帧数。大 = 集火的余温更长，短暂走开也还压得住。</summary>
    public static float RecoverFrames = 150f;

    /* ====== 占空比层（效果覆盖率） ====== */
    /// <summary>P=0 时的效果覆盖率下限。单人持续打（P≈0.2）拿到的是 DutyMin + 0.2*(DutyMax-DutyMin)。</summary>
    public static float DutyMin = 0.15f;
    /// <summary>P=1 时的效果覆盖率上限，也就是<b>整套职业克制的硬天花板</b>（再乘职业 DutyScale）。</summary>
    public static float DutyMax = 0.75f;

    /* ====== 计量与分块 ====== */
    /// <summary>单次命中计入预算的间隔下限（帧）。防止多段攻击/弹幕刷出一堆「零间隔」的免费预算。</summary>
    public static int IntervalMinFrames = 6;
    /// <summary>单次命中计入预算的间隔上限（帧）。慢攻速单位一下给的预算不会超过这个数。</summary>
    public static int IntervalMaxFrames = 120;
    /// <summary>超过这个帧数没再吃到职业克制，就视为脱战：压力、预算、攻击者表全部清空。</summary>
    public static int GapResetFrames = 180;
    /// <summary>MinChunk 取攻击者攻击周期（atkDuration + Reload）的这个比例。</summary>
    public static float MinChunkCycleRatio = 0.5f;
    /// <summary>MinChunk 的上限（帧）。攻击周期特别长的单位不会因此憋出一个超长块。</summary>
    public static int MinChunkCeilFrames = 20;

    /* ====== 伤害 ====== */
    /// <summary>职业克制成立时的伤害倍率（和旧版一致，职业克制的伤害部分不受占空比影响）。</summary>
    public static float DamageMultiplier = 1.35f;

    /* ====== Supporter 的额外击退 ====== */
    /// <summary>
    /// true：只在死亡标记那一块释放的瞬间击退一次（和占空比同步）。
    /// false：回到旧版行为，每次命中都击退——那等于无视占空比的硬控，慎用。
    /// </summary>
    public static bool SupporterKnockbackOnChunkOnly = true;
    /// <summary>Supporter 击退的力量比例，1 = 一个标准击退距离（240）。</summary>
    public static float SupporterKnockbackPower = 1f;

    /* ====== 每个职业的表 ====== */
    /// <summary>
    /// 按职业分别配：挂什么效果、分到多少占空比、一块多长、最多能攒多少。
    /// DutyScale 是这个职业相对 duty 的折扣——效果越硬（stop）给得越少。
    /// </summary>
    public static readonly CareerCounterEntry[] Entries =
    {
        // Warrior：削弱攻击力，最温和，给满占空比。
        new CareerCounterEntry
        {
            Effects = new[] { EffectName.weaken },
            DutyScale = 1.0f, Intensity = 50,
            MinChunkFrames = 10, MaxChunkFrames = 60, BudgetCapFrames = 90,
        },
        // Deffender：停止。唯一的完全硬控，占空比打五折，单块也短，避免动画抽搐。
        new CareerCounterEntry
        {
            Effects = new[] { EffectName.stop },
            DutyScale = 0.5f, Intensity = 0,
            MinChunkFrames = 15, MaxChunkFrames = 30, BudgetCapFrames = 45,
        },
        // Magician：减速。
        new CareerCounterEntry
        {
            Effects = new[] { EffectName.slow },
            DutyScale = 0.9f, Intensity = 0,
            MinChunkFrames = 10, MaxChunkFrames = 60, BudgetCapFrames = 90,
        },
        // Supporter：死亡标记（伤害分摊），另见 SupporterKnockbackOnChunkOnly。
        new CareerCounterEntry
        {
            Effects = new[] { EffectName.deathmark },
            DutyScale = 1.0f, Intensity = 50,
            MinChunkFrames = 12, MaxChunkFrames = 60, BudgetCapFrames = 90,
        },
        // Practician：裂伤 + 减速，两个效果共用同一块时长，所以占空比折一点。
        new CareerCounterEntry
        {
            Effects = new[] { EffectName.lacerate, EffectName.slow },
            DutyScale = 0.8f, Intensity = 50,
            MinChunkFrames = 12, MaxChunkFrames = 45, BudgetCapFrames = 70,
        },
    };

    /// <summary>
    /// 固定步帧号。效果时长是在 <c>E.FixedUpdate</c> 里 <c>duration--</c> 的，所以这里必须用固定步，
    /// 不能用 <c>Time.frameCount</c>（那是渲染帧，会随帧率漂移）。
    /// </summary>
    public static int CurrentFrame => Mathf.RoundToInt(Time.fixedTime / Time.fixedDeltaTime);
}

/// <summary>单个职业的克制配置，见 <see cref="CareerCounterConfig.Entries"/>。</summary>
public class CareerCounterEntry
{
    /// <summary>这个职业被克制时挂上的效果，按顺序全挂、共用同一段时长。</summary>
    public EffectName[] Effects;
    /// <summary>占空比折扣：实际覆盖率 ≈ duty * DutyScale。</summary>
    public float DutyScale = 1f;
    /// <summary>传给 EffectInstaller 的 intensity（weaken 是攻击力百分比，其余效果不读）。</summary>
    public int Intensity = 50;
    /// <summary>一块的最短帧数，低于它就继续攒，免得碎片化开关。</summary>
    public int MinChunkFrames = 10;
    /// <summary>一块的最长帧数，预算再多也要分几次放。</summary>
    public int MaxChunkFrames = 60;
    /// <summary>预算上限，攒满就不再增长（溢出的压力直接浪费掉，这是天花板的一部分）。</summary>
    public int BudgetCapFrames = 90;
}

/// <summary>
/// 一只单位的职业克制受击状态机。每只单位第一次真的吃到职业克制时才会 new 出来（见
/// <c>Character.DMG_CarrerEffects</c>），没被克制过的单位一分内存都不占。
/// </summary>
public class CareerCounter
{
    /// <summary>同时参与压力计算的攻击者上限。压力在 5~6 人就接近饱和，12 格足够且无 GC。</summary>
    private const int AttackerSlots = 12;
    private const int EmptySlot = 0;
    /// <summary>attacker == null（毒伤这类无来源伤害）用的匿名身份。</summary>
    private const int AnonymousAttackerId = int.MinValue;

    private readonly int[] attackerIds = new int[AttackerSlots];
    private readonly int[] attackerFrames = new int[AttackerSlots];
    private readonly float[] budget = new float[CareerCounterConfig.Entries.Length];
    private readonly int[] chunkEndFrame = new int[CareerCounterConfig.Entries.Length];

    private float pressure;
    private int lastHitFrame = int.MinValue;
    private int lastPressureFrame;

    /// <summary>当前压力，0~1。给调试/UI 看，逻辑里不要外部写。</summary>
    public float Pressure => pressure;

    /// <summary>
    /// 受击方的 career 和攻击方的 againstCareer 的交集，按 <see cref="CareerCounterConfig.Entries"/>
    /// 的下标做成位掩码。0 表示不构成职业克制——Character 靠它决定要不要创建状态机。
    /// </summary>
    public static int MatchMask(Careers career, AgainstCareer opponentAC)
    {
        if (career == null || opponentAC == null) return 0;
        int mask = 0;
        if (career.Warrior && opponentAC.AggainstWarrior) mask |= 1 << 0;
        if (career.Deffender && opponentAC.AggainstDeffender) mask |= 1 << 1;
        if (career.Magician && opponentAC.AggainstMagician) mask |= 1 << 2;
        if (career.Supporter && opponentAC.AggainstSupporter) mask |= 1 << 3;
        if (career.Practician && opponentAC.AggainstPractician) mask |= 1 << 4;
        return mask;
    }

    /// <summary>
    /// 结算一次职业克制命中：抬伤害、更新压力、往预算里加帧数、够一块就挂效果。
    /// </summary>
    /// <param name="victim">被克制的单位（效果挂在它身上）。</param>
    /// <param name="attacker">
    /// 这一击的来源。只用来做「算不算一个新的人」的身份判定，<b>不解引用跨帧保存</b>，
    /// 所以攻击者当帧死亡也没有影响。为 null 时按同一个匿名攻击者处理。
    /// </param>
    /// <param name="matchMask"><see cref="MatchMask"/> 的结果，必须非 0。</param>
    public void Apply(Character victim, Character attacker, int matchMask, ref float DMG)
    {
        if (victim == null || matchMask == 0) return;

        DMG *= CareerCounterConfig.DamageMultiplier;

        int now = CareerCounterConfig.CurrentFrame;
        int interval;
        if (lastHitFrame == int.MinValue || now - lastHitFrame >= CareerCounterConfig.GapResetFrames)
        {
            // 脱战重来：这一击只按最小间隔计，重新集火不会一上来就续上之前的压制。
            ResetEngagement(now);
            interval = CareerCounterConfig.IntervalMinFrames;
        }
        else
        {
            interval = Mathf.Clamp(now - lastHitFrame,
                CareerCounterConfig.IntervalMinFrames, CareerCounterConfig.IntervalMaxFrames);
        }
        lastHitFrame = now;

        UpdatePressure(attacker, now);

        float duty = Mathf.Clamp01(CareerCounterConfig.DutyMin
            + (CareerCounterConfig.DutyMax - CareerCounterConfig.DutyMin) * pressure);
        int minChunkFromCycle = MinChunkFromAttackCycle(attacker);

        CareerCounterEntry[] entries = CareerCounterConfig.Entries;
        for (int slot = 0; slot < entries.Length; slot++)
        {
            if ((matchMask & (1 << slot)) == 0) continue;
            CareerCounterEntry entry = entries[slot];
            if (entry == null) continue;

            budget[slot] = Mathf.Min(budget[slot] + interval * duty * entry.DutyScale, entry.BudgetCapFrames);

            // 上一块还在生效：继续攒，不要用新的时长去覆盖它（Inflict 是覆盖而不是取较大值）。
            if (now < chunkEndFrame[slot]) continue;

            int minChunk = Mathf.Max(entry.MinChunkFrames, minChunkFromCycle);
            if (budget[slot] < minChunk) continue;

            int chunk = Mathf.Clamp(Mathf.FloorToInt(budget[slot]), minChunk, entry.MaxChunkFrames);
            budget[slot] -= chunk;
            chunkEndFrame[slot] = now + chunk;
            ReleaseChunk(victim, slot, entry, chunk);
        }
    }

    private void ReleaseChunk(Character victim, int slot, CareerCounterEntry entry, int chunk)
    {
        if (entry.Effects != null)
        {
            for (int i = 0; i < entry.Effects.Length; i++)
                EffectInstaller.Inflict(victim.gameObject, entry.Effects[i], chunk, entry.Intensity);
        }
        // Supporter（slot 3）的附带击退。旧版是每次命中都退，等于绕过占空比的硬控。
        if (slot == 3 && CareerCounterConfig.SupporterKnockbackOnChunkOnly)
            EffectInstaller.Inflict(victim.gameObject, EffectName.knockback, CareerCounterConfig.SupporterKnockbackPower, 1);
    }

    /// <summary>每次命中都会调用：先按经过的帧数衰减，再看这个攻击者算不算一次新的人头。</summary>
    private void UpdatePressure(Character attacker, int now)
    {
        float recover = Mathf.Max(1f, CareerCounterConfig.RecoverFrames);
        pressure = Mathf.Max(0f, pressure - (now - lastPressureFrame) / recover);
        lastPressureFrame = now;

        if (!TryClaimAttacker(attacker, now)) return;
        pressure = Mathf.Clamp01(pressure + CareerCounterConfig.PressureGain * (1f - pressure));
    }

    /// <summary>
    /// 这个攻击者本窗口内是否还能贡献压力。定长数组 + 线性查找：无 GC、人数天然有上限，
    /// 满了就顶掉最久没出现的那一格。
    /// </summary>
    private bool TryClaimAttacker(Character attacker, int now)
    {
        int id = attacker != null ? attacker.GetInstanceID() : AnonymousAttackerId;
        int oldestSlot = 0;
        int oldestFrame = int.MaxValue;
        for (int i = 0; i < AttackerSlots; i++)
        {
            if (attackerIds[i] == id)
            {
                if (now - attackerFrames[i] < CareerCounterConfig.ShareWindowFrames) return false;
                attackerFrames[i] = now;
                return true;
            }
            if (attackerIds[i] == EmptySlot)
            {
                attackerIds[i] = id;
                attackerFrames[i] = now;
                return true;
            }
            if (attackerFrames[i] < oldestFrame)
            {
                oldestFrame = attackerFrames[i];
                oldestSlot = i;
            }
        }
        attackerIds[oldestSlot] = id;
        attackerFrames[oldestSlot] = now;
        return true;
    }

    /// <summary>一块至少要有攻击者半个攻击周期那么长，否则「被压制」在体感上根本来不及成立。</summary>
    private static int MinChunkFromAttackCycle(Character attacker)
    {
        if (attacker == null) return 0;
        int cycle = attacker.atkDuration + attacker.Reload;
        if (cycle <= 0) return 0;
        return Mathf.Min(CareerCounterConfig.MinChunkCeilFrames,
            Mathf.RoundToInt(cycle * CareerCounterConfig.MinChunkCycleRatio));
    }

    private void ResetEngagement(int now)
    {
        pressure = 0f;
        lastPressureFrame = now;
        for (int i = 0; i < AttackerSlots; i++)
        {
            attackerIds[i] = EmptySlot;
            attackerFrames[i] = 0;
        }
        for (int i = 0; i < budget.Length; i++)
        {
            budget[i] = 0f;
            chunkEndFrame[i] = 0;
        }
    }
}
