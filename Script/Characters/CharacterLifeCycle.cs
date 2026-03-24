using System.IO;
using Unity.VisualScripting;
using UnityEngine;

public abstract partial class Character
{
    [Header("Animation")]
    public bool UNITYAnimated;
    protected Animator animator;
    protected AnimationDisplayer animatorDisplayer;
    public LevelController levelController;
    public bool BlockAnimationSwitch = false;
    protected int frame_step = 1;

    //private const string CAT_PATH_FMT = "Units/Cat Units/{0}/{1:000}/{2}/data";
    //private const string ENEMY_PATH_FMT = "Units/Enemy Units/{0}/data";

    /* ====== 初始化 ====== */
    private void Awake()
    {
        // 不再需要体积框，改用统一管理器
    }

    private void Start()
    {
        // Base单位也需要注册到管理器（但不会注销）
        bool isBase = name.Contains("Base");
        bool isSpecialUnit = name.Contains("wave") || name.Contains("surge") || name.Contains("cannon");
        
        if (!isBase && !isSpecialUnit)
        {
            if (UNITYAnimated)
            {
                animator = transform.GetChild(1).GetComponent<Animator>();
            }
            else animatorDisplayer = GetComponent<AnimationDisplayer>();
        }
        
        // 注册到统一目标管理器
        if (isSpecialUnit)
        {
            CharacterTargetManager.Instance.RegisterProjectile(this);
        }
        else
        {
            CharacterTargetManager.Instance.RegisterCharacter(this);
        }
        
        realSpeed = Speed;
        realReload = Reload;
        SetAttackRange(0, DetectionRange);
        StartPos();
        EM = GameObject.Find("Effects").GetComponent<EffectManager>();
        InitializeCharacter();
        Passive_OnDeployUnit();
    }
    
    protected virtual void OnDestroy()
    {
        // Base单位不会被注销（全局不可删除）
        // 其他单位正常注销
        bool isBase = name.Contains("Base");
        bool isSpecialUnit = name.Contains("wave") || name.Contains("surge") || name.Contains("cannon");
        
        if (isSpecialUnit)
        {
            CharacterTargetManager.Instance.UnregisterProjectile(this);
        }
        else if (!isBase)
        {
            CharacterTargetManager.Instance.UnregisterCharacter(this);
        }
    }
    
    protected void StartPos()=>startingY = transform.position.y;

    private void FixedUpdate() => realReload++;
    private void Update() {if(Time.timeScale>0.1f) UpdateAnimation();}

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
        if (forceLevel > 1) level = forceLevel;
        IsEliteUnit = data.isEliteUnit;
        Health = (int)(data.Health * treasureBonus);
        KB = data.KB;
        Speed = data.Speed;
        Reload = data.Reload;
        DetectionRange = data.DetectionRange;
        Cost = data.Cost;
        Cooldown = data.Cooldown;
        UNITYAnimated = data.UNITYAnimated;

        ATKTypes = data.ATKType;
        atkInfos = data.atkInfos;
        realDamage = new int[atkInfos.Length];
        for (int i = 0; i < atkInfos.Length; i++) realDamage[i] = (int)(atkInfos[i].ATK * treasureBonus);
        areaATK = data.areaATK;
        atkDuration = data.atkDuration;
        //one_off = data.one_off;

        traits = data.traits;
        traitSpecials = data.traitSpecials;
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
    public void SwitchAnimation(int index) {
        if (BlockAnimationSwitch) return;
        if (UNITYAnimated){ animator.SetInteger("state", index);}
        else { animatorDisplayer.SetMaanimPointer(index); }
    }
    public void SetAnimationSpeed(int spd)
    {
        if (UNITYAnimated) animator.speed = spd;
        else animatorDisplayer.SetAnimationSpeed(spd);
    }
    public void SetFrameStep(int step)=>frame_step = step;
}
