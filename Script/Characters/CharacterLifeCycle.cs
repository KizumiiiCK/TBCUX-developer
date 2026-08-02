using Spine.Unity;
using System.IO;
using Unity.VisualScripting;
using UnityEngine;

public abstract partial class Character
{
    protected enum TargetRegistrationKind
    {
        Character,
        Projectile,
        PersistentBase
    }

    [Header("Animation")]
    public bool UNITYAnimated;
    public bool SPINEAnimated;
    protected Animator animator;
    protected AnimationDisplayer animatorDisplayer;
    protected SkeletonAnimation skeletonAnimator;
    public LevelController levelController;
    // 外部动画控制：某个被动技能正在完全接管该角色（自定义动画 + 移动/攻击流程），
    // 此时角色自身的 UpdateAnimation 行为应暂停，但 SwitchAnimation 仍可正常写入
    // （由驱动型被动通过 OnAfterSwitchingAnim 钉住动画号）。取代旧的、易出错的 BlockAnimationSwitch。
    protected bool externalAnimControl = false;
    public void SetExternalAnimControl(bool value) => externalAnimControl = value;
    public bool IsExternalAnimControlled() => externalAnimControl;
    protected int frame_step = 1;

    //private const string CAT_PATH_FMT = "Units/Cat Units/{0}/{1:000}/{2}/data";
    //private const string ENEMY_PATH_FMT = "Units/Enemy Units/{0}/data";

    protected virtual bool ShouldCacheAnimatorOnStart => false;
    protected virtual TargetRegistrationKind RegistrationKind => TargetRegistrationKind.Character;

    /* ====== 初始化 ====== */
    private void Awake()
    {
        // 不再需要体积框，改用统一管理器
    }

    private void Start()
    {
        if (ShouldCacheAnimatorOnStart)
        {
            CacheAnimationComponents();
        }

        RegisterToTargetManager();
        
        realSpeed = Speed;
        realReload = Reload;
        SetAttackRange(-CharacterTargetVolumeLength, DetectionRange);
        StartPos();
        EM = GameObject.Find("Effects").GetComponent<EffectManager>();
        InitializeCharacter();
        Passive_OnDeployUnit();
    }
    
    protected virtual void OnDestroy()
    {
        UnregisterFromTargetManager();
    }
    
    protected void StartPos()=>startingY = transform.position.y;
    public void RefreshStartPos() => StartPos();

    private void FixedUpdate()
    {
        if (Time.timeScale <= 0.1f) return;
        realReload++;
        UpdateAnimation();
    }

    /* ====== 数据加载，统一管理器 ====== */
    public void LoadCharacterData(LevelController lc, CharacterData data, int forceLevel=1, float treasureCount=0)
    {
        levelController = lc;
        if (data == null)
        {
            Debug.LogError($"[Character] Data not found");
            return;
        }
        float treasureBonus = 1 + treasureCount / 100f;

        NameCode = data.Name;
        if (forceLevel >= 1) level = forceLevel;
        IsEliteUnit = data.isEliteUnit;
        BaseEmotion = data.baseEmotion;
        Health = (int)(data.Health * treasureBonus);
        KB = data.KB;
        Speed = data.Speed;
        Reload = data.Reload;
        DetectionRange = data.DetectionRange;
        Cost = data.Cost;
        Cooldown = data.Cooldown;
        UNITYAnimated = data.UNITYAnimated;
        SPINEAnimated= data.SPINEAnimated;

        ATKTypes = data.ATKType;
        atkInfos = BuildRuntimeAttackInfos(data.atkInfos);
        realDamage = new int[atkInfos.Length];
        for (int i = 0; i < atkInfos.Length; i++) realDamage[i] = (int)(atkInfos[i].ATK * treasureBonus);
        areaATK = data.areaATK;
        atkDuration = data.atkDuration;
        //one_off = data.one_off;

        traits = data.traits;
        subtraits = data.subtraits;
        career = data.career;
        againstCareer = data.againstCareer;
        DRE = data.DRE;
        characterEffects = data.characterEffects;
        characterAbilities = data.abilities;
        atkTypeResis = data.atkTypeResis;
        effectResistances = data.effectResistances;

        if (UNITYAnimated) Destroy(GetComponent<AnimationDisplayer>());

        if (IsEliteUnit) AbilityInstaller.Install(this, new CharacterAbility {name=AbilityName.strategic});
        if(!IsCat()&&traits.Mtl) AbilityInstaller.Install(this, new CharacterAbility {name=AbilityName.metal});
        foreach (var ca in characterAbilities) AbilityInstaller.Install(this, ca);
    }

