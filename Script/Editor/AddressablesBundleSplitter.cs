using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

/// <summary>
/// 把 Units / Visuals 从「一个大 bundle」拆成「一个单位 / 一张地图一个 bundle」。
///
/// 做法：给每个 entry 打恰好一个标签，再把 group 的打包模式改成 PackTogetherByLabel
/// （BuildScriptPackedMode 里「一组唯一标签 = 一个 bundle」，bundle 文件名取标签串）。
/// 地址（m_Address）一个字节都不动，所以 BundledAddressables 的同步加载契约不受影响，
/// 运行时代码不用改一行。要回退就跑 Merge 菜单项。
///
/// 每个 entry 只给一个标签是硬要求：PackTogetherByLabel 拼 key 时遍历 HashSet&lt;string&gt;，
/// 多标签的顺序不稳定，会导致 bundle 名字在两次构建之间漂移。
/// </summary>
public static class AddressablesBundleSplitter
{
    private const string UnitsGroupName = "Units";
    private const string VisualsGroupName = "Visuals";

    /// <summary>标签前缀，用来把「拆包用」标签和人手加的标签区分开，回退时只清这些。</summary>
    private const string LabelPrefix = "pack-";

    [MenuItem("TBCX/Addressables/Split Units Into Per-Unit Bundles")]
    public static void SplitUnits() => SplitGroup(UnitsGroupName, UnitsLabelFor);

    [MenuItem("TBCX/Addressables/Split Visuals Into Per-Map Bundles")]
    public static void SplitVisuals() => SplitGroup(VisualsGroupName, VisualsLabelFor);

    [MenuItem("TBCX/Addressables/Merge Units Back Into One Bundle")]
    public static void MergeUnits() => Merge(UnitsGroupName);

    [MenuItem("TBCX/Addressables/Merge Visuals Back Into One Bundle")]
    public static void MergeVisuals() => Merge(VisualsGroupName);

    /// <summary>
    /// 一个单位（含全部形态）一个 bundle：进一场战斗只拉编队里那几只，而不是整包 Cat Units。
    /// 形态不再细分——一个单位三个形态平均不到 500KB，拆到形态级只多出两倍多的 bundle 数。
    /// </summary>
    public static string UnitsLabelFor(string address)
    {
        string[] p = Segments(address);
        if (p.Length >= 5 && p[1] == "Cat Units") return LabelPrefix + "cat-" + p[2] + "-" + p[3];
        if (p.Length >= 4 && p[1] == "Enemy Units") return LabelPrefix + "enemy-" + Sanitize(p[2]);
        // DogeBases / CatBases / Projectiles 三类加起来 3.5MB，按类合并就够，不值得再拆。
        if (p.Length >= 2) return LabelPrefix + Sanitize(p[1]);
        return LabelPrefix + "misc";
    }

    /// <summary>
    /// 一张地图一个 bundle（一场战斗只用一张，现在却要拉全部 96 张 16MB）；
    /// 剧情立绘按角色分，其余按顶层目录分。
    /// </summary>
    public static string VisualsLabelFor(string address)
    {
        string[] p = Segments(address);
        if (p.Length >= 3 && p[0] == "Background" && p[1] == "Maps")
        {
            // 纯数字的是地图本体；MapFillingShader / MapFillingMaterial / MapGradingSettings
            // 是所有地图共用的，单独成包才不会被复制进 96 个 bundle。
            return IsAllDigits(p[2]) ? LabelPrefix + "map-" + p[2] : LabelPrefix + "map-shared";
        }
        if (p.Length >= 2 && p[0] == "Background") return LabelPrefix + "bg-" + Sanitize(p[1]);
        if (p.Length >= 2 && p[0] == "DialogueImage") return LabelPrefix + "dlg-" + DialogueCharacter(p[1]);
        // System 下按子目录分：fonts 一包 15.6MB，别让 5KB 的 AudioMixer 也要拉整包字体才拿到。
        if (p.Length >= 2 && p[0] == "System") return LabelPrefix + "system-" + Sanitize(p[1]);
        if (p.Length >= 1) return LabelPrefix + Sanitize(p[0]);
        return LabelPrefix + "misc";
    }

