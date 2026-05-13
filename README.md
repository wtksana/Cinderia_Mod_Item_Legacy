# Cinderia_Mod_Item_Legacy

基于 BepInEx 和 Harmony 开发的 Cinderia 道具扩展 Mod。当前项目主要围绕新增道具、宝箱奖励选择、藏宝图掉落、上一局道具继承和技能选择刷新次数扩展。

## 功能概览

- `复制器` 道具：注入 1-4 级复制器道具，清空房间掉落奖励时有概率额外复制一份相同奖励，并附带额外道具格 Buff。
- 自选开箱：拦截大宝箱和中海盗宝箱开奖流程，保留原版随机品质逻辑，将随机具体道具改为玩家从候选池中选择。
- 藏宝图调整：统一 `藏宝图2`、`藏宝图3`、`藏宝图4` 的清场掉海盗宝箱逻辑，并支持配置最高级藏宝图的小/中/大宝箱概率。
- 上一局继承：结算时记录本局全部道具，下局进入第一个房间时弹窗让玩家选择一个继承。
- 技能选择刷新：在原版主动/被动技能选择刷新次数基础上额外增加配置次数。

## 项目结构

```text
Cinderia_Mod_Item_Legacy/
├─ Cinderia_Mod_Item_Legacy.cs       # 主插件、配置、道具注入、藏宝图、继承、刷新次数、Harmony 补丁
├─ ChestRewardSelection.cs           # 自选开箱流程和 IMGUI 选择界面
├─ Cinderia_Mod_Item_Legacy.csproj   # .NET Framework 4.7.2 项目文件
├─ Cinderia_Mod_Item_Legacy.slnx     # 解决方案入口
├─ Properties/AssemblyInfo.cs        # 程序集信息
├─ README.md                         # 项目说明
└─ DEVELOPMENT_RECORD.md             # 开发记录和后续维护说明
```

## 环境要求

- Windows
- Cinderia 游戏根目录
- BepInEx Unity Mono
- .NET SDK 或可用的 MSBuild/dotnet 构建环境
- 游戏反编译源码目录：`../Assembly-CSharp`
- 游戏程序集引用目录：`../Cinderia_Data/Managed`

项目目标框架为 `.NET Framework 4.7.2`，Debug 构建输出到：

```text
../BepInEx/plugins/Cinderia_Mod_Item_Legacy.dll
```

## 构建

在游戏根目录执行：

```powershell
$env:DOTNET_CLI_HOME = (Resolve-Path '.\Cinderia_Mod_Item_Legacy').Path
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
dotnet build '.\Cinderia_Mod_Item_Legacy\Cinderia_Mod_Item_Legacy.csproj' -t:Rebuild
```

构建成功后，DLL 会写入 `BepInEx/plugins/`。启动游戏后可在 `BepInEx/LogOutput.log` 中查看插件日志，日志前缀为 `Cinderia_Mod_Item_Legacy`。

## 配置

BepInEx 会生成配置文件：

```text
BepInEx/config/Cinderia_Mod_Item_Legacy.cfg
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
dotnet build '.\Cinderia_Mod_Item_Legacy.csproj' -t:Rebuild
```

## 维护提示

游戏更新后优先核对这些补丁点：

- `Rogue.Items.道具宝箱大.获得奖励`
- `Rogue.WavesManager.CreateReward`
- `Rogue.Units.Character.角色创建时`
- `Rogue.Units.Character.角色出门时`
- `Rogue.Units.Character.自杀重置回老家`
- `Rogue.房间_入口.进入新房间`
- `Rogue.Buffs.Trigger.战斗结算时.清场时`
- `Rogue.Buffs.Trigger.战斗结算时包括继续游戏.清场时`

如果运行时功能失效，先看 `BepInEx/LogOutput.log`，再对照 `DEVELOPMENT_RECORD.md` 的排查清单。