    private static ATKInfo[] BuildRuntimeAttackInfos(ATKInfo[] sourceInfos)
    {
        if (sourceInfos == null || sourceInfos.Length == 0) return new ATKInfo[0];

        ATKInfo[] runtimeInfos = new ATKInfo[sourceInfos.Length];
        for (int i = 0; i < sourceInfos.Length; i++)
        {
            ATKInfo src = sourceInfos[i];
            if (src == null)
            {
                runtimeInfos[i] = new ATKInfo();
                continue;
            }

            Vector2 range = src.ATKRange;
            if (Mathf.Approximately(range.x, 0f))
            {
                range.x = -CharacterTargetVolumeLength;
            }

            runtimeInfos[i] = new ATKInfo
            {
                ATK = src.ATK,
                frame = src.frame,
                DoNotTriggerEffects = src.DoNotTriggerEffects,
                DoNotTriggerAbilities = src.DoNotTriggerAbilities,
                Friendly = src.Friendly,
                ATKRange = range
            };
        }

        return runtimeInfos;
    }
    private int current_animation_index = 999;
    public void SwitchAnimation(int index) {
        // 被动技能可以把请求的动画号改写成自己的阶段动画（如遁地、practician），
        // 从而无需再用 BlockAnimationSwitch 关闭/打开来打断原动画更新。KB/死亡等动画会被放行。
        index = Passive_OnAfterSwitchingAnim(index);
        if (current_animation_index == index) return;
        current_animation_index = index;
        if (UNITYAnimated){ animator.SetInteger("state", index);}
        else { animatorDisplayer.SetMaanimPointer(index); }
    }
    public void SetAnimationSpeed(int spd)
    {
        if (UNITYAnimated)
        {
            animator.speed = spd;
            if (SPINEAnimated)
            {
                skeletonAnimator.timeScale = spd;
            }
        }
        else animatorDisplayer.SetAnimationSpeed(spd);
    }
    public void SetFrameStep(int step)=>frame_step = step;
    public int GetFrameStep()=>frame_step;

    private void CacheAnimationComponents()
    {
        if (UNITYAnimated)
        {
            animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (SPINEAnimated) skeletonAnimator = GetComponentInChildren<SkeletonAnimation>();
        }
        else
        {
            animatorDisplayer = GetComponent<AnimationDisplayer>();
        }
    }

    private void RegisterToTargetManager()
    {
        if (ShouldSkipTargetRegistration()) return;
        switch (RegistrationKind)
        {
            case TargetRegistrationKind.Projectile:
                CharacterTargetManager.Instance.RegisterProjectile(this);
                break;
            case TargetRegistrationKind.Character:
            case TargetRegistrationKind.PersistentBase:
                CharacterTargetManager.Instance.RegisterCharacter(this);
                break;
        }
    }

    private void UnregisterFromTargetManager()
    {
        switch (RegistrationKind)
        {
            case TargetRegistrationKind.Projectile:
                CharacterTargetManager.Instance.UnregisterProjectile(this);
                break;
            case TargetRegistrationKind.Character:
                CharacterTargetManager.Instance.UnregisterCharacter(this);
                break;
            case TargetRegistrationKind.PersistentBase:
                break;
        }
    }
}
