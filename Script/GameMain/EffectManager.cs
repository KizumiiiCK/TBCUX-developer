using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EffectManager : MonoBehaviour
{
    [SerializeField] private List<GameObject> hitEffects;
    [SerializeField] public GameObject EffectObjectNull;
    [SerializeField] protected DecryptFileFormat[] DFF;
    [SerializeField] protected DecryptFileFormat wave_D;
    [SerializeField] protected DecryptFileFormat wave_e_D;
    protected AnimDecryptPack[] assets_decrypted;
    public AnimationDisplayer InstantiateBattleObject(SEnums sename,float posX, float posY, bool playSound=true)
    {
        int matchedNum = SEpairs[sename];
        AnimationDisplayer ad= Instantiate(EffectObjectNull, new Vector3(posX, posY+Ydeviation[sename], 0), Quaternion.identity).GetComponent<AnimationDisplayer>();
        if (ad != null)
        {
            AnimDecryptPack pack = GetOrCreateEffectPack(matchedNum);
            if (pack != null) ad.Initialization(pack);
            int resetlayer = (int)(20000 * (1 - posY));
            if (Mathf.Abs(resetlayer) > 21000) resetlayer = 21000;
            CharacterSummoner.ResetAnimationOrderLayer(ad, resetlayer);
            //ad.OrderLayerStart = 20000;
            //ad.ResetModelOrderLayer();
        }
        if (SEExistT[sename] > 0)
        {
            ad.gameObject.AddComponent<InstanceObject>().exist_duration= SEExistT[sename];
        }
        if (playSound)
        {
            SoundEffects ses = transform.GetChild(SEpairs[sename]).GetComponent<SoundEffects>();
            ses.PlayEffectSound();
        }
        return ad;
    }
    private static Dictionary<SEnums, int> SEpairs = new Dictionary<SEnums, int>() {
        { SEnums.soul,         0 },
        { SEnums.bite,         1 },
        { SEnums.critical,     2 },
        { SEnums.savage,       3 },
        { SEnums.soulStrike,   4 },
        { SEnums.wave_invalid, 5 },
        { SEnums.invalid,      6 },
        { SEnums.wave,         7 },
        { SEnums.wave_e,       8 },
        { SEnums.heal,         9 },
        { SEnums.heal_e,       10 },
        { SEnums.surge,        11 },
        { SEnums.surge_e,      12 }
    };
    private static Dictionary<SEnums, int> SEExistT = new Dictionary<SEnums, int>() {
        { SEnums.soul,         90 },
        { SEnums.bite,         30 },
        { SEnums.critical,     30 },
        { SEnums.savage,       30 },
        { SEnums.soulStrike,   30 },
        { SEnums.wave_invalid, 30 },
        { SEnums.invalid,      30 },
        { SEnums.wave,         30 },
        { SEnums.wave_e,       30 },
        { SEnums.heal,         60 },
        { SEnums.heal_e,       60 },
        { SEnums.surge,        -1 },
        { SEnums.surge_e,      -1 }
    };
    private static Dictionary<SEnums, int> Ydeviation = new Dictionary<SEnums, int>() {
        { SEnums.soul,         1 },
        { SEnums.bite,         1 },
        { SEnums.critical,     1 },
        { SEnums.savage,       1 },
        { SEnums.soulStrike,   1 },
        { SEnums.wave_invalid, 1 },
        { SEnums.invalid,      1 },
        { SEnums.wave,         -1 },
        { SEnums.wave_e,       -1 },
        { SEnums.heal,         1 },
        { SEnums.heal_e,       1 },
        { SEnums.surge,        -1 },
        { SEnums.surge_e,      -1 }
    };
    private AnimDecryptPack GetOrCreateEffectPack(int index)
    {
        if (DFF == null || index < 0 || index >= DFF.Length) return null;
        if (assets_decrypted == null || assets_decrypted.Length != DFF.Length)
            assets_decrypted = new AnimDecryptPack[DFF.Length];
        if (assets_decrypted[index] != null) return assets_decrypted[index];

        var src = DFF[index];
        if (src == null) return null;
        AnimEncryptPack animEncryptPack = new AnimEncryptPack(src.unitTexture, src.imgcut, src.mamodel, src.maanim);
        assets_decrypted[index] = AnimFileDecrypter.DecryptEncryptPack(animEncryptPack);
        return assets_decrypted[index];
    }
    //public AnimDecryptPack GetCatWave() { return wave_decrypt; }
    //public AnimDecryptPack GetEnemyWave() { return wave_e_decrypt; }
}
public enum SEnums
{
    soul, bite, critical, savage, soulStrike, wave_invalid, invalid, wave, wave_e, heal, heal_e, surge, surge_e
}
[System.Serializable]
public class DecryptFileFormat
{
    public string name;
    public Texture2D unitTexture;
    public TextAsset imgcut;
    public TextAsset mamodel;
    public TextAsset[] maanim;
}

