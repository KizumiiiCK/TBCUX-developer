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
    protected AnimDecryptPack[] buffs_decrypted;
    //protected AnimDecryptPack wave_decrypt;
    //protected AnimDecryptPack wave_e_decrypt;
    //protected AnimDecryptPack surge_decrypt;
    //protected AnimDecryptPack surge_e_decrypt;
    public AnimationDisplayer InstantiateBattleObject(SEnums sename,float posX, float posY, bool playSound=true)
    {
        int matchedNum = SEpairs[sename];
        AnimationDisplayer ad= Instantiate(EffectObjectNull, new Vector3(posX, posY+Ydeviation[sename], 0), Quaternion.identity).GetComponent<AnimationDisplayer>();
        if (ad != null)
        {
            ad.Initialization(assets_decrypted[SEpairs[sename]]);
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
    //public AnimDecryptPack InstantiateBattleEffect(EffectName en)
    //{
    //    return buffs_decrypted[Array.IndexOf(Enum.GetValues(typeof(EffectName)), en)-1];
    //}
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
    private void Start()
    {
        EncryptAllFiles();
    }
    private void EncryptAllFiles()
    {
        assets_decrypted = new AnimDecryptPack[DFF.Length];
        AnimEncryptPack animEncryptPack;
        for (int i=0;i< DFF.Length; i++)
        {
            animEncryptPack = new AnimEncryptPack(DFF[i].unitTexture, DFF[i].imgcut, DFF[i].mamodel, DFF[i].maanim);
            assets_decrypted[i] = AnimFileDecrypter.DecryptEncryptPack(animEncryptPack);
        }
        //animEncryptPack = new AnimEncryptPack(wave_D.unitTexture, wave_D.imgcut, wave_D.mamodel, wave_D.maanim);
        //wave_decrypt = AnimFileDecrypter.DecryptEncryptPack(animEncryptPack);
        //animEncryptPack = new AnimEncryptPack(wave_e_D.unitTexture, wave_e_D.imgcut, wave_e_D.mamodel, wave_e_D.maanim);
        //wave_e_decrypt = AnimFileDecrypter.DecryptEncryptPack(animEncryptPack);
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

