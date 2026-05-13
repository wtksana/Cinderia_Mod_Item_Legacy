# Cinderia_Mod_Item_Legacy 开发记录

本文档记录当前 Mod 的功能脉络、关键补丁点、配置项和维护注意事项，方便后续继续开发或在游戏更新后排查失效点。

## 当前状态

- 插件入口：`Cinderia_Mod_Item_Legacy.Cinderia_Mod_Item_Legacy`
- BepInEx 插件标识：`Cinderia_Mod_Item_Legacy`
- 当前版本号：`1.1.0`
- 目标框架：`.NET Framework 4.7.2`
- Debug 输出：`../BepInEx/plugins/`
- 主要源码：
  - `Cinderia_Mod_Item_Legacy.cs`
  - `ChestRewardSelection.cs`

## 功能记录

### 复制器

目标：新增 1-4 级 `复制器` 道具，并提供额外房间奖励复制概率和额外道具格。

主要入口：

- `EnsureCustomDuplicatorItems`
- `EnsureCustomDuplicatorSlotBuffs`
- `UpdateDuplicatorItemDescriptions`
- `TryDuplicateRoomReward`
- `Patch_Duplicator_OnCreateReward`

实现方式：

- 使用 `藏宝图4` 作为模板克隆出 `复制器1` 到 `复制器4`。
- 注入对应 Buff：`复制器加格子一`、`复制器加格子二`、`复制器加格子三`、`复制器加格子四`。
- 挂载 `WavesManager.CreateReward` 的 `Postfix`，在房间奖励生成后按复制器概率额外生成一份奖励。
- 使用 `_creatingDuplicatedReward` 防止复制出来的奖励再次触发复制，避免递归。

相关配置：

- `Duplicator.启用`
- `Duplicator.绿概率`
- `Duplicator.蓝概率`
- `Duplicator.紫概率`
- `Duplicator.橙概率`

维护注意：

- 复制器依赖 `ExcelData.magicCards` 和 `ExcelData.buffs` 可写。
- 如果游戏更新后 `藏宝图4` 字段结构变化，需要重新核对克隆字段。
- 如果额外道具格异常，优先核对原版 `宽松的腰带` 对应 Buff 的脚本字段和 `scriptData` 语义。

### 自选开箱

目标：保留原版“先随机品质”逻辑，把“随机具体道具”改为“玩家从同品质候选池选择”。

主要入口：

- `Patch_ChestRewardSelection`
- `ChestRewardSelectionManager.执行自选开箱流程`
- `ChestRewardSelectionManager.构建候选上下文`
- `ChestRewardSelectionManager.获取候选道具池`
- `ChestRewardSelectionOverlay`

补丁点：

- `Rogue.Items.道具宝箱大.获得奖励`

支持对象：

- `道具宝箱大`
- `海盗宝箱中`

当前不支持：

- `海盗宝箱小`
- `海盗宝箱大`

实现要点：

- 读取 `道具宝箱大.指定宝箱等级`，保留指定等级宝箱逻辑。
- 未指定等级时使用原版 Drop 权重随机品质。
- 候选池基于 `MagicCard_Manager.Inst.剩余魔卡卡池`、已拾取基础名、互斥组、前置条件和角色颜色过滤。
- `复制器` 作为自定义候选道具额外放行，避免因为不在原版卡池里而不可选。
- 选择界面使用 IMGUI 绘制，支持中文字体、图标、详情面板。
- 如果选择界面异常或候选池为空，会回退为原版等权随机。

关键反射字段：

- `道具宝箱大.指定宝箱等级`
- `Rogue.Units.Unit.unitEvent`
- `可拾取物.isPicked`
- `可拾取物.主角拾取动作名`

维护注意：

- 原版 `道具宝箱大.获得奖励` 如果新增流程，需要同步检查 `播放开箱并掉落奖励` 是否漏掉新逻辑。
- 目前 `播放开箱并掉落奖励` 会执行 `WavesManager.RewardPicked()`，如果原版存档条件变化，需要重新核对。
- Unity IMGUI 布局必须保证 Layout/Repaint 分支控件数量一致，否则会出现 `GUILayoutGroup.GetNext` 异常。

### 藏宝图

目标：统一 `藏宝图2`、`藏宝图3`、`藏宝图4` 清场掉海盗宝箱逻辑，并让最高级藏宝图概率可配置。

主要入口：

- `ApplyTreasureMapTweaks`
- `TryHandleTreasureMapBattleClearReward`
- `ResolveTreasureMapRewardPrefab`
- `GetTreasureMapChestCreatePos`
- `应用藏宝图道具描述`
- `Patch_TreasureMap_BattleClearReward`
- `Patch_TreasureMap_BattleClearReward_IncludeContinue`

补丁点：

- `Rogue.Buffs.Trigger.战斗结算时.清场时`
- `Rogue.Buffs.Trigger.战斗结算时包括继续游戏.清场时`

