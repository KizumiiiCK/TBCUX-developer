# TBCX — Assets

> Unity 塔防 / 实时战斗游戏的**核心数据与逻辑仓库**。
> 本仓库的 git 根目录就是 Unity 工程的 `Assets/` 文件夹，仅版本化 **`Resources/`**（游戏数据）与 **`Script/`**（游戏逻辑）两大目录，以保持仓库轻量、传输快速。

---

## 目录

- [项目概览](#项目概览)
- [仓库范围与策略](#仓库范围与策略)
- [环境与依赖](#环境与依赖)
- [目录结构](#目录结构)
  - [`Script/` — 游戏逻辑](#script--游戏逻辑)
  - [`Resources/` — 游戏数据与资源](#resources--游戏数据与资源)
- [核心数据模型](#核心数据模型)
- [单位数据格式](#单位数据格式)
- [关卡与章节](#关卡与章节)
- [本地化](#本地化)
- [存档系统](#存档系统)
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

---

## 仓库范围与策略

本仓库采用**白名单式 `.gitignore`**：默认忽略一切，仅纳入 `Resources/` 与 `Script/`。

由于工程体积增长导致 git 传输困难，**大体积的外围媒体资源被刻意排除**（详见 [未纳入版本管理的内容](#未纳入版本管理的内容)）。这些文件依然存在于本地磁盘，只是不进行版本管理——它们不影响游戏逻辑与数据的正确性。

> ⚠️ 因此本仓库**不是**一个可以直接 clone 就完整运行的独立工程；它是核心数据 / 逻辑层。字体、音频、CG、背景图等需另行获取（见下文）。

---

## 环境与依赖

| 项目 | 版本 |
|---|---|
| Unity 编辑器 | **2022.3.60f1c1**（LTS） |
| 渲染 | 内置管线 + Post Processing 3.4 |

关键 Unity 包（来自 `Packages/manifest.json`，不在本仓库内）：

- `com.unity.localization` 1.5.3 — 多语言本地化
- `com.unity.textmeshpro` 3.0.7 — 文本渲染（依赖 SDF 字体资源）
- `com.unity.timeline` 1.7.6 — 过场 / 演出
- `com.unity.postprocessing` 3.4.0 — 战斗光环等视觉效果
- `com.unity.feature.2d` / `com.unity.feature.mobile` — 2D 与移动端
- Spine（`spine-csharp` / `spine-unity`）— 骨骼动画（工程内以 `.csproj` 形式存在）
- Supabase（通过 `UnityWebRequest` 直连 REST API）— 云端存档

---

## 目录结构

```
Assets/                     ← git 根目录
├── .gitignore
├── README.md               ← 本文件
├── Resources/              ← 游戏数据与运行时加载的资源
└── Script/                 ← 全部 C# 游戏逻辑
```

### `Script/` — 游戏逻辑

| 目录 | 内容 | 说明 |
|---|---|---|
| **`Characters/`** | 角色系统（~22 个脚本） | 单位的核心运行时逻辑 |
| **`System/`** | 系统层（~45 个脚本） | 存档、本地化、UI 主控、云同步、剧情等 |
| **`UI/`** | 界面控制（~41 个脚本） | 各面板、图鉴、装备、抽卡、地图等 |
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

- 存档：`SaveSystem.cs`、`GenericSaveSystem.cs`、`SupabaseSaveRemote.cs`、`SupabaseSaveUploader.cs`、`UserInfoLocalStore.cs`
- 账户：`UserCreateAccountPage` / `UserLoginCheckPage` / `UserRestoreAccountPage` / `UserDeleteAccountPage` / `UserUploadAccountPage`、`TransferCodeRules.cs`
- 本地化：`LocaleSelect.cs`
- 剧情 / 演出：`GamePlot.cs`、`DialoguePortraitCatalog.cs`、`Chatbox.cs`、`SpineAnimationEventController.cs`
- 运行时：`WorldTimeService.cs`、`CheckInSystem.cs`、`PoolSettings.cs`、`AnimationDecrypter.cs`

### `Resources/` — 游戏数据与资源

| 目录 | 文件数（约） | 内容 |
|---|---|---|
| **`Units/`** | ~18,400 | 全部单位数据（猫 / 敌 / 基地 / 投射物），**核心** |
| **`LevelData/`** | ~1,050 | 关卡、章节、敌人配置、地图 |
| **`Effects/`** | ~740 | 战斗特效 prefab / 材质 |
| **`kennyui/`** | ~660 | UI 素材（边框、控件图） |
| **`EAIcons/`** | ~210 | 图标 |
| **`Reward/`** | ~165 | 奖励图标 |
| **`UI/`** | ~130 | UI prefab / 资源 |
| **`Localization/`** | ~107 | 多语言表 |
| **`emoji/`** | ~85 | 表情 |
| **`Pools/`** | ~56 | 抽卡池图标与配置 |
| **`Background/`** | ~26 | 战斗背景（prefab / shader / 材质，**图片已排除**） |
| `Music/` `video/` `CG/` `DialogueImage/` `System/` | 少量 | 仅保留配置资源，**媒体本体已排除**（见下） |

**`Units/` 子结构**：

- `Cat Units/` (~14,300) — 猫单位
- `Enemy Units/` (~3,400) — 敌方单位
- `CatBases/` / `DogeBases/` — 基地
- `Projectiles/` — 投射物
- `Forbiden/` / `testers/` — 特殊 / 测试单位

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

每个单位的每个形态（tier）对应一个叶子目录，结构统一。以 `Resources/Units/Cat Units/1/002/0/` 为例：

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

敌方单位（`Enemy Units/`）结构一致，另有部分带 `uaunit.prefab`。

---

## 关卡与章节

`Resources/LevelData/`：

- **`Chapters/`** — 章节世界：`World_I` / `World_II` / `World_III`、`Future_I`、`Dungeon`、`LEGEND`、`Dream_Pre`、`tobeupdated`，另有 `CPImages`（章节按钮图）。
- **`LevelEnemyData/`**（~914） — 各关卡的敌人配置（波次、难度 `dif0` 等）。
- **`Maps/`** — 地图 prefab。

---

## 本地化

基于 Unity Localization 包，支持语言：

- 简体中文 `zh-CN`
- 英语（美国）`en-US`
- 日语（日本）`ja-JP`

本地化表（`Resources/Localization/`）：`UnitNames`（单位名）、`LevelNames`（关卡名，含 `ChapterName`）、`Dialogues`（对话）、`Descriptions`（描述）、`UI Elements`（界面文案）、`BaseMessages`、`BontiqueItems`。

---

## 存档系统

采用**本地 + 云端**双轨：

- **本地**：`SaveSystem`（JSON 序列化到 `streamingAssetsPath`）、`GenericSaveSystem`、`UserInfoLocalStore`。
- **云端**：`SupabaseSaveRemote` / `SupabaseSaveUploader` 通过 `UnityWebRequest` 直连 Supabase REST API，带重试机制，支持账户创建 / 登录 / 恢复 / 删除，以及转移码（`TransferCodeRules`）。

> Supabase 的 URL / Key 在运行时通过 `SupabaseSaveRemote.Initialize(...)` 注入，不硬编码在数据中。

---

## 未纳入版本管理的内容

以下大体积外围媒体被 `.gitignore` 排除。**它们不影响游戏逻辑与数据的正确性**，文件仍保存在本地磁盘，只是不进行版本管理，也已从 git 历史中清除以缩减仓库体积：

| 类别 | 路径 | 说明 |
|---|---|---|
| 字体 | `Resources/System/**` | TTF/OTF/TTC 及 TextMeshPro SDF 图集（含一个 ~135MB 图集） |
| 音乐 / 音效 | `Resources/Music/**` 的 `*.mp3` `*.ogg` | ✅ 保留 `AudioMixer.mixer` |
| 过场视频 | `Resources/video/**` 的 `*.mp4` | ✅ 保留 `*.renderTexture` |
| CG 插画 | `Resources/CG/**` 的图片 | ✅ 保留 shader / 材质 |
| 对话立绘 | `Resources/DialogueImage/**` 的图片 | ✅ 保留 `DialoguePortraitSettings.asset` |
| 战斗背景 | `Resources/Background/**` 的图片 | ✅ 保留 prefab / shader / 材质 / 脚本 |

**保留在库中的核心数据**：单位 `data.asset` + 精灵图、关卡 / 敌人数据、本地化、以及上述各类配置资源（`.mixer` / `.renderTexture` / shader / 材质 / prefab）。

> 若需完整可运行工程，请从项目媒体存储另行获取上述资源，放回对应目录即可（Unity 会依据保留的 `.meta` 恢复引用）。

---

## 克隆与首次运行

本仓库对应 Unity 工程的 `Assets/` 目录。要在完整工程中使用：

1. 准备一个 Unity **2022.3.60f1c1** 工程（含 `Packages/`、`ProjectSettings/` —— 这些不在本仓库内）。
2. 将本仓库 clone 为该工程的 `Assets/` 目录（或把 `Resources/`、`Script/` 覆盖进去）。
3. 从媒体存储补齐 [被排除的媒体](#未纳入版本管理的内容)。
4. 用 Unity 打开工程，等待导入完成。

> 由于字体 SDF 图集被排除，缺失时 TextMeshPro 文本可能无法正确显示；补齐 `Resources/System/**` 后即可恢复。

---

## Git 工作流约定

- **仅**在 `Resources/` 与 `Script/` 下提交内容；其他目录默认被忽略。
- 新增媒体文件前，先确认其是否落在 `.gitignore` 的排除规则内——大体积媒体应放到项目媒体存储，而非本仓库。
- Unity 的 `.meta` 文件**必须**随对应资源一同提交，以保证 GUID 引用稳定。
- 提交前确保 Unity 已完成导入（避免 `.meta` 缺失或不一致）。
