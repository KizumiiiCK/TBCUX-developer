using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterSummoner : MonoBehaviour
{
    public static GameObject CreateACharacter(bool cat, string characterCode, bool isIndexUnit)
    {
        string loadPath = cat? 
            $"Units/Cat Units/{characterCode[0]}/{characterCode.Substring(1, 3)}/{characterCode[4]}/":
            $"Units/Enemy Units/{characterCode}/";
        CharacterData CD = Resources.Load<CharacterData>(loadPath + "data");
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
            ResetAnimationOrderLayer(C, "UI", 3);
            C.GetComponent<Animator>().SetInteger("state", 0);
            C.GetComponent<Animator>().speed = 1;
        }
        else
        {
            Texture2D unitTexture = Resources.Load<Texture2D>(loadPath + "sprite");
            TextAsset imagecut = Resources.Load<TextAsset>(loadPath + "imgcut");
            TextAsset mamodel = Resources.Load<TextAsset>(loadPath + "mamodel");
            TextAsset maanim_walk = Resources.Load<TextAsset>(loadPath + "maanim_walk");
            TextAsset maanim_idle = Resources.Load<TextAsset>(loadPath + "maanim_idle");
            TextAsset maanim_attack = Resources.Load<TextAsset>(loadPath + "maanim_attack");
            TextAsset maanim_kb = Resources.Load<TextAsset>(loadPath + "maanim_kb");
            string unitLoadPath;
            if (isIndexUnit) unitLoadPath = "Units/IndexUnit";
            else if (cat) unitLoadPath = "Units/Cat Units/catunit";
            else unitLoadPath = "Units/Enemy Units/enemyunit";
            C = Instantiate(Resources.Load<GameObject>(unitLoadPath));
            AnimationDisplayer ad = C.GetComponent<AnimationDisplayer>();
            ad.SetImage(unitTexture);
            ad.SetImgcut(imagecut);
            ad.SetModel(mamodel);
            ad.SetMaanimLength(4);
            ad.SetMaanim(maanim_walk, 0);
            ad.SetMaanim(maanim_idle, 1);
            ad.SetMaanim(maanim_attack, 2);
            ad.SetMaanim(maanim_kb, 3);
            ad.Initialization();
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
}