当前默认概率：

| 道具 | 小宝箱 | 中宝箱 | 大宝箱 |
| --- | ---: | ---: | ---: |
| `藏宝图2` | `0.6` | `0.3` | `0.1` |
| `藏宝图3` | `0.4` | `0.4` | `0.2` |
| `藏宝图4` | 配置 | 配置 | 配置 |

相关配置：

- `TreasureMap.藏宝图4_小宝箱概率`
- `TreasureMap.藏宝图4_中宝箱概率`
- `TreasureMap.藏宝图4_大宝箱概率`

实现要点：

- 触发后调用 `trigger.设置cd()`，增加 `triggerCount`，并调用 `trigger.buff.道具亮一下(true)`。
- 宝箱位置以当前房间奖励位置为锚点，随机偏移后用 `MapUtils.ClampToNavMesh` 修正。
- 描述文本会按当前概率重写，`藏宝图4` 保留“打开宝箱时获得 1 点随机属性”的描述行。
- 当前随机使用 `Game.获取一个固定随机数float(buffId + "清场宝箱")`。

维护注意：

- 游戏更新后如果藏宝图概率失效，优先确认藏宝图 Buff 的 `data.id` 是否仍为 `藏宝图二`、`藏宝图三`、`藏宝图四`。
- 如果日志没有 `[藏宝图四]` 输出，说明补丁没有命中，应检查原版触发类是否又改名或改为其他 Trigger。
- 如果日志有输出但概率异常，检查 `ResolveTreasureMapRewardPrefab` 的随机流和配置读取。

### 上一局道具继承

目标：结算时记录本局全部道具，下局进入第一个房间时弹窗选择一个继承。

主要入口：

- `记录上一局继承候选`
- `尝试在首房间弹出上一局继承选择`
- `执行上一局继承选择流程`
- `获取上一局继承候选道具`
- `发放上一局继承道具`

补丁点：

- `Rogue.Units.Character.自杀重置回老家`
- `Rogue.房间_入口.进入新房间`

相关配置：

- `LegacyInheritance.启用`
- `LegacyInheritance.候选道具列表`

实现要点：

- 结算时读取 `MagicCard_Manager.Inst.道具列表`，排除 `项链`。
- 候选保存为英文逗号分隔，便于手动编辑。
- 读取时兼容旧版 JSON 数组格式，成功解析后会自动写回逗号分隔格式。
- 首房间判定使用 `FateManager.当前关卡数 == 0`，并排除 `Game.局外` 和老家地图。
- 发放时优先直接放入空槽，失败则生成 `换下来的魔卡` 掉落物。

维护注意：

- 如果进入第二个房间才弹窗，优先核对 `FateManager.当前关卡数` 的语义是否变化。
- 如果继承 `复制器` 后不可识别，先调用 `EnsureCustomDuplicatorItems` 确保自定义道具已注入。

### 技能选择额外刷新次数

目标：在原版主动/被动技能选择刷新次数基础上额外增加配置的次数。

主要入口：

- `获取额外技能刷新次数`
- `Patch_ExtraRefreshCount_OnCharacterLeaveHome`

补丁点：

- `Rogue.Units.Character.角色出门时`

相关配置：

- `SkillSelection.额外刷新次数`

实现要点：

- 原版在 `Character.角色出门时` 中写入 `Game.PlayerData.三选一刷新次数`。
- Mod 使用 `Postfix` 在原版最终写入后增加配置次数。
- 不挂 UI 面板入口，避免每次打开选择界面时重复叠加。

维护注意：

- 如果刷新次数无效，优先核对原版是否仍在 `Character.角色出门时` 写入 `Game.PlayerData.三选一刷新次数`。
- 如果刷新后次数显示异常，检查 `UI_三选一面板.进入选择界面` 和 `UI_二选一面板.进入选择界面` 是否改了刷新参数语义。

## Harmony 补丁清单

| 补丁类 | 原版目标 | 用途 |
| --- | --- | --- |
| `Patch_ChestRewardSelection` | `道具宝箱大.获得奖励` | 自选开箱 |
| `Patch_RecordLegacyInheritanceItemsOnRunEnd` | `Character.自杀重置回老家` | 记录上一局道具 |
| `Patch_ResetPerRunState` | `Character.角色创建时` | 新局状态重置、注入道具、应用描述 |
| `Patch_ExtraRefreshCount_OnCharacterLeaveHome` | `Character.角色出门时` | 追加技能选择刷新次数 |
| `Patch_LegacyInheritanceSelection_OnEnterRoom` | `房间_入口.进入新房间` | 首房间弹出继承选择 |
| `Patch_Duplicator_OnCreateReward` | `WavesManager.CreateReward` | 复制器额外复制房间奖励 |
| `Patch_TreasureMap_BattleClearReward` | `战斗结算时.清场时` | 藏宝图清场掉宝箱 |
| `Patch_TreasureMap_BattleClearReward_IncludeContinue` | `战斗结算时包括继续游戏.清场时` | 兼容新版藏宝图清场触发 |

