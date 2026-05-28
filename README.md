# Cinderia_Mod_Item_Legacy

基于 BepInEx 和 Harmony 开发的 Cinderia 道具扩展 Mod。当前项目主要围绕新增道具、宝箱奖励选择、藏宝图掉落、上一局道具继承和技能选择刷新次数扩展。

## 功能概览

- `复制器` 道具：注入 1-4 级复制器道具，清空房间掉落奖励时有概率额外复制一份相同奖励，并附带额外道具格 Buff。
- 自选开箱：拦截大宝箱和中海盗宝箱开奖流程，保留原版随机品质逻辑，将随机具体道具改为玩家从候选池中选择。
- 藏宝图调整：统一 `藏宝图2`、`藏宝图3`、`藏宝图4` 的清场掉海盗宝箱逻辑，并支持配置最高级藏宝图的小/中/大宝箱概率。
- 上一局继承：结算时记录本局全部道具，下局进入第一个房间时弹窗让玩家选择一个继承。
- 技能选择刷新：在原版主动/被动技能选择刷新次数基础上额外增加配置次数。
- **调试功能**：
  - **F9 配置界面**：游戏中按 F9 打开配置窗口，可实时修改所有 mod 配置项并保存。
  - **F10 道具选择**：游戏中按 F10 打开道具选择窗口，列出所有最高级道具，点击即可在角色旁掉落。

## 项目结构

```text
Cinderia_Mod_Item_Legacy/
├─ Cinderia_Mod_Item_Legacy.cs       # 主插件、配置、道具注入、藏宝图、继承、刷新次数、Harmony 补丁
├─ ChestRewardSelection.cs           # 自选开箱流程和 IMGUI 选择界面
├─ DebugGUI.cs                       # F9 配置界面和 F10 道具选择界面
├─ Cinderia_Mod_Item_Legacy.csproj   # .NET Framework 4.7.2 项目文件
├─ Cinderia_Mod_Item_Legacy.slnx     # 解决方案入口
├─ Cinderia_Game/                    # junction 链接，指向游戏根目录，提供程序集引用与 DLL 部署目标（.gitignore 已忽略）
├─ Properties/AssemblyInfo.cs        # 程序集信息
├─ README.md                         # 项目说明
└─ DEVELOPMENT_RECORD.md             # 开发记录和后续维护说明
```

## 环境要求

- Windows
- Cinderia 游戏根目录
- BepInEx Unity Mono
- .NET SDK 或可用的 MSBuild/dotnet 构建环境

项目通过 `Cinderia_Game` junction 定位游戏目录，所有程序集引用与构建产物均基于该链接，项目本身无需放置在游戏目录之下。首次拉取项目后需要在项目根创建 junction：

```powershell
# 在项目根目录执行，把目标路径替换为实际游戏根目录
cmd /c mklink /J Cinderia_Game "C:\Programs\Steam\steamapps\common\Cinderia"
```

创建后，以下路径需要可访问：

- `Cinderia_Game/BepInEx/`（Harmony 与 BepInEx 程序集、`plugins/` 输出目录）
- `Cinderia_Game/Cinderia_Data/Managed/`（Unity 与游戏程序集）
- `Cinderia_Game/Assembly-CSharp/`（可选，用于查阅反编译源码；若不存在可用 ilspycmd 反编译生成）

项目目标框架为 `.NET Framework 4.7.2`，Debug 构建输出到：

```text
Cinderia_Game/BepInEx/plugins/Cinderia_Mod_Item_Legacy.dll
```

通过 junction 直接写入游戏目录，无需手动复制。

## 构建

在项目根目录执行：

```powershell
dotnet build .\Cinderia_Mod_Item_Legacy.csproj -t:Rebuild
```

构建成功后，DLL 会写入 `Cinderia_Game/BepInEx/plugins/`。启动游戏后可在 `Cinderia_Game/BepInEx/LogOutput.log` 中查看插件日志，日志前缀为 `Cinderia_Mod_Item_Legacy`。

## 游戏内快捷键

- **F9**：打开/关闭 Mod 配置界面
  - 实时修改所有配置项（复制器概率、自选开箱、继承、藏宝图、技能刷新等）
  - 配置项带中文名称、说明和生效时机提示
  - 点击"保存配置"按钮保存到配置文件
  - 界面尺寸：1000x700，字体清晰易读
  
