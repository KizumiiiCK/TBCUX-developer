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
    [SerializeField] private ScrollRect IconScrollRect;
    private int tresureCount = 0;
    private GameObject iconUnitPrefab;

    public void ShowCharacterDetails(CharacterData cd, bool consider_treasure, int level)
    {
        if (cd == null) return;

        ApplyTraitGroups(cd);
        SetDetailedText(cd, consider_treasure, level);
        ShowEAIcons(cd);
    }

    public void ShowEAIcons(CharacterData cd)
    {
        var payloads = new List<IconPayload>();

        if (cd.isEliteUnit)
        {
            payloads.Add(NewPayloadByCode("N:s", "D:s"));
        }
        if (cd.DRE.massiveDamage)
        {
            payloads.Add(NewPayloadByCode("N:dre:m", "D:dre:m"));
        }
        if (cd.DRE.insaneDamage)
        {
            payloads.Add(NewPayloadByCode("N:dre:i", "D:dre:i"));
        }
        if (cd.DRE.tough)
        {
            payloads.Add(NewPayloadByCode("N:dre:t", "D:dre:t"));
        }
        if (cd.DRE.aegis)
        {
            payloads.Add(NewPayloadByCode("N:dre:a", "D:dre:a"));
        }
        if (cd.DRE.strongAgainst)
        {
            payloads.Add(NewPayloadByCode("N:dre:s", "D:dre:s"));
        }
        if (cd.againstCareer.AggainstWarrior)
        {
            payloads.Add(NewPayloadByCode("N:ac:1", "D:ac:1"));
        }
        if (cd.againstCareer.AggainstDeffender)
        {
            payloads.Add(NewPayloadByCode("N:ac:2", "D:ac:2"));
        }
        if (cd.againstCareer.AggainstMagician)
        {
            payloads.Add(NewPayloadByCode("N:ac:3", "D:ac:3"));
        }
        if (cd.againstCareer.AggainstSupporter)
        {
            payloads.Add(NewPayloadByCode("N:ac:4", "D:ac:4"));
        }
        if (cd.againstCareer.AggainstPractician)
        {
            payloads.Add(NewPayloadByCode("N:ac:5", "D:ac:5"));
        }

        if (cd.characterEffects != null)
        {
            for (int i = 0; i < cd.characterEffects.Length; i++)
            {
                try
                {
                    int ibenum = GetEnumNumericId(cd.characterEffects[i].name);
                    payloads.Add(NewPayloadByCode(
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
                    int ibenum = GetEnumNumericId(cd.abilities[i].name);
                    payloads.Add(NewPayloadByCode(
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
                    int ibenum = GetEnumNumericId(cd.atkTypeResis[i].type);
                    payloads.Add(NewPayloadByCode(
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
                    int ibenum = GetEnumNumericId(cd.effectResistances[i].name);
                    payloads.Add(NewPayloadByCode(
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

        RefreshIconScrollContent(payloads.Count);
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
            cd.traits.Red, cd.traits.Flt, cd.traits.Blk, cd.traits.Mtl, cd.traits.Ang,
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
    private IconPayload NewPayloadByCode(
        string nameCode,
        string descriptionCode,
        int probability = 0,
        int duration = 0,
        int intensity = 0)
    {
        return NewPayload(
            EAIconResolver.LoadByNameCode(nameCode),
            nameCode,
            descriptionCode,
            probability,
            duration,
            intensity);
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

        EnsureIconListHorizontalLayout();
    }

    private void EnsureIconListHorizontalLayout()
    {
        if (IconList == null) return;
        var contentRect = IconList as RectTransform;
        if (contentRect == null) return;

        if (IconScrollRect == null) IconScrollRect = contentRect.GetComponentInParent<ScrollRect>();
        if (IconScrollRect != null)
        {
            if (IconScrollRect.content != contentRect) IconScrollRect.content = contentRect;
            IconScrollRect.horizontal = true;
            IconScrollRect.vertical = false;
        }

        var horizontal = contentRect.GetComponent<HorizontalLayoutGroup>();
        if (horizontal != null)
        {
            horizontal.childAlignment = TextAnchor.MiddleLeft;
            horizontal.childForceExpandWidth = false;
            horizontal.childForceExpandHeight = false;
        }

        var fitter = contentRect.GetComponent<ContentSizeFitter>();
        if (fitter != null)
        {
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    private void RefreshIconScrollContent(int iconCount)
    {
        if (IconList == null) return;
        var contentRect = IconList as RectTransform;
        if (contentRect == null) return;

        EnsureIconListHorizontalLayout();

        float iconWidth = 0f;
        float iconHeight = 0f;
        if (IconList.childCount > 0)
        {
            var first = IconList.GetChild(0) as RectTransform;
            if (first != null)
            {
                iconWidth = Mathf.Max(iconWidth, LayoutUtility.GetPreferredWidth(first));
                iconHeight = Mathf.Max(iconHeight, LayoutUtility.GetPreferredHeight(first));
                if (iconWidth <= 0f) iconWidth = Mathf.Max(iconWidth, first.rect.width);
                if (iconHeight <= 0f) iconHeight = Mathf.Max(iconHeight, first.rect.height);
                if (iconWidth <= 0f) iconWidth = Mathf.Max(iconWidth, first.sizeDelta.x);
                if (iconHeight <= 0f) iconHeight = Mathf.Max(iconHeight, first.sizeDelta.y);
            }
        }

        if (iconWidth <= 0f || iconHeight <= 0f)
        {
            if (iconUnitPrefab == null) iconUnitPrefab = Resources.Load<GameObject>(IconUnitPrefabPath);
            if (iconUnitPrefab != null)
            {
                var prefabRect = iconUnitPrefab.GetComponent<RectTransform>();
                if (prefabRect != null)
                {
                    if (iconWidth <= 0f) iconWidth = Mathf.Max(prefabRect.rect.width, prefabRect.sizeDelta.x);
                    if (iconHeight <= 0f) iconHeight = Mathf.Max(prefabRect.rect.height, prefabRect.sizeDelta.y);
                }
            }
        }

        if (iconWidth <= 0f) iconWidth = 64f;
        if (iconHeight <= 0f) iconHeight = 64f;

        float spacing = 0f;
        RectOffset padding = null;
        var horizontal = contentRect.GetComponent<HorizontalLayoutGroup>();
        if (horizontal != null)
        {
            spacing = horizontal.spacing;
            padding = horizontal.padding;
        }

        float width = iconCount > 0
            ? iconCount * iconWidth + Mathf.Max(0, iconCount - 1) * spacing + (padding != null ? padding.left + padding.right : 0)
            : 0f;
        float height = iconHeight + (padding != null ? padding.top + padding.bottom : 0);

        if (IconScrollRect != null && IconScrollRect.viewport != null)
        {
            width = Mathf.Max(width, IconScrollRect.viewport.rect.width);
            height = Mathf.Max(height, IconScrollRect.viewport.rect.height);
        }

        contentRect.sizeDelta = new Vector2(width, height);
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        if (IconScrollRect != null)
        {
            IconScrollRect.horizontalNormalizedPosition = 0f;
        }
    }

    private void LightUpTrait(Image source_img, bool lightup)
    {
        if (source_img == null) return;
        if (lightup) { source_img.color = Color.white; }
        else { source_img.color = new Color(0.4f, 0.4f, 0.4f, 1); }
    }

    private static int GetEnumNumericId<TEnum>(TEnum value) where TEnum : Enum
    {
        return Convert.ToInt32(value);
    }
}

public static class EAIconResolver
{
    private const string RootPath = "EAIcons/";
    private const string FallbackPath = "EAIcons/null";
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
    private static Sprite fallbackSprite;

    public static Sprite LoadByNameCode(string nameCode)
    {
        string iconPath = NameCodeToPath(nameCode);
        return LoadSpriteOrFallback(iconPath);
    }

    public static Sprite LoadSpriteOrFallback(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath))
        {
            return GetFallback();
        }
        if (cache.TryGetValue(iconPath, out var cached) && cached != null)
        {
            return cached;
        }
        Sprite sprite = Resources.Load<Sprite>(iconPath);
        if (sprite == null)
        {
            sprite = GetFallback();
        }
        cache[iconPath] = sprite;
        return sprite;
    }

    private static Sprite GetFallback()
    {
        if (fallbackSprite == null)
        {
            fallbackSprite = Resources.Load<Sprite>(FallbackPath);
        }
        return fallbackSprite;
    }

    private static string NameCodeToPath(string nameCode)
    {
        if (string.IsNullOrEmpty(nameCode)) return FallbackPath;
        if (nameCode == "N:s") return RootPath + "s";

        string[] parts = nameCode.Split(':');
        if (parts.Length != 3 || parts[0] != "N") return FallbackPath;

        string kind = parts[1];
        string id = parts[2];
        if (string.IsNullOrEmpty(id)) return FallbackPath;

        return kind switch
        {
            "e" => RootPath + "e-" + id,
            "a" => RootPath + "a-" + id,
            "ra" => RootPath + "ra-" + id,
            "re" => RootPath + "re-" + id,
            "dre" => RootPath + "dre-" + id,
            "ac" => RootPath + "ac-" + id,
            _ => FallbackPath
        };
    }
}
