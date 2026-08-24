using Spine.Unity;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 角色视觉资源加载与动画管理。附加动画在 ExtraAnims 登记名字即可，能力用 GetExtraAnimIndex(key) 取号。
/// </summary>
public static class CharacterVisualLoader
{
    public const int BaseAnimCount = 4;

    public static class ExtraAnim
    {
        public const string P = "p";
        public const string In = "in";
        public const string Dive = "dive";
        public const string Out = "out";
    }

    private readonly struct ExtraAnimEntry
    {
        public readonly string Key;
        public readonly AbilityName UaAbility;

        public ExtraAnimEntry(string key, AbilityName uaAbility)
        {
            Key = key;
            UaAbility = uaAbility;
        }
    }

    /// <summary>
    /// 新附加动画加在末尾：key + UA 无 maanim 文件时用来占 state 的能力。
    /// </summary>
    private static readonly ExtraAnimEntry[] ExtraAnims =
    {
        new ExtraAnimEntry(ExtraAnim.P, AbilityName.practician),
        new ExtraAnimEntry(ExtraAnim.In, AbilityName.ZombieDive),
        new ExtraAnimEntry(ExtraAnim.Dive, AbilityName.ZombieDive),
        new ExtraAnimEntry(ExtraAnim.Out, AbilityName.ZombieDive),
    };

    public static CharacterData LoadCharacterData(bool cat, string characterCode)
    {
        string loadPath = GetCharacterLoadPath(cat, characterCode);
        CharacterData data = LoadAsset<CharacterData>(loadPath + "data");
        return data != null ? data.Clone() : null;
    }

    public static AnimDecryptPack DecryptCharacterFiles(bool cat, string characterCode, CharacterData data)
    {
        if (data == null || data.UNITYAnimated) return null;

        string loadPath = GetCharacterLoadPath(cat, characterCode);
        Texture2D unitTexture = LoadAsset<Texture2D>(loadPath + "sprite");
        TextAsset imagecut = LoadAsset<TextAsset>(loadPath + "imgcut");
        TextAsset mamodel = LoadAsset<TextAsset>(loadPath + "mamodel");
        TextAsset maanimWalk = LoadAsset<TextAsset>(loadPath + "maanim_walk");
        TextAsset maanimIdle = LoadAsset<TextAsset>(loadPath + "maanim_idle");
        TextAsset maanimAttack = LoadAsset<TextAsset>(loadPath + "maanim_attack");
        TextAsset maanimKb = LoadAsset<TextAsset>(loadPath + "maanim_kb");

        List<TextAsset> maanims = new List<TextAsset> { maanimWalk, maanimIdle, maanimAttack, maanimKb };
        Dictionary<string, int> extraIndices = BuildExtraAnimIndices(loadPath, maanims, data);

        AnimEncryptPack pack = new AnimEncryptPack(unitTexture, imagecut, mamodel, maanims.ToArray());
        AnimDecryptPack decrypted = AnimFileDecrypter.DecryptEncryptPack(pack);
        if (decrypted != null) decrypted.ExtraAnimIndices = extraIndices;
        return decrypted;
    }

    public static void InitializeRuntimeCharacterVisual(
        GameObject runtimeCharacter,
        bool cat,
        string characterCode,
        CharacterData data,
        AnimDecryptPack decryptedPack,
        string sortingLayer,
        int uaOrder,
        int adOrder)
    {
        if (runtimeCharacter == null || data == null) return;

        if (data.UNITYAnimated)
        {
            GameObject uaPrefab = LoadPrefab(GetUaUnitPath(cat, characterCode));
            if (uaPrefab == null) return;
            GameObject uaunit = Object.Instantiate(uaPrefab, runtimeCharacter.transform.position, Quaternion.identity);
            uaunit.transform.SetParent(runtimeCharacter.transform);
            if (data.SPINEAnimated) ResetSpineOrderLayer(uaunit, sortingLayer, uaOrder);
            else ResetAnimationOrderLayer(uaunit, sortingLayer, uaOrder);
            Animator animator = uaunit.GetComponent<Animator>();
            if (animator != null)
            {
                animator.SetInteger("state", 0);
                animator.speed = 1f;
            }
            ApplyExtraAnimIndices(runtimeCharacter, BuildExtraAnimIndices(GetCharacterLoadPath(cat, characterCode), null, data));
            return;
        }

        if (decryptedPack == null)
        {
            decryptedPack = DecryptCharacterFiles(cat, characterCode, data);
        }
        AnimationDisplayer ad = runtimeCharacter.GetComponent<AnimationDisplayer>();
        if (ad == null || decryptedPack == null) return;
        ad.Initialization(decryptedPack);
        ResetAnimationOrderLayer(ad, adOrder);
        ApplyExtraAnimIndices(runtimeCharacter, decryptedPack.ExtraAnimIndices);
    }

