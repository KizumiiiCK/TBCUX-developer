# TBCX — Assets

> Unity 塔防 / 实时战斗游戏的**核心数据与逻辑仓库**。
> 本仓库的 git 根目录就是 Unity 工程的 `Assets/` 文件夹。版本化 **`Script/`**（逻辑）、**`Resources/`**（仍走 Resources 加载的数据）、**`Bundled/`**（Addressables 源资源）和 **`AddressableAssetsData/`**（Addressables 分组与目录配置）。

---

## 目录

- [项目概览](#项目概览)
- [仓库范围与策略](#仓库范围与策略)
- [环境与依赖](#环境与依赖)
- [目录结构](#目录结构)
  - [`Script/` — 游戏逻辑](#script--游戏逻辑)
  - [`Resources/` — Resources 加载的数据](#resources--resources-加载的数据)
  - [`Bundled/` — Addressables 源资源](#bundled--addressables-源资源)
  - [`AddressableAssetsData/` — Addressables 配置](#addressableassetsdata--addressables-配置)
- [核心数据模型](#核心数据模型)
- [单位数据格式](#单位数据格式)
- [关卡与章节](#关卡与章节)
- [本地化](#本地化)
- [存档系统](#存档系统)
- [Addressables 约定](#addressables-约定)
- [未纳入版本管理的内容](#未纳入版本管理的内容)
- [克隆与首次运行](#克隆与首次运行)
- [Git 工作流约定](#git-工作流约定)

---

## 项目概览

TBCX 是一款 2D 横版**塔防 / 出兵对战**游戏：玩家部署"猫单位（Cat Units）"迎击成波来袭的"敌方单位（Enemy Units）"，通过击退（Knockback）、属性克制、技能与被动效果推进关卡，最终摧毁敌方基地。游戏包含多章节世界地图、单位图鉴、抽卡、云端存档等系统。

核心玩法元素：

- **单位**：猫单位 / 敌方单位，各带血量、攻击、射程、击退、费用、冷却等战斗参数。
- **属性与职业**：`Traits`（红/浮/黑/金属/天使/异星/僵尸/恶魔…）、`SubTraits`、`Careers`（战士/防御/法师/辅助/技巧）构成克制关系。
- **效果器（Effectors）**：中毒、诅咒、减速、定身、撕裂、束缚、虚弱、死亡标记等状态效果。
- **关卡**：由 `LevelData` ScriptableObject 驱动，定义基地血量、地图尺寸、敌人召唤序列、奖励、战斗光环（后处理）等。

运行时加载分两路：

- **Resources**：关卡、UI、特效、本地化表、音效 mixer 等，仍用 `Resources.Load`。
- **Addressables**：单位、BGM、字体/视觉、部分背景与 CG，经 `BundledAddressables` 按地址加载。源文件在 `Bundled/`。

---

## 仓库范围与策略

本仓库采用**白名单式 `.gitignore`**：默认忽略一切，仅纳入：

| 目录 | 作用 |
|---|---|
| `Script/` | 全部 C# 游戏逻辑 |
| `Resources/` | 仍走 Resources 的数据与资源 |
| `Bundled/` | Addressables 源资源（单位数据在这里） |
| `AddressableAssetsData/` | 分组、地址、平台 content state |

由于工程体积增长导致 git 传输困难，**大体积的外围媒体资源被刻意排除**（详见 [未纳入版本管理的内容](#未纳入版本管理的内容)）。这些文件依然存在于本地磁盘，只是不进行版本管理——它们不影响游戏逻辑与核心数据的正确性。

**不版本化 Addressables 构建产物**（`Library/com.unity.addressables`、Player 包内的 bundle）。构建是本机 / 导出步骤，不是源文件。

> ⚠️ 因此本仓库**不是**一个可以直接 clone 就完整运行的独立工程；它是核心数据 / 逻辑层。`Packages/`、`ProjectSettings/`、场景、Spine 以及字体/音频/CG 等需另行准备（见下文）。

---

## 环境与依赖

| 项目 | 版本 |
|---|---|
| Unity 编辑器 | **2022.3.60f1c1**（LTS） |
| 渲染 | 内置管线 + Post Processing 3.4 |

关键 Unity 包（来自 `Packages/manifest.json`，不在本仓库内）：

- `com.unity.addressables`（当前随 Localization 引入，约 1.22.2）— 单位 / BGM / 视觉加载
- `com.unity.localization` 1.5.3 — 多语言本地化
- `com.unity.textmeshpro` 3.0.7 — 文本渲染（依赖 SDF 字体资源）
- `com.unity.timeline` 1.7.6 — 过场 / 演出
- `com.unity.postprocessing` 3.4.0 — 战斗光环等视觉效果
- `com.unity.feature.2d` / `com.unity.feature.mobile` — 2D 与移动端
- Spine（`spine-csharp` / `spine-unity`）— 骨骼动画（工程内以 `.csproj` 形式存在）
- Supabase（通过 `UnityWebRequest` 直连 REST API）— 云端存档

目标平台：**Windows** 与 **Android**。两套 Addressables 产物不通用，需各构建一次。

---

## 目录结构

```
Assets/                          ← git 根目录
├── .gitignore
├── README.md                    ← 本文件
├── Script/                      ← 全部 C# 游戏逻辑
├── Resources/                   ← Resources.Load 的数据（关卡、UI、特效、本地化…）
├── Bundled/                     ← Addressables 源资源（单位、BGM、视觉…）
└── AddressableAssetsData/       ← Addressables 分组与 catalog 配置
```

### `Script/` — 游戏逻辑

| 目录 | 内容 | 说明 |
|---|---|---|
| **`Characters/`** | 角色系统（~22 个脚本） | 单位的核心运行时逻辑 |
| **`System/`** | 系统层 | 存档、本地化、UI 主控、云同步、剧情、`BundledAddressables` |
| **`UI/`** | 界面控制 | 各面板、图鉴、装备、抽卡、地图等 |
| **`GameMain/`** | 主玩法 | 效果器、被动技能、ScriptableObject 定义 |
| **`Editor/`** | 编辑器扩展工具 | 仅编辑器下使用 |
| **`Color/` `UV/`** | 着色 / 贴图工具 | |
| **`testificates/`** | 测试脚本 | 开发期临时测试 |

**`Characters/` 关键脚本**（`Character` 是 `abstract partial class`，按职责拆分为多个部分类）：

- `CharacterMain.cs` — 角色主体，战斗参数、属性、能力定义
- `CharacterCombat.cs` — 战斗行为
- `CharacterLifeCycle.cs` — 生命周期
- `CharacterTargetManager.cs` (+ `_Integration`) — 目标选取
- `CharacterPassive.cs` — 被动技能
- `CatBase.cs` / `DogeBase.cs` — 我方 / 敌方基地
- `CatCharacter.cs` / `EnemyCharacter.cs` — 猫 / 敌方单位实体
- `WaveUnit.cs` / `SurgeUnit.cs` / `ProjectileUnit.cs` / `CannonUnit.cs` — 攻击类型（波动 / 冲击波 / 投射物 / 炮击）
- `CharDataStructure.cs` — `Traits` / `SubTraits` / `Careers` / `ATKInfo` 等数据结构
- `EmotionUX.cs` — 单位情绪表现

**`GameMain/`**：

- `ScriptableObjects/LevelData.cs` — 关卡定义（见 [核心数据模型](#核心数据模型)）
- `ScriptableObjects/UpgradeInfo.cs` — 升级信息
- `effector/` — 状态效果：`Toxic`（毒）、`Curse`（诅咒）、`Slow`（减速）、`Stop`（定身）、`Lacerate`（撕裂）、`Wrap`（束缚）、`Weaken`（虚弱）、`DeathMark`（死亡标记）、`BuffInstaller`
- `PassiveSkills/PassiveEditor.cs` — 被动技能编辑

**`System/` 亮点**：

- Addressables：`BundledAddressables.cs` — 同步 / 异步按地址加载
- 音频：`BGMTool.cs` — 从 BGM 组加载 clip，写入场景中名为 `BGM` 的 AudioSource
- 存档：`SaveSystem.cs`、`GenericSaveSystem.cs`、`SupabaseSaveRemote.cs`、`SupabaseSaveUploader.cs`、`UserInfoLocalStore.cs`
- 账户：`UserCreateAccountPage` / `UserLoginCheckPage` / `UserRestoreAccountPage` / `UserDeleteAccountPage` / `UserUploadAccountPage`、`TransferCodeRules.cs`
- 本地化：`LocaleSelect.cs`
- 剧情 / 演出：`GamePlot.cs`、`DialoguePortraitCatalog.cs`、`Chatbox.cs`、`SpineAnimationEventController.cs`
- 运行时：`WorldTimeService.cs`、`CheckInSystem.cs`、`PoolSettings.cs`、`AnimationDecrypter.cs`

### `Resources/` — Resources 加载的数据

单位主体已迁到 `Bundled/Units/`。这里留下关卡、UI、特效，以及少量测试单位。

| 目录 | 内容 |
|---|---|
| **`LevelData/`** | 关卡、章节、敌人配置、地图 |
| **`Effects/`** | 战斗特效 prefab / 材质 |
| **`kennyui/`** | UI 素材（边框、控件图） |
| **`EAIcons/`** | 图标 |
| **`Reward/`** | 奖励图标 |
| **`UI/`** | UI prefab / 资源 |
| **`Localization/`** | 多语言表（Localization 包也会生成 Addressables 组） |
| **`emoji/`** | 表情 |
| **`Pools/`** | 抽卡池图标与配置 |
| **`Music/`** | `AudioMixer.mixer`；音效本体已排除 |
| **`Units/`** | 仅剩 `Forbiden/`、`testers/` 与若干运行时 prefab |
| **`Background/`** | 主菜单等仍走 Resources 的背景 |

### `Bundled/` — Addressables 源资源

| 目录 | Addressables 组 | 说明 |
|---|---|---|
| **`Units/`** | Units | 猫 / 敌 / 基地 / 投射物。**核心数据，必须进 git** |
| **`Music/BGM/`** | BGM | 背景音乐。clip 本体已排除，组配置仍要进 git |
| **`System/fonts/`** | Visuals | 字体与 TMP SDF。本体已排除 |
| **`Background/`** | Visuals | 战斗特效 prefab、地图 shader / 材质；**图片已排除** |
| **`CG/`** | Visuals | shader / 材质；**插画已排除** |
| **`DialogueImage/`** | Visuals | `DialoguePortraitSettings.asset`；**立绘已排除** |
| **`video/`** | Visuals | `*.renderTexture`；**mp4 已排除** |

**`Bundled/Units/` 子结构**：

- `Cat Units/` — 猫单位
- `Enemy Units/` — 敌方单位
- `CatBases/` / `DogeBases/` — 基地
- `Projectiles/` — 投射物

### `AddressableAssetsData/` — Addressables 配置

必须进 git，否则另一台机器没有地址与分组：

- `AddressableAssetSettings.asset` — 全局设置（当前 **不** 随 Player 自动打 Addressables）
- `AssetGroups/` — `Units` / `BGM` / `Visuals` / Localization-* 组及 Schema
- `Android/addressables_content_state.bin`、`Windows/addressables_content_state.bin` — 供该平台 **Update a Previous Build**

不要把 `Library/` 或导出包里的 bundle 提交进来。

---

## 核心数据模型

`LevelData`（`ScriptableObject`，可通过 `Create > ScriptableObjects > Level Data` 创建）定义一个关卡：

```csharp
public class LevelData : ScriptableObject
{
    public string levelName;
    public int gainXP = 0;
    public int BaseHealth = 1000;      // 我方基地血量
    public int mapSize = 6000;         // 地图长度
    public int maxEmenyCount = 50;     // 场上最大敌人数
    public int BackgroundID = 0;
    public int BaseImageID = 0;
    public string[] CombatEffect;      // 战斗特效
    public string[] Restriction;       // 出战限制
    public Aura[] CombatAura;          // 战斗光环（后处理：bloom/vignette/grading…）
    public EXstage exstage;
    public Reward[] rewardlist;        // 奖励表
    public EnemySummoner[] enemySummoners;  // 敌人召唤序列
}
```

`Character`（`abstract partial class`）的关键战斗字段：

- `Health`、`KB`（击退次数）、`Speed`、`Reload`、`DetectionRange`、`Cost`、`Cooldown`
- `ATKTypes` / `atkInfos[]` — 攻击类型与数值，`areaATK`（范围攻击）、`atkDuration`、`one_off`（单次攻击）
- `traits` / `subtraits` / `career` / `againstCareer` — 属性与职业克制
- `DRE`（伤害相关效果）、`characterEffects[]`、`characterAbilities[]`、`atkTypeResis[]`（抗性）

---

## 单位数据格式

每个单位的每个形态（tier）对应一个叶子目录，结构统一。以 `Bundled/Units/Cat Units/1/002/0/` 为例：

| 文件 | 作用 |
|---|---|
| `data.asset` | **单位数据**（战斗参数、属性、技能）— 核心 |
| `sprite.png` | 单位精灵图 |
| `icon_deploy.png` | 出战图标（敌方为 `enemy_icon.png`） |
| `mamodel.txt` | 模型 / 部件定义 |
| `imgcut.txt` | 精灵切片信息 |
| `maanim_idle.txt` | 待机动画 |
| `maanim_walk.txt` | 行走动画 |
| `maanim_attack.txt` | 攻击动画 |
| `maanim_kb.txt` | 击退动画 |

> `.txt` 动画 / 模型数据（`maanim_*` / `mamodel` / `imgcut`）与 `data.asset` 均为核心数据，随仓库版本化。单位精灵图 `sprite.png` / 图标同样保留。

敌方单位（`Enemy Units/`）结构一致，另有部分带 `uaunit.prefab`。Addressables 地址沿用旧 Resources 路径风格，例如 `Units/Enemy Units/e002/data`。BGM 使用短名，例如 `002`、`silent_love`。

---

## 关卡与章节

`Resources/LevelData/`：

- **`Chapters/`** — 章节世界：`World_I` / `World_II` / `World_III`、`Future_I`、`Dungeon`、`LEGEND`、`Dream_Pre`、`tobeupdated`，另有 `CPImages`（章节按钮图）。
- **`LevelEnemyData/`** — 各关卡的敌人配置（波次、难度 `dif0` 等）。
- **`Maps/`** — 地图 prefab。

---

## 本地化

基于 Unity Localization 包，支持语言：

- 简体中文 `zh-CN`
- 英语（美国）`en-US`
- 日语（日本）`ja-JP`

本地化表（`Resources/Localization/`）：`UnitNames`（单位名）、`LevelNames`（关卡名，含 `ChapterName`）、`Dialogues`（对话）、`Descriptions`（描述）、`UI Elements`（界面文案）、`BaseMessages`、`BontiqueItems`。

Localization 会在 `AddressableAssetsData` 下生成 `Localization-*` 组，这些组文件也要进 git。

---

## 存档系统

采用**本地 + 云端**双轨：

- **本地**：`SaveSystem`（JSON 序列化到 `streamingAssetsPath`）、`GenericSaveSystem`、`UserInfoLocalStore`。
- **云端**：`SupabaseSaveRemote` / `SupabaseSaveUploader` 通过 `UnityWebRequest` 直连 Supabase REST API，带重试机制，支持账户创建 / 登录 / 恢复 / 删除，以及转移码（`TransferCodeRules`）。

> Supabase 的 URL / Key 在运行时通过 `SupabaseSaveRemote.Initialize(...)` 注入，不硬编码在数据中。

---

## Addressables 约定

- 组：`Units`、`BGM`、`Visuals`、若干 `Localization-*`。BGM 组必须 **Include in Build**。
- `Build Addressables with Player Build` 为关闭：打 Player **不会**自动打 Addressables。先对该平台做 Addressables 构建，再打 exe / apk。
- 一次 Addressables Build 会打当前平台**所有**纳入构建的组，不必按资源或按组各点一次。
- Windows 与 Android 产物不通用。
- 本地 Catalog（无远程）。新资源必须随新的 Player 包发出。
- 已有该平台 `addressables_content_state.bin` 时可用 **Update a Previous Build**；改组设置、改旧地址、首次构建或 Update 失败时用 **New Build**。
- 编辑器 Play Mode 若使用 Asset Database，试玩可以不先 Build；真机 / 独立播放器必须先 Build。

---

## 未纳入版本管理的内容

以下大体积外围媒体被 `.gitignore` 排除。**它们不影响游戏逻辑与核心数据的正确性**，文件仍保存在本地磁盘，只是不进行版本管理：

| 类别 | 路径 | 说明 |
|---|---|---|
| 字体 | `Bundled/System/**` | TTF/OTF/TTC 及 TextMeshPro SDF |
| 音乐 | `Bundled/Music/**` 的 `*.mp3` `*.ogg` | clip 不进 git；mixer 在 `Resources/Music/` |
| 音效 | `Resources/Music/**` 的 `*.mp3` `*.ogg` | ✅ 保留 `AudioMixer.mixer` |
| 过场视频 | `Bundled/video/**` 的 `*.mp4` | ✅ 保留 `*.renderTexture` |
| CG 插画 | `Bundled/CG/**` 的图片 | ✅ 保留 shader / 材质 |
| 对话立绘 | `Bundled/DialogueImage/**` 的图片 | ✅ 保留 `DialoguePortraitSettings.asset` |
| 战斗背景图 | `Bundled/Background/**` 的图片 | ✅ 保留 prefab / shader / 材质 / 脚本 |

**保留在库中的核心数据**：`Bundled/Units/` 的 `data.asset` + 精灵图 + 动画 txt、关卡 / 敌人数据、本地化、Addressables 组配置，以及上述各类配置资源。

另外不在本仓库内：`Packages/`、`ProjectSettings/`、`Scenes/`、Spine、Plugins、Player 输出、Addressables 打出来的 bundle。

> 若需完整可运行工程，请从项目媒体存储另行获取被排除的媒体，放回 `Bundled/`（及 `Resources/Music/SE`）对应目录。Unity 会依据保留的 `.meta` 恢复引用。

---

## 克隆与首次运行

本仓库对应 Unity 工程的 `Assets/` 目录。要在完整工程中使用：

1. 准备一个 Unity **2022.3.60f1c1** 工程（含 `Packages/`、`ProjectSettings/` —— 这些不在本仓库内）。确保已有 Addressables 与 Localization 包。
2. 将本仓库 clone 为该工程的 `Assets/` 目录（覆盖 `Script/`、`Resources/`、`Bundled/`、`AddressableAssetsData/`）。
3. 从媒体存储补齐 [被排除的媒体](#未纳入版本管理的内容)。
4. 用 Unity 打开工程，等待导入完成。
5. 切到目标平台（Windows 或 Android），做一次 Addressables **New Build**（或已有 content state 时 Update）。
6. 再打 Player。

> 字体 SDF 被排除时，TextMeshPro 可能显示为 □；把字体放回 `Bundled/System/fonts/` 后重建 Visuals 所在的 Addressables 即可。

---

## Git 工作流约定

- **仅**提交白名单目录：`Script/`、`Resources/`、`Bundled/`、`AddressableAssetsData/`。不要 `git add -A` 整个 `Assets/`。
- 新增 Addressable 资源：放进 `Bundled/` 对应组，勾选 Addressable，**连同** `AddressableAssetsData/AssetGroups/` 的组文件一起提交。
- 大体积媒体（BGM clip、字体、CG、立绘、背景图、视频）放到项目媒体存储，不要进本仓库。
- Unity 的 `.meta` 必须随对应资源一同提交。
- 提交前确保 Unity 已完成导入。
- Addressables Build 产物不要提交；`addressables_content_state.bin` 要提交。
- 单位 / 关卡 / 脚本改动与「结构迁移」不要混在同一笔提交里。