    private static void SplitGroup(string groupName, System.Func<string, string> labelFor)
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("AddressableAssetSettings not found.");
            return;
        }

        AddressableAssetGroup group = settings.FindGroup(groupName);
        if (group == null)
        {
            Debug.LogError($"Addressables group '{groupName}' not found.");
            return;
        }

        BundledAssetGroupSchema schema = group.GetSchema<BundledAssetGroupSchema>();
        if (schema == null)
        {
            Debug.LogError($"Group '{groupName}' has no BundledAssetGroupSchema.");
            return;
        }

        int labelled = ApplyLabels(settings, group, labelFor, out HashSet<string> used);

        schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel;
        // 运行时没有任何按标签加载的代码，把标签写进 catalog 只会白涨 catalog 体积
        // （现在 catalog.json 已经 2.4MB，开局就要下载+解析）。
        schema.IncludeLabelsInCatalog = false;
        EditorUtility.SetDirty(schema);
        EditorUtility.SetDirty(group);

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, null, true, true);
        AssetDatabase.SaveAssets();
        Debug.Log(
            $"[BundleSplitter] '{groupName}' -> PackTogetherByLabel: {labelled} entries, {used.Count} bundles. " +
            "Addresses unchanged. Rebuild Addressables content, then run " +
            "Window > Asset Management > Addressables > Analyze > Check Duplicate Bundle Dependencies.");
    }

    private static void Merge(string groupName)
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("AddressableAssetSettings not found.");
            return;
        }

        AddressableAssetGroup group = settings.FindGroup(groupName);
        if (group == null)
        {
            Debug.LogError($"Addressables group '{groupName}' not found.");
            return;
        }

        BundledAssetGroupSchema schema = group.GetSchema<BundledAssetGroupSchema>();
        if (schema == null)
        {
            Debug.LogError($"Group '{groupName}' has no BundledAssetGroupSchema.");
            return;
        }

        int cleared = 0;
        HashSet<string> orphaned = new HashSet<string>();
        foreach (AddressableAssetEntry entry in new List<AddressableAssetEntry>(group.entries))
        {
            if (entry == null) continue;
            foreach (string label in new List<string>(entry.labels))
            {
                if (!label.StartsWith(LabelPrefix)) continue;
                entry.SetLabel(label, false, false, false);
                orphaned.Add(label);
                cleared++;
            }
        }

        // 标签是全局的，别的 group 可能还在用同一个前缀的标签，只删没人用的。
        foreach (string label in orphaned)
        {
            if (!IsLabelInUse(settings, label)) settings.RemoveLabel(label, false);
        }

        schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
        schema.IncludeLabelsInCatalog = true;
        EditorUtility.SetDirty(schema);
        EditorUtility.SetDirty(group);

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, null, true, true);
        AssetDatabase.SaveAssets();
        Debug.Log($"[BundleSplitter] '{groupName}' -> PackTogether, removed {cleared} pack labels.");
    }

    /// <summary>
    /// 给 group 里每个 entry 换上唯一一个拆包标签。Registrar 重建条目后调这个补标签，
    /// 否则重建会把 group 悄悄退回单包（标签没了，PackTogetherByLabel 全落到同一个空 key）。
    /// </summary>
    public static int ApplyLabels(
        AddressableAssetSettings settings,
        AddressableAssetGroup group,
        System.Func<string, string> labelFor,
        out HashSet<string> usedLabels)
    {
        usedLabels = new HashSet<string>();
        int labelled = 0;
        foreach (AddressableAssetEntry entry in new List<AddressableAssetEntry>(group.entries))
        {
            if (entry == null || string.IsNullOrEmpty(entry.address)) continue;

            string wanted = labelFor(entry.address);
            foreach (string stale in new List<string>(entry.labels))
            {
                if (stale != wanted && stale.StartsWith(LabelPrefix)) entry.SetLabel(stale, false, false, false);
            }

            settings.AddLabel(wanted, false);
            entry.SetLabel(wanted, true, true, false);
            usedLabels.Add(wanted);
            labelled++;
        }
        return labelled;
    }

    /// <summary>
    /// Registrar 重建条目后调用。只在 group 已经处于拆包模式时补标签——没拆包的工程行为完全不变。
    /// 不补的话，重建出来的条目全都没有标签，PackTogetherByLabel 会把它们塞进同一个空 key，
    /// 于是「拆过包」的工程在下一次重建后静默退回单个大 bundle。
    /// </summary>
    public static void RelabelIfSplit(
        AddressableAssetSettings settings,
        AddressableAssetGroup group,
        System.Func<string, string> labelFor)
    {
        if (settings == null || group == null) return;
        BundledAssetGroupSchema schema = group.GetSchema<BundledAssetGroupSchema>();
        if (schema == null || schema.BundleMode != BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel) return;

        int labelled = ApplyLabels(settings, group, labelFor, out HashSet<string> used);
        Debug.Log($"[BundleSplitter] Re-applied pack labels to '{group.Name}': {labelled} entries, {used.Count} bundles.");
    }

    public static string LabelForAddress(string groupName, string address)
    {
        if (groupName == UnitsGroupName) return UnitsLabelFor(address);
        if (groupName == VisualsGroupName) return VisualsLabelFor(address);
        return null;
    }

    private static bool IsLabelInUse(AddressableAssetSettings settings, string label)
    {
        foreach (AddressableAssetGroup group in settings.groups)
        {
            if (group == null) continue;
            foreach (AddressableAssetEntry entry in group.entries)
            {
                if (entry != null && entry.labels.Contains(label)) return true;
            }
        }
        return false;
    }

    private static string[] Segments(string address) => address.Split('/');

    private static bool IsAllDigits(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] < '0' || s[i] > '9') return false;
        }
        return true;
    }

    /// <summary>立绘按角色归包：Moneko_3_think / _Alina_x 都归到第一段人名。</summary>
    private static string DialogueCharacter(string fileName)
    {
        string name = fileName.TrimStart('_');
        int underscore = name.IndexOf('_');
        if (underscore > 0) name = name.Substring(0, underscore);
        return Sanitize(name);
    }

    /// <summary>标签会原样进 bundle 文件名，压成小写并去掉路径/空格不安全的字符。</summary>
    private static string Sanitize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "misc";
        StringBuilder sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = char.ToLowerInvariant(s[i]);
            if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) sb.Append(c);
            else if (sb.Length > 0 && sb[sb.Length - 1] != '-') sb.Append('-');
        }
        return sb.Length > 0 ? sb.ToString().TrimEnd('-') : "misc";
    }
}
