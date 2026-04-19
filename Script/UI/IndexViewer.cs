using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class IndexViewer : MonoBehaviour
{
    private struct IconPayload
    {
        public Sprite Sprite;
        public string NameCode;
        public string DescriptionCode;
        public int Probability;
        public int Duration;
        public int Intensity;
    }

    private const string IconUnitPrefabPath = "UI/IconUnit";

    [SerializeField] private Transform TraitsList;
    [SerializeField] private Transform SubTraitsList;
    [SerializeField] private Transform CareerList;
    [SerializeField] private Transform IconList;
    [SerializeField] private TMP_Text DetailsText;
    [SerializeField] private GameObject ImageHelper;
    private IconImageHelper IIH;
    private int tresureCount = 0;
    private GameObject iconUnitPrefab;

    public void ShowCharacterDetails(CharacterData cd, bool consider_treasure, int level)
    {
        if (cd == null) return;
        if (ImageHelper != null) IIH = ImageHelper.GetComponent<IconImageHelper>();
        if (IIH == null) return;

        ApplyTraitGroups(cd);
        SetDetailedText(cd, consider_treasure, level);
        ShowEAIcons(cd);
    }

    public void ShowEAIcons(CharacterData cd)
    {
        var payloads = new List<IconPayload>();

        if (cd.isEliteUnit)
        {
            payloads.Add(NewPayload(IIH.strategic, "N:s", "D:s"));
        }
        if (cd.DRE.massiveDamage)
        {
            payloads.Add(NewPayload(IIH.icon_massive, "N:dre:m", "D:dre:m"));
        }
        if (cd.DRE.insaneDamage)
        {
            payloads.Add(NewPayload(IIH.icon_insane, "N:dre:i", "D:dre:i"));
        }
        if (cd.DRE.tough)
        {
            payloads.Add(NewPayload(IIH.icon_tough, "N:dre:t", "D:dre:t"));
        }
        if (cd.DRE.aegis)
        {
            payloads.Add(NewPayload(IIH.icon_aegis, "N:dre:a", "D:dre:a"));
        }
        if (cd.DRE.strongAgainst)
        {
            payloads.Add(NewPayload(IIH.icon_strongagainst, "N:dre:s", "D:dre:s"));
        }
        if (cd.againstCareer.AggainstWarrior)
        {
            payloads.Add(NewPayload(IIH.against_W, "N:ac:1", "D:ac:1"));
        }
        if (cd.againstCareer.AggainstDeffender)
        {
            payloads.Add(NewPayload(IIH.against_D, "N:ac:2", "D:ac:2"));
        }
        if (cd.againstCareer.AggainstMagician)
        {
            payloads.Add(NewPayload(IIH.against_M, "N:ac:3", "D:ac:3"));
        }
        if (cd.againstCareer.AggainstSuppoter)
        {
            payloads.Add(NewPayload(IIH.against_S, "N:ac:4", "D:ac:4"));
        }
        if (cd.againstCareer.AggainstPractician)
        {
            payloads.Add(NewPayload(IIH.against_P, "N:ac:5", "D:ac:5"));
        }

        if (cd.characterEffects != null)
        {
            for (int i = 0; i < cd.characterEffects.Length; i++)
            {
                try
                {
                    int ibenum = Array.IndexOf(Enum.GetValues(typeof(EffectName)), cd.characterEffects[i].name);
                    payloads.Add(NewPayload(
                        IIH.GetIconSprite(cd.characterEffects[i].name),
                        $"N:e:{ibenum}",
                        $"D:e:{ibenum}",
                        cd.characterEffects[i].probability,
                        cd.characterEffects[i].duration,
                        cd.characterEffects[i].intensity
                    ));
                }
                catch { Debug.Log($"Setup effect slot {i} error!"); }
            }
        }

        if (cd.abilities != null)
        {
            for (int i = 0; i < cd.abilities.Length; i++)
            {
                try
                {
                    int ibenum = Array.IndexOf(Enum.GetValues(typeof(AbilityName)), cd.abilities[i].name);
                    payloads.Add(NewPayload(
                        IIH.GetIconSprite(cd.abilities[i].name),
                        $"N:a:{ibenum}",
                        $"D:a:{ibenum}",
                        cd.abilities[i].probability,
                        cd.abilities[i].duration,
                        cd.abilities[i].intensity
                    ));
                }
                catch { Debug.Log($"Setup ability slot {i} error!"); }
            }
        }

        if (cd.atkTypeResis != null)
        {
            for (int i = 0; i < cd.atkTypeResis.Length; i++)
            {
                try
                {
                    int ibenum = Array.IndexOf(Enum.GetValues(typeof(AttackType)), cd.atkTypeResis[i].type);
                    payloads.Add(NewPayload(
                        IIH.GetAtkResSprite(cd.atkTypeResis[i].type),
                        $"N:ra:{ibenum}",
                        $"D:ra:{ibenum}",
                        cd.atkTypeResis[i].intensity
                    ));
                }
                catch { Debug.Log($"Setup atk type slot {i} error!"); }
            }
        }

        if (cd.effectResistances != null)
        {
            for (int i = 0; i < cd.effectResistances.Length; i++)
            {
                try
                {
                    int ibenum = Array.IndexOf(Enum.GetValues(typeof(EffectName)), cd.effectResistances[i].name);
                    payloads.Add(NewPayload(
                        IIH.GetEffResSprite(cd.effectResistances[i].name),
                        $"N:re:{ibenum}",
                        $"D:re:{ibenum}",
                        cd.effectResistances[i].probability
                    ));
                }
                catch { Debug.Log($"Setup eff res slot {i} error!"); }
            }
        }

        RebuildIconUnits(payloads.Count);
        for (int i = 0; i < payloads.Count; i++)
        {
            var item = IconList.GetChild(i);
            var image = item.GetComponent<Image>();
            var iconButton = item.GetComponent<IconButton>();
            if (image != null) image.sprite = payloads[i].Sprite;
            if (iconButton != null)
            {
                iconButton.SetDescriptionInfo(
                    payloads[i].NameCode,
                    payloads[i].DescriptionCode,
                    payloads[i].Probability,
                    payloads[i].Duration,
                    payloads[i].Intensity
                );
            }
        }
    }

    public void SetDetailedText(CharacterData cd, bool consider_treasure, int level=1)
    {
        if (DetailsText == null) return;
        if(level<1)level = 1;
        tresureCount = consider_treasure?RewardingSystem.GetAmount(RewardName.WorldTreasures):0;
        float bonus = (1 + 1.5f * (tresureCount / 150f))*(0.8f + 0.2f * level);
        DetailsText.text = 
            $"HP:         <color=#00FFFF>{(int)(cd.Health*bonus)}</color>   " +
            $"(KB  <color=#00AFFF>{cd.KB}</color>)\r\n" +
            $"RANGE:  <color=yellow>{cd.DetectionRange}</color>\r\n" +
            $"ATK:       ";
        int atk_length = cd.atkInfos.Length;
        for (int i = 0; i < atk_length; i++)
        {
            DetailsText.text += $"<color=#FF3030>{(int)(cd.atkInfos[i].ATK*bonus)}</color>  (<color=yellow>{cd.atkInfos[i].frame}</color>f,  " +
                $"r=(<color=yellow>{cd.atkInfos[i].ATKRange.x}</color>,  <color=yellow>{cd.atkInfos[i].ATKRange.y}</color>))"+
                "/\r\n                ";
        }
        DetailsText.text+=
            $" -- (<color=yellow>{cd.atkDuration - cd.atkInfos[atk_length-1].frame}</color>f)\r\n"+
            $"COST:    <color=#FCBA01>{cd.Cost}$</color>\r\n" +
            $"CD:          <color=#75FF85>{cd.Cooldown}</color>";
    }

    private void ApplyTraitGroups(CharacterData cd)
    {
        ApplyTraits(TraitsList, new[]
        {
            cd.traits.Red, cd.traits.Flt, cd.traits.Blk, cd.traits.Ang, cd.traits.Mtl,
            cd.traits.Aln, cd.traits.Z, cd.traits.Re, cd.traits.Aku, cd.traits.None
        });

        ApplyTraits(SubTraitsList, new[]
        {
            cd.subtraits.Starred, cd.subtraits.Colossus, cd.subtraits.Behemoth, cd.subtraits.Sage
        });

        ApplyTraits(CareerList, new[]
        {
            cd.career.Warrior, cd.career.Deffender, cd.career.Magician, cd.career.Supporter, cd.career.Practician
        });
    }

    private void ApplyTraits(Transform listRoot, bool[] states)
    {
        if (listRoot == null || states == null) return;
        int count = Mathf.Min(listRoot.childCount, states.Length);
        for (int i = 0; i < count; i++)
        {
            var image = listRoot.GetChild(i).GetComponent<Image>();
            LightUpTrait(image, states[i]);
        }
    }

    private IconPayload NewPayload(
        Sprite sprite,
        string nameCode,
        string descriptionCode,
        int probability = 0,
        int duration = 0,
        int intensity = 0)
    {
        return new IconPayload
        {
            Sprite = sprite,
            NameCode = nameCode,
            DescriptionCode = descriptionCode,
            Probability = probability,
            Duration = duration,
            Intensity = intensity
        };
    }

    private void RebuildIconUnits(int requiredCount)
    {
        if (IconList == null) return;
        if (requiredCount < 0) requiredCount = 0;

        for (int i = 0; i < IconList.childCount; i++)
        {
            IconList.GetChild(i).gameObject.SetActive(i < requiredCount);
        }

        if (requiredCount == 0) return;

        if (iconUnitPrefab == null)
        {
            iconUnitPrefab = Resources.Load<GameObject>(IconUnitPrefabPath);
        }
        if (iconUnitPrefab == null)
        {
            Debug.LogError($"[IndexViewer] Missing icon unit prefab at Resources/{IconUnitPrefabPath}");
            return;
        }

        while (IconList.childCount < requiredCount)
        {
            var go = Instantiate(iconUnitPrefab, IconList, false);
            go.SetActive(true);
        }
    }

    private void LightUpTrait(Image source_img, bool lightup)
    {
        if (source_img == null) return;
        if (lightup) { source_img.color = Color.white; }
        else { source_img.color = new Color(0.4f, 0.4f, 0.4f, 1); }
    }
}
