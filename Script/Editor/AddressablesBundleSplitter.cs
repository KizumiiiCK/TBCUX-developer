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

    /// <summary>一个 bundle 至少要装到这个量级才值一次 HTTP 请求（见 EnemyBucket 注释）。</summary>
    private const int EnemyBucketKeyLength = 3;

    /// <summary>
    /// 猫咪：一个单位（含全部形态）一个 bundle：进一场战斗只拉编队里那几只，而不是整包 Cat Units。
    /// 形态不再细分——一个单位三个形态平均不到 500KB，拆到形态级只多出两倍多的 bundle 数。
    ///
    /// 敌人：按编号前缀归桶，不做一敌一包。见 <see cref="EnemyBucket"/>。
    /// </summary>
    public static string UnitsLabelFor(string address)
    {
        string[] p = Segments(address);
        if (p.Length >= 5 && p[1] == "Cat Units") return LabelPrefix + "cat-" + p[2] + "-" + p[3];
        if (p.Length >= 4 && p[1] == "Enemy Units") return LabelPrefix + "enemy-" + EnemyBucket(p[2]);
        // DogeBases / CatBases / Projectiles 三类加起来 3.5MB，按类合并就够，不值得再拆。
        if (p.Length >= 2) return LabelPrefix + Sanitize(p[1]);
        return LabelPrefix + "misc";
    }

    /// <summary>
    /// 敌人按编号前 3 位归桶（e800 -> enemy-e80），180 个敌人收成约 25 包。
    ///
    /// 一敌一包时平均只有 58KB——Web 上一次请求的头部 + TLS + RTT 占用跟这个载荷已经同量级，
    /// 拆到这个粒度是白付请求开销。而且访问模式本来就是批量的：一关会召一整批敌人，
    /// 桶基本会被整个用掉，过度拉取很少。归桶后平均约 400KB，正好落在合理区间。
    ///
    /// 取前 3 位而不是前 2 位：编号高度集中（e0 有 97 个、e1 有 62 个），
    /// 按前 2 位会打出 5.6MB 的巨包，反而害了只用其中一只的关卡。
    /// 前 3 位同时保住了局部性——同章节的敌人编号通常相邻，会落进同一个桶。
    /// </summary>
    private static string EnemyBucket(string code)
    {
        string s = Sanitize(code);
        return s.Length <= EnemyBucketKeyLength ? s : s.Substring(0, EnemyBucketKeyLength);
    }

    /// <summary>
    /// 地图按章节（编号首位）归桶；剧情立绘按角色分，其余按顶层目录分。
    /// </summary>
    public static string VisualsLabelFor(string address)
    {
        string[] p = Segments(address);
        if (p.Length >= 3 && p[0] == "Background" && p[1] == "Maps")
        {
            // 纯数字的是地图本体；MapFillingShader / MapFillingMaterial / MapGradingSettings
            // 是所有地图共用的，单独成包才不会被复制进 96 个 bundle。
            return IsAllDigits(p[2]) ? LabelPrefix + "map-" + MapBucket(p[2]) : LabelPrefix + "map-shared";
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

    /// <summary>
    /// 地图按编号首位归桶（605 -> map-6），93 张地图收成 10 包，最大的桶 20 张约 2.9MB。
    ///
    /// 一敌一包那套理由这里也成立（单张平均 143KB，偏细），但更关键的是局部性：
    /// 编号首位就是章节，玩家在一章里会连着打十几关，桶下载一次之后同章其余关卡都不再产生请求。
    /// 代价是只进去看一关也要拉整章，这个是有意接受的——地图是有限内容，不像猫咪会一直加，
    /// 用它换出来的余量正好留给后面新增的单位。
    /// 要恢复「一图一包」把这里直接 return id 即可，运行时地址不受影响。
    /// </summary>
    private static string MapBucket(string id)
    {
        return string.IsNullOrEmpty(id) ? "misc" : id.Substring(0, 1);
    }

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
