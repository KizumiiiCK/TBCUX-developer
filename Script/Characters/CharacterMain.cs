using System;
using System.Collections.Generic;
using UnityEngine;

//[RequireComponent(typeof(BoxCollider2D))]
public abstract partial class Character : MonoBehaviour
{
    /* ====== ֻ���������� ====== */
    public string NameCode { get; private set; }
    public bool IsEliteUnit { get; private set; }
    public float Power { get; private set; } = 1f;

    /* ====== ս������ ====== */
    [Header("Combat")]
    public int Health;
    public int KB;
    public int Speed;
    public int Reload;
    public int DetectionRange;
    public int Cost;
    public int Cooldown;

    /* ====== �������� ====== */
    public List<AttackType> ATKTypes;
    public ATKInfo[] atkInfos;
    public bool areaATK;
    public int atkDuration;
    public bool one_off;

    /* ====== �������� ====== */
    public Traits traits;
    public SubTraits subtraits;
    public Careers career;
    public AgainstCareer againstCareer;
    public DamageRelatedEffect DRE;
    public CharacterEffect[] characterEffects;
    public CharacterAbility[] characterAbilities;
    public AttackTypeResistance[] atkTypeResis;
    public CharacterEffect[] effectResistances;

    /* ====== ����ʱ���� ====== */
    protected int level = 1;
    protected float maxHealth;
    protected float realHealth;
    protected int[] realDamage;
    protected int animateStep = 0;
    protected int realKBtimes;
    protected int realSpeed;
    protected int realReload;
    protected float hardness;
    protected float ATK_muiltipier = 1f;
    protected float MAX_muiltipier = 1f;
    protected float skillfactor = 1f;

    /* ====== ������� ====== */
    [System.Obsolete("不再使用碰撞箱，改用CharacterTargetManager统一管理")]
    protected BoxCollider2D volumeBox;
    [System.Obsolete("不再使用碰撞箱，改用CharacterTargetManager统一管理")]
    protected BoxCollider2D attackBox;
    protected int animatedframes = 0;
    protected bool onKB = false;
    protected bool onATK = false;
    protected bool onCurse = false;
    private bool cancelAttackStartRequested = false;
    private bool undetectableByTargeting = false;
    private bool canTargetUndetectable = false;
    public bool[] effectMarkedSlots = new bool[5];
    protected float startingY = 1;
    protected Coroutine coroutineKB = null;
    public EffectManager EM;



    /* ====== �������ڣ�����ʵ�֣� ====== */
    public abstract void InitializeCharacter();
    public abstract void UpdateAnimation();
    public abstract float GetFactor();

    public void RequestCancelAttackStart() => cancelAttackStartRequested = true;
    public bool ConsumeAttackStartCancelRequest()
    {
        if (!cancelAttackStartRequested) return false;
        cancelAttackStartRequested = false;
        return true;
    }
    public void SetUndetectableByTargeting(bool value) => undetectableByTargeting = value;
    public bool IsUndetectableByTargeting() => undetectableByTargeting;
    public void SetCanTargetUndetectable(bool value) => canTargetUndetectable = value;
    public bool CanTargetUndetectable() => canTargetUndetectable;
}
public static class AnimatorFrameTool
{
    public static int GetCurrentFrame(Animator animator, int layerIndex = 0)
    {
        if (!animator) return 0;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(layerIndex);
        AnimatorClipInfo[] clipInfo = animator.GetCurrentAnimatorClipInfo(layerIndex);

        if (clipInfo.Length == 0) return 0;

        AnimationClip clip = clipInfo[0].clip;
        float normalized = stateInfo.normalizedTime % 1f;   // 0~1 �Ľ���
        int totalFrames = Mathf.RoundToInt(clip.length * clip.frameRate);
        int currentFrame = Mathf.FloorToInt(normalized * totalFrames);

        return currentFrame;
    }
}
