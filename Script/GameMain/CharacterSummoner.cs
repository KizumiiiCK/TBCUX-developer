using Spine.Unity;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CharacterSummoner : MonoBehaviour
{
    private const string CatUnitPrefabPath = "Units/Cat Units/catunit";
    private const string EnemyUnitPrefabPath = "Units/Enemy Units/enemyunit";
    private const string IndexUnitPrefabPath = "Units/IndexUnit";

    public static GameObject CreateACharacter(bool cat, string characterCode, bool isIndexUnit)
    {
        CharacterData CD = LoadCharacterData(cat, characterCode);
        if (CD == null)
        {
            Debug.LogError($"No character data for {characterCode}");
            return null;
        }
        GameObject C;
        if (CD.UNITYAnimated)
        {
            string caracterLoadPath = cat ?
                $"Units/Cat Units/{characterCode[0]}/{characterCode.Substring(1, 3)}/{characterCode[4]}/uaunit" :
                $"Units/Enemy Units/{characterCode}/uaunit";
            GameObject prefab = LoadPrefab(cat, caracterLoadPath);
            if (prefab == null)
            {
                Debug.LogError($"No uaunit prefab at {caracterLoadPath}");
                return null;
            }
            C = Instantiate(prefab);
            if (CD.SPINEAnimated)
            {
                ResetSpineOrderLayer(C, "UI", 3);
            }
            else ResetAnimationOrderLayer(C, "UI", 3);
            C.GetComponent<Animator>().SetInteger("state", 0);
            C.GetComponent<Animator>().speed = 1;
        }
        else
        {
            string unitLoadPath;
            if (isIndexUnit) unitLoadPath = IndexUnitPrefabPath;
            else if (cat) unitLoadPath = CatUnitPrefabPath;
            else unitLoadPath = EnemyUnitPrefabPath;

            GameObject prefab = isIndexUnit
                ? Resources.Load<GameObject>(unitLoadPath)
                : BundledAddressables.LoadSync<GameObject>(unitLoadPath);
            if (prefab == null)
            {
                Debug.LogError($"No unit prefab at {unitLoadPath}");
                return null;
            }
            C = Instantiate(prefab);
            AnimationDisplayer ad = C.GetComponent<AnimationDisplayer>();
            AnimDecryptPack pack = DecryptCharacterFiles(cat, characterCode, CD);
            ad.Initialization(pack);
            ad.OrderLayerStart = 3;
            ad.ResetModelOrderLayer();
        }
        return C;
    }

    public static void ResetAnimationOrderLayer(AnimationDisplayer ad, int order)
    {
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

    public static void SetCharacterPosition(GameObject C, Vector3 pos) => C.transform.position = pos;

    public static CharacterData LoadCharacterData(bool cat, string characterCode)
    {
        string loadPath = GetCharacterLoadPath(cat, characterCode);
        CharacterData data = LoadAsset<CharacterData>(cat, loadPath + "data");
        return data != null ? data.Clone() : null;
    }

    public static AnimDecryptPack DecryptCharacterFiles(bool cat, string characterCode, CharacterData data)
    {
        if (data == null || data.UNITYAnimated) return null;

        string loadPath = GetCharacterLoadPath(cat, characterCode);
        Texture2D unitTexture = LoadAsset<Texture2D>(cat, loadPath + "sprite");
        TextAsset imagecut = LoadAsset<TextAsset>(cat, loadPath + "imgcut");
        TextAsset mamodel = LoadAsset<TextAsset>(cat, loadPath + "mamodel");
        TextAsset maanim_walk = LoadAsset<TextAsset>(cat, loadPath + "maanim_walk");
        TextAsset maanim_idle = LoadAsset<TextAsset>(cat, loadPath + "maanim_idle");
        TextAsset maanim_attack = LoadAsset<TextAsset>(cat, loadPath + "maanim_attack");
        TextAsset maanim_kb = LoadAsset<TextAsset>(cat, loadPath + "maanim_kb");

        List<TextAsset> maanims = new List<TextAsset> { maanim_walk, maanim_idle, maanim_attack, maanim_kb };
        if (cat && data.career != null && data.career.Practician)
        {
            TextAsset maanim_p = LoadAsset<TextAsset>(cat, loadPath + "maanim_p");
            if (maanim_p != null) maanims.Add(maanim_p);
        }
        if (!cat && data.abilities.Any(a => a.name == AbilityName.ZombieDive))
        {
            TextAsset maanim_in = LoadAsset<TextAsset>(cat, loadPath + "maanim_in");
            TextAsset maanim_dive = LoadAsset<TextAsset>(cat, loadPath + "maanim_dive");
            TextAsset maanim_out = LoadAsset<TextAsset>(cat, loadPath + "maanim_out");
            if (maanim_in != null) maanims.Add(maanim_in);
            if (maanim_dive != null) maanims.Add(maanim_dive);
            if (maanim_out != null) maanims.Add(maanim_out);
        }

        AnimEncryptPack pack = new AnimEncryptPack(unitTexture, imagecut, mamodel, maanims.ToArray());
        return AnimFileDecrypter.DecryptEncryptPack(pack);
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
            string uaPath = cat
                ? $"Units/Cat Units/{characterCode[0]}/{characterCode.Substring(1, 3)}/{characterCode[4]}/uaunit"
                : $"Units/Enemy Units/{characterCode}/uaunit";
            GameObject uaPrefab = LoadPrefab(cat, uaPath);
            if (uaPrefab == null) return;
            GameObject uaunit = Instantiate(uaPrefab, runtimeCharacter.transform.position, Quaternion.identity);
            uaunit.transform.SetParent(runtimeCharacter.transform);
            if (data.SPINEAnimated)
            {
                ResetSpineOrderLayer(uaunit, sortingLayer, uaOrder);
            }
            else ResetAnimationOrderLayer(uaunit, sortingLayer, uaOrder);
            Animator animator = uaunit.GetComponent<Animator>();
            if (animator != null)
            {
                animator.SetInteger("state", 0);
                animator.speed = 1f;
            }
            return;
        }

        if (decryptedPack == null)
        {
            decryptedPack = DecryptCharacterFiles(cat, characterCode, data);
        }
        AnimationDisplayer ad = runtimeCharacter.GetComponent<AnimationDisplayer>();
        if (ad == null || decryptedPack == null) return;
        ad.Initialization(decryptedPack);
        ad.OrderLayerStart = adOrder;
        ad.ResetModelOrderLayer();
    }

    public static void SwitchAnimation(GameObject C, bool ua, int animationNum)
    {
        if (C == null) return;
        if (ua)
        {
            Debug.Log($"Unity animate: {animationNum}");
            C.GetComponent<Animator>().SetInteger("state", animationNum);
        }
        else C.GetComponent<AnimationDisplayer>().PlayAnimation(animationNum);
    }

    private static string GetCharacterLoadPath(bool cat, string characterCode)
    {
        return cat
            ? $"Units/Cat Units/{characterCode[0]}/{characterCode.Substring(1, 3)}/{characterCode[4]}/"
            : $"Units/Enemy Units/{characterCode}/";
    }

    private static T LoadAsset<T>(bool cat, string address) where T : UnityEngine.Object
    {
        return BundledAddressables.LoadSync<T>(address);
    }

    private static GameObject LoadPrefab(bool cat, string address)
    {
        return LoadAsset<GameObject>(cat, address);
    }
}
