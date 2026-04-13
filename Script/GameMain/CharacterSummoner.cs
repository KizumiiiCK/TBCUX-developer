using Spine.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterSummoner : MonoBehaviour
{
    private const string CatUnitPrefabPath = "Units/Cat Units/catunit";
    private const string EnemyUnitPrefabPath = "Units/Enemy Units/enemyunit";
    private const string IndexUnitPrefabPath = "Units/IndexUnit";

    public static GameObject CreateACharacter(bool cat, string characterCode, bool isIndexUnit)
    {
        string loadPath = GetCharacterLoadPath(cat, characterCode);
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
            C = Instantiate(Resources.Load<GameObject>(caracterLoadPath));
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
            C = Instantiate(Resources.Load<GameObject>(unitLoadPath));
            AnimationDisplayer ad = C.GetComponent<AnimationDisplayer>();
            AnimDecryptPack pack = DecryptCharacterFiles(cat, characterCode, CD);
            ad.Initialization(pack);
            ad.OrderLayerStart = 3;
            ad.ResetModelOrderLayer();
        }
        //IV.ShowCharacterDetails(CD);
        //LocalizationHelper.GetLocalizedText("UnitNames", current_code, localizedText => name_txt.text = localizedText ?? current_code);
        return C;
    }
    public static void ResetAnimationOrderLayer(AnimationDisplayer ad, int order)
    {
        ad.OrderLayerStart=order;
        ad.ResetModelOrderLayer();
    }
    public static void ResetSpineOrderLayer(GameObject go, string sortingLayer, int order)
    {
        if (go == null) return;
        MeshRenderer mr = go.GetComponentInChildren<MeshRenderer>(true);
        if (mr != null)
        {
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
        CharacterData data = Resources.Load<CharacterData>(loadPath + "data");
        return data != null ? data.Clone() : null;
    }

    public static AnimDecryptPack DecryptCharacterFiles(bool cat, string characterCode, CharacterData data, bool includeZombieDiveMaanim = false)
    {
        if (data == null || data.UNITYAnimated) return null;

        string loadPath = GetCharacterLoadPath(cat, characterCode);
        Texture2D unitTexture = Resources.Load<Texture2D>(loadPath + "sprite");
        TextAsset imagecut = Resources.Load<TextAsset>(loadPath + "imgcut");
        TextAsset mamodel = Resources.Load<TextAsset>(loadPath + "mamodel");
        TextAsset maanim_walk = Resources.Load<TextAsset>(loadPath + "maanim_walk");
        TextAsset maanim_idle = Resources.Load<TextAsset>(loadPath + "maanim_idle");
        TextAsset maanim_attack = Resources.Load<TextAsset>(loadPath + "maanim_attack");
        TextAsset maanim_kb = Resources.Load<TextAsset>(loadPath + "maanim_kb");

        List<TextAsset> maanims = new List<TextAsset> { maanim_walk, maanim_idle, maanim_attack, maanim_kb };
        if (cat && data.career != null && data.career.Practician)
        {
            TextAsset maanim_p = Resources.Load<TextAsset>(loadPath + "maanim_p");
            if (maanim_p != null) maanims.Add(maanim_p);
        }
        if (!cat && ((data.traits != null && data.traits.Z) || includeZombieDiveMaanim))
        {
            TextAsset maanim_dive = Resources.Load<TextAsset>(loadPath + "maanim_dive");
            TextAsset maanim_out = Resources.Load<TextAsset>(loadPath + "maanim_out");
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
            GameObject uaunit = Instantiate(Resources.Load<GameObject>(uaPath), runtimeCharacter.transform.position, Quaternion.identity);
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
            //current_animation_num = (current_animation_num + 1) % 4;
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
}