- **F10**：打开/关闭道具选择界面
  - 按等级（白/绿/蓝/紫/橙）筛选所有道具
  - 网格布局显示道具卡片，带图标和名称
  - 点击道具卡片即可在角色旁掉落
  - 右侧显示道具详情（名称、稀有度、描述）
  - 包含 mod 新增的复制器道具

## 配置

BepInEx 会生成配置文件：

```text
Cinderia_Game/BepInEx/config/Cinderia_Mod_Item_Legacy.cfg
```

当前配置分组：

| 分组 | 键 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `Duplicator` | `启用` | `true` | 是否注入复制器 |
| `Duplicator` | `绿概率` | `0.20` | 绿色复制器复制房间奖励概率 |
| `Duplicator` | `蓝概率` | `0.40` | 蓝色复制器复制房间奖励概率 |
| `Duplicator` | `紫概率` | `0.60` | 紫色复制器复制房间奖励概率 |
| `Duplicator` | `橙概率` | `0.80` | 橙色复制器复制房间奖励概率 |
| `ChestSelection` | `启用` | `true` | 是否启用自选开箱 |
| `LegacyInheritance` | `启用` | `true` | 是否启用上一局道具继承 |
| `LegacyInheritance` | `候选道具列表` | 空 | 内部记录上一局候选道具，英文逗号分隔，也可手动修改 |
| `TreasureMap` | `藏宝图4_小宝箱概率` | `0.2` | 最高级藏宝图生成小海盗宝箱概率/权重 |
| `TreasureMap` | `藏宝图4_中宝箱概率` | `0.4` | 最高级藏宝图生成中海盗宝箱概率/权重 |
| `TreasureMap` | `藏宝图4_大宝箱概率` | `0.4` | 最高级藏宝图生成大海盗宝箱概率/权重 |
| `SkillSelection` | `额外刷新次数` | `0` | 主动/被动技能选择界面在原版基础上额外增加的刷新次数 |

`LegacyInheritance.候选道具列表` 示例：

```ini
候选道具列表 = 复制器4,藏宝图4,宽松的腰带
```

## 开发规范

- 修改前先阅读对应原版反编译源码，确认类型、方法、字段仍存在。
- Harmony 补丁优先挂在语义稳定的方法上，避免依赖过深的 UI 内部状态。
- 反射字段使用 `AccessTools.FieldRefAccess` 时，需要在游戏更新后重新核对字段名。
- 修改原版流程的补丁需要保留回退路径，避免异常中断游戏流程。
- 配置项新增后，需要同步更新 README 和开发记录。
- 代码、注释、文档统一使用简体中文。

## Git 规范

提交信息建议使用推荐提交规范：

```text
feat: 新增功能
fix: 修复问题
docs: 文档更新
refactor: 重构但不改变行为
chore: 构建、忽略文件、工程配置等维护项
```

示例：

```text
feat: 添加技能选择额外刷新次数配置
fix: 修正藏宝图四宝箱概率配置
docs: 整理mod开发记录
```

提交前建议执行：

```powershell
git status --short
dotnet build .\Cinderia_Mod_Item_Legacy.csproj -t:Rebuild
```

## 维护提示

游戏更新后优先核对以下 Harmony 补丁目标：

- `Rogue.Items.道具宝箱大.获得奖励`
- `Rogue.WavesManager.CreateReward`
- `Rogue.Units.Character.角色创建时`
- `Rogue.Units.Character.角色出门时`
- `Rogue.Units.Character.自杀重置回老家`
- `Rogue.房间_入口.进入新房间`
- `Rogue.Buffs.Trigger.战斗结算时.清场时`
- `Rogue.Buffs.Trigger.战斗结算时包括继续游戏.清场时`

以及 mod 直接调用的关键工具方法（游戏曾经改名过这些 API）：

- `Game.获取多语言_MagicCard名称(string id)` / `Game.获取多语言_MagicCard描述(string id)`
- `MagicCard_Manager.id找data` / `MagicCard_Manager.Inst.放到一个空槽位_返回魔卡`
- `Game.实例化预制体` / `Game.获取一个固定随机数float` / `Game.获取一个固定随机数bool`

如果运行时功能失效，先看 `Cinderia_Game/BepInEx/LogOutput.log`，再对照 `DEVELOPMENT_RECORD.md` 的排查清单。