    public static void SwitchAnimation(GameObject character, bool ua, int animationNum)
    {
        if (character == null) return;
        if (ua)
        {
            Animator animator = character.GetComponent<Animator>();
            if (animator != null) animator.SetInteger("state", animationNum);
            return;
        }
        AnimationDisplayer displayer = character.GetComponent<AnimationDisplayer>();
        if (displayer != null) displayer.PlayAnimation(animationNum);
    }

    /// <summary>
    /// 可播放段数：基础 0-3 + 实际附加段。UA 与 BCU 使用同一套连续编号。
    /// </summary>
    public static int GetPlayableAnimCount(GameObject character, bool ua, bool cat, string characterCode, CharacterData data)
    {
        if (!ua && character != null)
        {
            AnimationDisplayer ad = character.GetComponent<AnimationDisplayer>();
            if (ad != null && ad.AnimationTotalFrame != null && ad.AnimationTotalFrame.Length > 0)
            {
                return ad.AnimationTotalFrame.Length;
            }
        }

        int extra = 0;
        string loadPath = GetCharacterLoadPath(cat, characterCode);
        for (int i = 0; i < ExtraAnims.Length; i++)
        {
            ExtraAnimEntry entry = ExtraAnims[i];
            if (LoadAsset<TextAsset>(loadPath + "maanim_" + entry.Key) != null) extra++;
            else if (ua && DataHasAbility(data, entry.UaAbility)) extra++;
        }
        return BaseAnimCount + extra;
    }

    public static void ResetAnimationOrderLayer(AnimationDisplayer ad, int order)
    {
        if (ad == null) return;
        ad.OrderLayerStart = order;
        ad.ResetModelOrderLayer();
    }

    public static void ResetSpineOrderLayer(GameObject go, string sortingLayer, int order)
    {
        if (go == null) return;
        SkeletonAnimation[] skeletonAnimations = go.GetComponentsInChildren<SkeletonAnimation>(true);
        for (int i = 0; i < skeletonAnimations.Length; i++)
        {
            SkeletonAnimation skeleton = skeletonAnimations[i];
            if (skeleton == null) continue;
            MeshRenderer mr = skeleton.GetComponent<MeshRenderer>();
            if (mr == null) continue;
            mr.sortingLayerName = sortingLayer;
            mr.sortingOrder = order;
        }

        MeshRenderer[] renderers = go.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer mr = renderers[i];
            if (mr == null) continue;
            mr.sortingLayerName = sortingLayer;
            mr.sortingOrder = order;
        }
    }

    public static void ResetAnimationOrderLayer(GameObject go, string sortingLayer, int order)
    {
        if (go == null) return;
        if (go.TryGetComponent(out SpriteRenderer sr))
        {
            sr.sortingLayerName = sortingLayer;
            sr.sortingOrder = order;
        }
        foreach (Transform child in go.transform) ResetAnimationOrderLayer(child.gameObject, sortingLayer, order);
    }

    public static string GetCharacterLoadPath(bool cat, string characterCode)
    {
        return cat
            ? $"Units/Cat Units/{characterCode[0]}/{characterCode.Substring(1, 3)}/{characterCode[4]}/"
            : $"Units/Enemy Units/{characterCode}/";
    }

    public static string GetUaUnitPath(bool cat, string characterCode)
    {
        return cat
            ? $"Units/Cat Units/{characterCode[0]}/{characterCode.Substring(1, 3)}/{characterCode[4]}/uaunit"
            : $"Units/Enemy Units/{characterCode}/uaunit";
    }

    public static GameObject LoadPrefab(string address)
    {
        return LoadAsset<GameObject>(address);
    }

    private static Dictionary<string, int> BuildExtraAnimIndices(string loadPath, List<TextAsset> maanims, CharacterData data)
    {
        Dictionary<string, int> map = new Dictionary<string, int>();
        int index = maanims != null ? maanims.Count : BaseAnimCount;
        for (int i = 0; i < ExtraAnims.Length; i++)
        {
            ExtraAnimEntry entry = ExtraAnims[i];
            TextAsset clip = LoadAsset<TextAsset>(loadPath + "maanim_" + entry.Key);
            if (clip != null)
            {
                if (maanims != null) maanims.Add(clip);
                map[entry.Key] = index++;
                continue;
            }
            if (maanims == null && DataHasAbility(data, entry.UaAbility))
            {
                map[entry.Key] = index++;
            }
        }
        return map;
    }

    private static bool DataHasAbility(CharacterData data, AbilityName abilityName)
    {
        if (data == null || data.abilities == null) return false;
        for (int i = 0; i < data.abilities.Length; i++)
        {
            CharacterAbility ability = data.abilities[i];
            if (ability != null && ability.name == abilityName) return true;
        }
        return false;
    }

    private static void ApplyExtraAnimIndices(GameObject runtimeCharacter, Dictionary<string, int> map)
    {
        if (runtimeCharacter == null || map == null || map.Count == 0) return;
        Character character = runtimeCharacter.GetComponent<Character>();
        if (character != null) character.SetExtraAnimIndices(map);
    }

    private static T LoadAsset<T>(string address) where T : Object
    {
        return BundledAddressables.LoadSync<T>(address);
    }
}