## 配置项清单

| 分组 | 配置项 | 类型 | 默认值 |
| --- | --- | --- | --- |
| `Duplicator` | `启用` | `bool` | `true` |
| `Duplicator` | `绿概率` | `float` | `0.20` |
| `Duplicator` | `蓝概率` | `float` | `0.40` |
| `Duplicator` | `紫概率` | `float` | `0.60` |
| `Duplicator` | `橙概率` | `float` | `0.80` |
| `ChestSelection` | `启用` | `bool` | `true` |
| `LegacyInheritance` | `启用` | `bool` | `true` |
| `LegacyInheritance` | `候选道具列表` | `string` | 空 |
| `TreasureMap` | `藏宝图4_小宝箱概率` | `float` | `0.2` |
| `TreasureMap` | `藏宝图4_中宝箱概率` | `float` | `0.4` |
| `TreasureMap` | `藏宝图4_大宝箱概率` | `float` | `0.4` |
| `SkillSelection` | `额外刷新次数` | `int` | `0` |

## 游戏更新后的核对流程

1. 重新反编译 `Assembly-CSharp`。
2. 搜索所有 Harmony 目标方法是否仍存在。
3. 搜索所有 `FieldRefAccess` 字段是否仍存在。
4. 对照原版方法实现，确认调用时机和语义没有变化。
5. 执行 `dotnet build`，确认能链接新版程序集。
6. 进游戏测试以下最小用例：
   - 复制器是否能出现在候选池。
   - 大宝箱/中海盗宝箱是否弹出自选界面。
   - `藏宝图4` 清场是否输出日志并按配置掉宝箱。
   - 上一局继承是否在第一个房间弹窗。
   - 技能选择刷新次数是否等于原版值加配置值。

## 常见排查

### 启动时报补丁错误

检查目标类型、方法名和签名是否变化。优先搜索：

```powershell
rg -n "class 道具宝箱大|获得奖励\(|class Character|角色出门时\(|class WavesManager|CreateReward\(|class 战斗结算时" ..\Assembly-CSharp
```

### 自选开箱没有候选道具

检查：

- `MagicCard_Manager.Inst.剩余魔卡卡池` 是否已初始化。
- 自定义道具是否已通过 `EnsureCustomDuplicatorItems` 注入。
- 道具是否被 `没法爆出来`、互斥组、前置条件、角色颜色过滤。

### 藏宝图概率不生效

检查：

- 日志是否出现 `[藏宝图四]`。
- Buff id 是否仍为 `藏宝图四`。
- Trigger 是否仍为 `战斗结算时` 或 `战斗结算时包括继续游戏`。
- 配置文件是否写在 `BepInEx/config/Cinderia_Mod_Item_Legacy.cfg`。

### 技能选择额外刷新次数不生效

检查：

- `Character.角色出门时` 是否仍写入 `Game.PlayerData.三选一刷新次数`。
- 是否从继续游戏进入，继续游戏路径可能不会执行完整出门初始化。
- `SkillSelection.额外刷新次数` 是否大于 `0`。

## 历史记录摘要

根据 git 记录整理出的主要演进：

- `init`：创建初始 Mod 项目。
- `修改藏宝图掉落宝箱的概率`：最早期调整藏宝图概率。
- `继承道具改成和流浪汉对话掉落`：早期继承逻辑通过 NPC 发放。
- `添加复制器道具`：加入自定义复制器道具。
- `新增宝箱自选道具功能，大部分功能可在配置文件配置是否启用`：加入自选开箱和基础配置。
- `为复制器添加额外道具格增益`：复制器新增道具格能力。
- `Fix duplicator slot buffs and sidebar UI sync`：修复复制器格子和 UI 同步问题。
- `新增上一局继承道具选择流程`：继承逻辑改为下局首房间弹窗选择。
- `feat: 统一藏宝图2至4的掉落逻辑与道具描述`：统一藏宝图掉箱逻辑。
- `Add skill refresh and treasure map configs`：加入技能刷新次数和藏宝图4概率配置。

## 后续建议

- 将 `Cinderia_Mod_Item_Legacy.cs` 拆分为 `Duplicator`、`TreasureMap`、`LegacyInheritance`、`SkillSelection` 等独立文件，降低后续维护成本。
- 为每个 Harmony 补丁补充一条启动日志，方便判断游戏更新后是否命中。
- 对藏宝图和技能刷新次数增加更明确的运行日志，减少只能靠体感判断概率的情况。
- 自选开箱流程应定期和原版 `道具宝箱大.获得奖励` 对照，避免游戏更新后遗漏新逻辑。
