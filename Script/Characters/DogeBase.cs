using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class DogeBase : EnemyCharacter
{
    protected override TargetRegistrationKind RegistrationKind => TargetRegistrationKind.PersistentBase;

    private Transform main;
    private GameObject NewTurret;
    private AudioSource audioSource;
    private TMP_Text healthInfo;
    private Coroutine shakecoroutine;
    private readonly List<Character> teamDeadBuffer = new List<Character>(128);
    private bool dead = false;
    public bool bossLock = false;
    //public override void Attack(GameObject specific = null) { }
    public override void SetAttackRange(float far, float near) { }
    //public override void GetStrategy() { }
    public override void InitializeCharacter()
    {
        maxHealth = Health;
        realHealth = maxHealth;
        int num_base = PlayerPrefs.GetInt("num_base", 0);
        //Sprite towerBase = Resources.Load<Sprite>($"Units/DogeBases/ec/ec048_en");
        audioSource = GetComponent<AudioSource>();
        main = transform.GetChild(0);
        healthInfo = transform.GetChild(1).GetChild(0).GetComponent<TMP_Text>();
        UpdateHealthInfo();
        //SpriteRenderer renderer_base = main.GetChild(0).GetComponent<SpriteRenderer>();
        //renderer_base.sprite = towerBase;
        BaseResist();
    }
    public override void ReceiveAttack(float DMG, Traits opponentTraits, SubTraits opponentSubtraits, AgainstCareer opponentCE, DamageRelatedEffect dre, List<CharacterEffect> opponentEffect, List<AttackType> atkType)
    {
        if (realHealth <= 0) { realHealth = 0; return; }
        if (DMG < 0) return;
        if (atkType != null) foreach (var ar in atkTypeResis)
        {
            foreach (var at in atkType)
            {
                if (ar.type == at)
                {
                    if (ar.intensity > 99) { return; }
                    else { DMG *= (100 - ar.intensity) / 100f; break; }
                }
            }
        }
        realHealth -= (int)DMG;
        if (realHealth <= 0) 
        {
            if (bossLock)
            {
                StartCoroutine(ReleaseBossLock());
                realHealth = 1;
            }
            else
            {
                Dead();
                realHealth = 0;
                shakecoroutine = StartCoroutine(ShakeTower(atkType, true));
                CharacterTargetManager manager = CharacterTargetManager.Instance;
                int count = manager.FillTeamCharacters(false, teamDeadBuffer, this, includeUndetectable: true);
                for (int i = 0; i < count; i++)
                {
                    EnemyCharacter cc = teamDeadBuffer[i] as EnemyCharacter;
                    if (cc == null) continue;
                    if (cc.HasAbility(AbilityName.BaseCharacter)) continue;
                    cc.Dead();
                }
                teamDeadBuffer.Clear();
            }  
        }
        else if (shakecoroutine == null) shakecoroutine = StartCoroutine(ShakeTower(atkType));
        UpdateHealthInfo();
    }
    public void ApplyLevelBaseHealth(int health)
    {
        Health = health;
        maxHealth = health;
        realHealth = health;
        if (healthInfo != null) UpdateHealthInfo();
    }
    private void UpdateHealthInfo() { healthInfo.text = $"{realHealth} / {maxHealth}"; }
    private IEnumerator ShakeTower(List<AttackType> types, bool permanent = false)
    {
        audioSource.Play();
        HitEffect(types);
        float deviation = 0.05f;
        for (int i = 0; i < 10; i++)
        {
            if (permanent) i = 0;
            float px = Random.Range(-deviation, deviation);
            main.localPosition = new Vector3(px, 0, 0);
            
            yield return new WaitForFixedUpdate();
        }
        main.localPosition = Vector3.zero;
        shakecoroutine = null;
    }
    public override void UpdateAnimation() { }
    public override float GetFactor() { return 0; }
    public float GetHealthPercentage() { return (float)realHealth / maxHealth; }
    public override void Dead() 
    {
        if (dead) return;
        else dead = true;
        GameObject.Find("Level Initializer").GetComponent<LevelController>().Victory();
        StartCoroutine(BreakingDown());
    }
    private IEnumerator ReleaseBossLock()
    {
        yield return new WaitForFixedUpdate();
        bossLock = false;
    }
    private IEnumerator BreakingDown()
    {
        float width = 6f;
        float height = 6f;
        while (true) {
            float dx = -Random.Range(0, width);
            float dy = Random.Range(0, height);
            EM.InstantiateBattleObject(SEnums.bite, dx + transform.position.x, transform.position.y + dy, false);
            yield return new WaitForFixedUpdate();
        }
    }
}
