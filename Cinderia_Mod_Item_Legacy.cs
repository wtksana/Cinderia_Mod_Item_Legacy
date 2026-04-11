using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using Newtonsoft.Json;
using Rogue;
using Rogue.Items;
using Rogue.NPCs;
using Rogue.Units;
using Rogue.Buffs.Trigger;
using Rogue.Data;
using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using UI;
using UnityEngine;
using MagicCardData = Rogue.Data.MagicCard;
using RuntimeMagicCard = Rogue.MagicCard;

namespace Cinderia_Mod_Item_Legacy
{
    [BepInPlugin("Cinderia_Mod_Item_Legacy", "Cinderia_Mod_Item_Legacy", "1.1.0")]
    public class Cinderia_Mod_Item_Legacy : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        private static readonly AccessTools.FieldRef<Trigger, int> Trigger_触发次数字段 =
            AccessTools.FieldRefAccess<Trigger, int>("triggerCount");
        private static readonly AccessTools.FieldRef<Game, Rogue.Map> Game_当前地图字段 =
            AccessTools.FieldRefAccess<Game, Rogue.Map>("map");
        private static readonly AccessTools.FieldRef<UI_左侧道具栏相关, bool> UI左侧道具栏_初始化完成字段 =
            AccessTools.FieldRefAccess<UI_左侧道具栏相关, bool>("初始化完成");
        private static readonly AccessTools.FieldRef<UI_左侧道具栏相关, bool> UI左侧道具栏_减少一个槽中字段 =
            AccessTools.FieldRefAccess<UI_左侧道具栏相关, bool>("减少一个槽中");
        private static readonly AccessTools.FieldRef<UI_左侧道具栏相关, bool> UI左侧道具栏_拖拽中字段 =
            AccessTools.FieldRefAccess<UI_左侧道具栏相关, bool>("拖拽中");
        private static readonly AccessTools.FieldRef<UI_左侧道具栏相关, int> UI左侧道具栏_当前悬停index字段 =
            AccessTools.FieldRefAccess<UI_左侧道具栏相关, int>("当前悬停index");
        private static readonly AccessTools.FieldRef<UI_左侧道具栏相关, int> UI左侧道具栏_拖拽者index字段 =
            AccessTools.FieldRefAccess<UI_左侧道具栏相关, int>("拖拽者index");
        private static readonly AccessTools.FieldRef<UI_左侧道具栏相关, int> UI左侧道具栏_拖拽时最近index字段 =
            AccessTools.FieldRefAccess<UI_左侧道具栏相关, int>("拖拽时最近index");
        private static readonly AccessTools.FieldRef<UI_左侧道具栏相关, int> UI左侧道具栏_上帧悬停index字段 =
            AccessTools.FieldRefAccess<UI_左侧道具栏相关, int>("上帧悬停index");
        private static readonly AccessTools.FieldRef<UI_左侧道具栏相关, int> UI左侧道具栏_上帧拖拽时最近index字段 =
            AccessTools.FieldRefAccess<UI_左侧道具栏相关, int>("上帧拖拽时最近index");
        private static readonly AccessTools.FieldRef<UI_左侧道具栏相关, float> UI左侧道具栏_已悬停时间字段 =
            AccessTools.FieldRefAccess<UI_左侧道具栏相关, float>("已悬停时间");
        private static readonly AccessTools.FieldRef<UI_左侧道具栏相关, int> UI左侧道具栏_手柄想换索引字段 =
            AccessTools.FieldRefAccess<UI_左侧道具栏相关, int>("手柄想换索引");
        private static readonly MethodInfo UI左侧道具栏_一个道具栏初始化方法 =
            AccessTools.Method(typeof(UI_左侧道具栏相关), "一个道具栏初始化");

        private Harmony _harmony;
        private static ConfigFile _configFile;
        private static bool _creatingDuplicatedReward;

        private static bool _bonusGivenThisRun;
        private static bool _treasureMap4TweakedThisSession;

        // ===== 藏宝图4配置（BepInEx cfg） =====
        private static ConfigEntry<float> Cfg_TreasureMap4_战斗结算触发概率;
        private static ConfigEntry<float> Cfg_TreasureMap4_小宝箱最终概率;
        private static ConfigEntry<float> Cfg_TreasureMap4_中宝箱最终概率;
        private static ConfigEntry<float> Cfg_TreasureMap4_大宝箱最终概率;

        // ===== 复制器配置（BepInEx cfg） =====
        private static ConfigEntry<bool> Cfg_复制器_启用;
        private static ConfigEntry<float> Cfg_复制器_绿概率;
        private static ConfigEntry<float> Cfg_复制器_蓝概率;
        private static ConfigEntry<float> Cfg_复制器_紫概率;
        private static ConfigEntry<float> Cfg_复制器_橙概率;

        // ===== 自选开箱配置（BepInEx cfg） =====
        private static ConfigEntry<bool> Cfg_自选开箱_启用;

        // ===== 额外掉落记录道具配置（BepInEx cfg） =====
        private static ConfigEntry<bool> Cfg_额外掉落记录道具_启用;
        private static ConfigEntry<string> Cfg_额外掉落记录道具_ID;

        private static string PendingItemId
        {
            get
            {
                return (Cfg_额外掉落记录道具_ID?.Value ?? "").Trim();
            }
            set
            {
                if (Cfg_额外掉落记录道具_ID != null)
                {
                    Cfg_额外掉落记录道具_ID.Value = (value ?? "").Trim();
                }
                _configFile?.Save();
            }
        }

        internal static ExcelData 获取Excel数据()
        {
            try
            {
                return Game.Excel;
            }
            catch (Exception ex)
            {
                Log?.LogWarning("[Cinderia_Mod_Item_Legacy] 获取 ExcelData 失败: " + ex.Message);
                return null;
            }
        }

        private void Awake()
        {
            Log = Logger;
            _configFile = Config;
            InitConfig();

            _harmony = new Harmony("Cinderia_Mod_Item_Legacy");
            _harmony.PatchAll();

            Log.LogInfo("[Cinderia_Mod_Item_Legacy] Plugin loaded. pendingItem=" + PendingItemId);
        }

        internal static void OnNewRunStarted()
        {
            _bonusGivenThisRun = false;
            Log.LogInfo("[Cinderia_Mod_Item_Legacy] New run started. pendingItem=" + PendingItemId);

            EnsureCustomDuplicatorItems();
            ApplyTreasureMap4Tweaks();
        }

        internal static void ApplyTreasureMap4Tweaks()
        {
            try
            {
                if (_treasureMap4TweakedThisSession)
                    return;

                ExcelData excel = 获取Excel数据();
                if (excel == null || excel.magicCards == null || excel.buffs == null)
                {
                    Log.LogWarning("[Cinderia_Mod_Item_Legacy] ApplyTreasureMap4Tweaks skipped, excel not ready.");
                    return;
                }

                // 1) 找到道具数据
                MagicCardData map4 = excel.magicCards.FirstOrDefault(c => c != null && c.id == "藏宝图4");
                if (map4 == null)
                {
                    Log.LogWarning("[Cinderia_Mod_Item_Legacy] Item not found: 藏宝图4");
                    _treasureMap4TweakedThisSession = true;
                    return;
                }

                float finalSmall;
                float finalMid;
                float finalBig;
                float noReward;
                GetTreasureMap4ConfiguredRates(out finalSmall, out finalMid, out finalBig, out noReward);
                map4.introduce = BuildTreasureMap4Introduce(finalSmall, finalMid, finalBig);
                Log.LogInfo("[Cinderia_Mod_Item_Legacy] [藏宝图4] 新描述: " + (map4.introduce ?? ""));
                Log.LogInfo("[Cinderia_Mod_Item_Legacy] [藏宝图四] 当前配置 => 小:"
                    + finalSmall.ToString("0.###")
                    + ", 中:" + finalMid.ToString("0.###")
                    + ", 大:" + finalBig.ToString("0.###")
                    + ", 空:" + noReward.ToString("0.###"));

                _treasureMap4TweakedThisSession = true;
            }
            catch (Exception ex)
            {
                Log.LogError("[Cinderia_Mod_Item_Legacy] ApplyTreasureMap4Tweaks error: " + ex);
            }
        }

        private void InitConfig()
        {
            const string dropSection = "LegacyDrop";
            Cfg_额外掉落记录道具_启用 = Config.Bind(dropSection, "启用", true, "是否启用‘每局结束记录最高道具，并在下局开始时和拾荒者NPC对话额外掉落该道具’");
            Cfg_额外掉落记录道具_ID = Config.Bind(dropSection, "记录道具ID", "", "内部使用：记录上局最高道具ID。可手动填写以指定下次额外掉落道具。留空表示无。") ;

            const string section = "TreasureMap4";
            Cfg_TreasureMap4_战斗结算触发概率 = Config.Bind(section, "战斗结算触发概率", 1f, "藏宝图4触发总概率（0~1，下面各宝箱概率之和要等于这个值）");
            Cfg_TreasureMap4_小宝箱最终概率 = Config.Bind(section, "小宝箱最终概率", 0.50f, "清空房间后最终获得小海盗宝箱的概率（0~1）");
            Cfg_TreasureMap4_中宝箱最终概率 = Config.Bind(section, "中宝箱最终概率", 0.35f, "清空房间后最终获得中海盗宝箱的概率（0~1）");
            Cfg_TreasureMap4_大宝箱最终概率 = Config.Bind(section, "大宝箱最终概率", 0.15f, "清空房间后最终获得大海盗宝箱的概率（0~1）");

            const string duplicatorSection = "Duplicator";
            Cfg_复制器_启用 = Config.Bind(duplicatorSection, "启用", true, "是否注入新增道具“复制器”");
            Cfg_复制器_绿概率 = Config.Bind(duplicatorSection, "绿概率", 0.20f, "拥有绿色复制器时，房间奖励额外复制一份的概率（0~1）");
            Cfg_复制器_蓝概率 = Config.Bind(duplicatorSection, "蓝概率", 0.40f, "拥有蓝色复制器时，房间奖励额外复制一份的概率（0~1）");
            Cfg_复制器_紫概率 = Config.Bind(duplicatorSection, "紫概率", 0.60f, "拥有紫色复制器时，房间奖励额外复制一份的概率（0~1）");
            Cfg_复制器_橙概率 = Config.Bind(duplicatorSection, "橙概率", 0.80f, "拥有橙色复制器时，房间奖励额外复制一份的概率（0~1）");

            const string chestSelectionSection = "ChestSelection";
            Cfg_自选开箱_启用 = Config.Bind(chestSelectionSection, "启用", true, "是否启用‘宝箱随机品质后，自选该品质候选道具’功能");

            Log.LogInfo("[Cinderia_Mod_Item_Legacy] Config loaded. trigger="
                + Cfg_TreasureMap4_战斗结算触发概率.Value.ToString("0.###")
                + ", small=" + Cfg_TreasureMap4_小宝箱最终概率.Value.ToString("0.###")
                + ", middle=" + Cfg_TreasureMap4_中宝箱最终概率.Value.ToString("0.###")
                + ", big=" + Cfg_TreasureMap4_大宝箱最终概率.Value.ToString("0.###")
                + ", duplicatorEnabled=" + Cfg_复制器_启用.Value
                + ", chestSelectionEnabled=" + Cfg_自选开箱_启用.Value
                + ", legacyDropEnabled=" + Cfg_额外掉落记录道具_启用.Value
                + ", pendingItem=" + (Cfg_额外掉落记录道具_ID.Value ?? ""));
        }

        internal static bool 是否启用自选开箱()
        {
            return Cfg_自选开箱_启用?.Value ?? true;
        }

        internal static bool 是否为自定义开箱候选道具(MagicCardData data)
        {
            if (data == null || string.IsNullOrEmpty(data.id))
            {
                return false;
            }

            return data.id.StartsWith("复制器", StringComparison.Ordinal);
        }

        internal static bool TryHandleTreasureMap4BattleClearReward(Trigger trigger)
        {
            if (trigger?.buff?.data == null || trigger.buff.data.id != "藏宝图四")
            {
                return false;
            }

            try
            {
                if (!CanProcessTreasureMap4Reward(trigger))
                {
                    return true;
                }
                trigger.设置cd();
                Trigger_触发次数字段(trigger)++;
                trigger.buff.道具亮一下(true);

                string rewardPrefab = ResolveTreasureMap4RewardPrefab();
                if (string.IsNullOrEmpty(rewardPrefab))
                {
                    Log.LogInfo("[Cinderia_Mod_Item_Legacy] [藏宝图四] 本次清场未生成海盗宝箱。");
                    return true;
                }

                Vector3 createPos = GetTreasureMap4ChestCreatePos();
                GameObject rewardObject = Game.实例化预制体(rewardPrefab, createPos);
                if (rewardObject == null)
                {
                    Log.LogWarning("[Cinderia_Mod_Item_Legacy] [藏宝图四] 生成海盗宝箱失败，预制体不存在: " + rewardPrefab);
                    return true;
                }

                Log.LogInfo("[Cinderia_Mod_Item_Legacy] [藏宝图四] 直接生成海盗宝箱: " + rewardPrefab + " @ " + createPos);
                return true;
            }
            catch (Exception ex)
            {
                Log.LogError("[Cinderia_Mod_Item_Legacy] [藏宝图四] 直接生成海盗宝箱异常: " + ex);
                return false;
            }
        }

        private static bool CanProcessTreasureMap4Reward(Trigger trigger)
        {
            if (trigger.skill == null || trigger.cooldown > 0f || trigger.skill.封印 || UI_对话气泡.对话中)
            {
                return false;
            }

            return true;
        }

        private static string ResolveTreasureMap4RewardPrefab()
        {
            const string noReward = "__NoReward__";
            float finalSmall;
            float finalMid;
            float finalBig;
            float noRewardWeight;
            GetTreasureMap4ConfiguredRates(out finalSmall, out finalMid, out finalBig, out noRewardWeight);
            string result = RandomUtils.PseudoRandom(
                "藏宝图四清场宝箱",
                true,
                new ValueTuple<string, float>[]
                {
                    new ValueTuple<string, float>("海盗宝箱小", finalSmall),
                    new ValueTuple<string, float>("海盗宝箱中", finalMid),
                    new ValueTuple<string, float>("海盗宝箱大", finalBig),
                    new ValueTuple<string, float>(noReward, noRewardWeight)
                });
            return result == noReward ? null : result;
        }

        private static Vector3 GetTreasureMap4ChestCreatePos()
        {
            Vector3 anchorPos = GetTreasureMap4RewardAnchorPos();
            Vector2 randomCircle = UnityEngine.Random.insideUnitCircle;
            if (randomCircle.sqrMagnitude < 0.01f)
            {
                randomCircle = Vector2.right;
            }

            float offsetDistance = UnityEngine.Random.Range(1.0f, 1.5f);
            Vector3 offset = new Vector3(randomCircle.x, 0f, randomCircle.y).normalized * offsetDistance;
            return MapUtils.ClampToNavMesh(anchorPos + offset, false);
        }

        private static Vector3 GetTreasureMap4RewardAnchorPos()
        {
            Vector3[] 已有奖励位置 = 可拾取物.所有可拾取物
                .Where(t => t != null && t.gameObject != null && t.gameObject.activeInHierarchy)
                .Select(t => t.transform.position)
                .ToArray();
            if (已有奖励位置.Length > 0)
            {
                Vector3 默认落点 = GetTreasureMap4DefaultDropPoint();
                return 已有奖励位置
                    .OrderBy(pos => Vector3.Distance(pos, 默认落点))
                    .First();
            }

            return GetTreasureMap4DefaultDropPoint();
        }

        private static Vector3 GetTreasureMap4DefaultDropPoint()
        {
            Rogue.Map 当前地图 = Game.Inst != null ? Game_当前地图字段(Game.Inst) : null;
            if (当前地图?.transform == null)
            {
                return Character.Inst != null ? Character.Inst.transform.position : Vector3.zero;
            }

            Transform points = 当前地图.transform.Find("掉落点");
            if (points == null || points.childCount == 0)
            {
                return Character.Inst != null ? Character.Inst.transform.position : Vector3.zero;
            }

            Rogue.Units.Unit 助战 = Game.Inst.units.Units.FirstOrDefault(t => t.data.id == "友谊赛小黄");
            Transform bestPoint = Enumerable.Range(0, points.childCount)
                .Select(points.GetChild)
                .Where(t => 助战 == null || Vector3.Distance(助战, t.position) > 3f)
                .OrderBy(t => Character.Inst != null ? Vector3.Distance(t.position, Character.Inst) : 0f)
                .FirstOrDefault();

            return bestPoint != null
                ? bestPoint.position
                : (Character.Inst != null ? Character.Inst.transform.position : Vector3.zero);
        }

        private static void GetTreasureMap4ConfiguredRates(out float finalSmall, out float finalMid, out float finalBig, out float noReward)
        {
            float triggerChance = Mathf.Clamp01(Cfg_TreasureMap4_战斗结算触发概率?.Value ?? 1f);
            finalSmall = Mathf.Max(0f, Cfg_TreasureMap4_小宝箱最终概率?.Value ?? 0.50f);
            finalMid = Mathf.Max(0f, Cfg_TreasureMap4_中宝箱最终概率?.Value ?? 0.35f);
            finalBig = Mathf.Max(0f, Cfg_TreasureMap4_大宝箱最终概率?.Value ?? 0.15f);
            float total = finalSmall + finalMid + finalBig;

            if (total > triggerChance && total > 0f)
            {
                float scale = triggerChance / total;
                finalSmall *= scale;
                finalMid *= scale;
                finalBig *= scale;
            }

            noReward = Mathf.Max(0f, 1f - (finalSmall + finalMid + finalBig));
        }

        internal static void EnsureCustomDuplicatorItems()
        {
            try
            {
                if (!(Cfg_复制器_启用?.Value ?? true))
                    return;

                ExcelData excel = 获取Excel数据();
                if (excel == null || excel.magicCards == null)
                    return;

                if (!EnsureCustomDuplicatorSlotBuffs(excel))
                    return;

                MagicCardData template = excel.magicCards.FirstOrDefault(c => c != null && c.id == "藏宝图4");
                if (template == null)
                {
                    Log.LogWarning("[Cinderia_Mod_Item_Legacy] 无法注入复制器，缺少模板道具 藏宝图4");
                    return;
                }

                var list = excel.magicCards.ToList();
                bool addedAny = false;
                RemoveDuplicatorItemIfExists(list, 0);
                for (int lv = 1; lv <= 4; lv++)
                {
                    string id = GetDuplicatorItemId(lv);
                    MagicCardData existing = list.FirstOrDefault(c => c != null && c.id == id);
                    if (existing != null)
                    {
                        existing.name = "复制器";
                        existing.icon = "藏宝图";
                        existing.ItemLv = lv;
                        existing.introduce = BuildDuplicatorIntroduce(lv);
                        existing.strength = 0;
                        existing.agility = 0;
                        existing.intelligence = 0;
                        existing.buffs = new[] { GetDuplicatorSlotBuffId(lv) };
                        existing.skills = Array.Empty<string>();
                        continue;
                    }

                    MagicCardData item = template.Clone();
                    item.id = id;
                    item.name = "复制器";
                    item.kind = "道具";
                    item.ItemLv = lv;
                    item.icon = "藏宝图";
                    item.introduce = BuildDuplicatorIntroduce(lv);
                    item.叠层buff = "";
                    item.CardData = "";
                    item.CardAtk = 0f;
                    item.CardAtk2 = 0f;
                    item.CardCD = 0f;
                    item.CardCharge = 0;
                    item.color = "";
                    item.tipRule = "无";
                    item.strength = 0;
                    item.agility = 0;
                    item.intelligence = 0;
                    item.buffs = new[] { GetDuplicatorSlotBuffId(lv) };
                    item.skills = Array.Empty<string>();
                    item.keyward = Array.Empty<string>();
                    item.groups = Array.Empty<string>();
                    item.preconditions = Array.Empty<string>();
                    item.mutex = Array.Empty<string>();
                    item.powerRarity = 1;
                    item.刷新权重 = 1f;
                    item.ban = true;
                    item.是否商店解锁 = false;
                    item.显示在图鉴 = true;

                    list.Add(item);
                    addedAny = true;
                }

                UpdateDuplicatorItemDescriptions(list);

                excel.magicCards = list.ToArray();
                if (MagicCard_Manager.Inst != null)
                {
                    MagicCard_Manager.Inst.重置剩余魔卡卡池();
                }
                if (addedAny)
                {
                    Log.LogInfo("[Cinderia_Mod_Item_Legacy] 已注入复制器道具 4 个等级版本（绿~橙）");
                }
            }
            catch (Exception ex)
            {
                Log.LogError("[Cinderia_Mod_Item_Legacy] EnsureCustomDuplicatorItems error: " + ex);
            }
        }

        private static void UpdateDuplicatorItemDescriptions(System.Collections.Generic.List<MagicCardData> list)
        {
            for (int lv = 1; lv <= 4; lv++)
            {
                string id = GetDuplicatorItemId(lv);
                MagicCardData item = list.FirstOrDefault(c => c != null && c.id == id);
                if (item == null)
                    continue;

                item.introduce = BuildDuplicatorIntroduce(lv);
                item.strength = 0;
                item.agility = 0;
                item.intelligence = 0;
                item.buffs = new[] { GetDuplicatorSlotBuffId(lv) };
            }
        }

        private static bool EnsureCustomDuplicatorSlotBuffs(ExcelData excel)
        {
            if (excel?.buffs == null)
            {
                return false;
            }

            System.Collections.Generic.List<Rogue.Data.Buff> list = excel.buffs.ToList();
            Rogue.Data.Buff template = list.FirstOrDefault(b => b != null && b.id == "宽松的腰带加格子二");
            if (template == null)
            {
                Log.LogWarning("[Cinderia_Mod_Item_Legacy] 无法注入复制器加格子 Buff，缺少模板 Buff 宽松的腰带加格子二");
                return false;
            }

            bool addedAny = false;
            for (int lv = 1; lv <= 4; lv++)
            {
                string id = GetDuplicatorSlotBuffId(lv);
                Rogue.Data.Buff buff = list.FirstOrDefault(b => b != null && b.id == id);
                if (buff == null)
                {
                    buff = template.Clone();
                    list.Add(buff);
                    addedAny = true;
                }

                buff.id = id;
                buff.name = "复制器加格子";
                buff.description = "复制器提供" + lv + "个额外道具格。";
                buff.script = template.script;
                buff.scriptData = lv.ToString();
            }

            excel.buffs = list.ToArray();
            if (addedAny)
            {
                Log.LogInfo("[Cinderia_Mod_Item_Legacy] 已注入复制器加格子 Buff 4 个等级版本");
            }
            return true;
        }

        private static void RemoveDuplicatorItemIfExists(System.Collections.Generic.List<MagicCardData> list, int level)
        {
            string id = GetDuplicatorItemId(level);
            MagicCardData existing = list.FirstOrDefault(c => c != null && c.id == id);
            if (existing != null)
            {
                list.Remove(existing);
            }
        }

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };

        private static string ToJson(object obj)
        {
            try
            {
                return JsonConvert.SerializeObject(obj, JsonSettings);
            }
            catch (Exception ex)
            {
                return "{\"jsonError\":\"" + (ex.Message ?? "unknown") + "\"}";
            }
        }

        private static string BuildTreasureMap4Introduce(float finalSmall, float finalMid, float finalBig)
        {
            return "清空房间：有" + ToPercentText(finalSmall) + "几率找到一个破烂海盗宝箱。\n"
                + "清空房间：有" + ToPercentText(finalMid) + "几率找到一个普通海盗宝箱。\n"
                + "清空房间：有" + ToPercentText(finalBig) + "几率找到一个高级海盗宝箱。\n"
                + "装备：打开宝箱时获得1点随机属性。";
        }

        private static string BuildDuplicatorIntroduce(int level)
        {
            float chance = GetDuplicatorChanceByLevel(level);
            return "清空房间掉落奖励时：有" + ToPercentText(chance) + "几率额外掉落一份相同的房间奖励。\n"
                + "装备：获得" + level + "个额外的道具格。";
        }

        private static string GetDuplicatorItemId(int level)
        {
            return "复制器" + level;
        }

        private static string GetDuplicatorSlotBuffId(int level)
        {
            switch (level)
            {
                case 1: return "复制器加格子一";
                case 2: return "复制器加格子二";
                case 3: return "复制器加格子三";
                case 4: return "复制器加格子四";
                default: throw new ArgumentOutOfRangeException(nameof(level));
            }
        }

        internal static bool 是否为复制器道具Id(string itemId)
        {
            return !string.IsNullOrEmpty(itemId) && itemId.StartsWith("复制器", StringComparison.Ordinal);
        }

        internal static void 延迟重应用复制器装备效果(string source, int waitFrames = 2)
        {
            if (!(Cfg_复制器_启用?.Value ?? true) || MagicCard_Manager.Inst?.已拾取魔卡 == null)
            {
                return;
            }

            UniTaskUtils.Split(async () =>
            {
                int 剩余帧数 = Mathf.Max(1, waitFrames);
                while (剩余帧数-- > 0)
                {
                    await UniTask.NextFrame();
                }

                重应用复制器装备效果(source);
            }).Forget();
        }

        private static void 重应用复制器装备效果(string source)
        {
            try
            {
                if (!(Cfg_复制器_启用?.Value ?? true))
                {
                    return;
                }

                EnsureCustomDuplicatorItems();

                if (MagicCard_Manager.Inst?.已拾取魔卡 == null)
                {
                    return;
                }

                bool hasDuplicator = MagicCard_Manager.Inst.已拾取魔卡
                    .Any(card => card?.data != null && 是否为复制器道具Id(card.data.id));
                if (!hasDuplicator)
                {
                    return;
                }

                恢复复制器额外格子();

                UI_背包面板.Inst?.Refresh();
                延迟同步左侧道具栏UI(source, 1, 20);
            }
            catch (Exception ex)
            {
                Log.LogError("[Cinderia_Mod_Item_Legacy] 重应用复制器装备效果失败 source=" + source + " ex=" + ex);
            }
        }

        private static void 恢复复制器额外格子()
        {
            MagicCard_Manager mgr = MagicCard_Manager.Inst;
            if (mgr?.道具列表 == null)
            {
                return;
            }

            int currentExtra = Mathf.Max(0, MagicCard_Manager.额外道具栏位);
            int extraFromItemList = Mathf.Max(0, mgr.道具列表.Count - MagicCard_Manager.初始道具栏位);
            int targetExtra = Mathf.Max(currentExtra, extraFromItemList);
            if (targetExtra == currentExtra)
            {
                return;
            }

            MagicCard_Manager.额外道具栏位 = targetExtra;
        }

        internal static void 延迟同步左侧道具栏UI(string source, int waitFrames = 1, int retryFrames = 20)
        {
            if (!(Cfg_复制器_启用?.Value ?? true))
            {
                return;
            }

            UniTaskUtils.Split(async () =>
            {
                int remaining = Mathf.Max(0, waitFrames);
                while (remaining-- > 0)
                {
                    await UniTask.NextFrame();
                }

                int retries = Mathf.Max(1, retryFrames);
                while (retries-- > 0)
                {
                    if (尝试同步左侧道具栏UI(source))
                    {
                        return;
                    }

                    await UniTask.NextFrame();
                }
            }).Forget();
        }

        private static bool 尝试同步左侧道具栏UI(string source)
        {
            try
            {
                if (!(Cfg_复制器_启用?.Value ?? true))
                {
                    return true;
                }

                UI_左侧道具栏相关 ui = UI_左侧道具栏相关.Inst;
                MagicCard_Manager mgr = MagicCard_Manager.Inst;
                if (ui == null || mgr == null || ui.m_道具栏 == null || ui.m_道具栏悬停框 == null)
                {
                    return false;
                }

                恢复复制器额外格子();
                if (!UI左侧道具栏_初始化完成字段(ui) || UI左侧道具栏_减少一个槽中字段(ui))
                {
                    return false;
                }

                int target = Mathf.Max(0, MagicCard_Manager.道具栏位);
                bool needRebuild = ui.m_道具栏.numItems != target
                    || ui.m_道具栏悬停框.numItems != target
                    || ui.所有背包 == null
                    || ui.所有背包外发光 == null
                    || ui.所有背包.Count != target
                    || ui.所有背包外发光.Count != target;

                System.Collections.Generic.List<UI_左侧道具悬停框> 旧悬停框列表 = ui.所有背包外发光 != null
                    ? ui.所有背包外发光.ToList()
                    : new System.Collections.Generic.List<UI_左侧道具悬停框>();

                if (needRebuild)
                {
                    UI_主界面 主界面 = UI_主界面.Inst;
                    if (主界面 != null)
                    {
                        foreach (UI_左侧道具悬停框 hover in 旧悬停框列表)
                        {
                            if (hover != null)
                            {
                                主界面.设置可以被手柄选择的按钮内是否存在(hover, 0, false, "左侧道具栏相关");
                            }
                        }
                    }

                    重置左侧道具栏交互状态(ui);
                    ui.m_道具栏.RemoveChildrenToPool();
                    ui.m_道具栏悬停框.RemoveChildrenToPool();

                    for (int i = 0; i < target; i++)
                    {
                        ui.m_道具栏.AddItemFromPool();
                        UI_左侧道具悬停框 hover = ui.m_道具栏悬停框.AddItemFromPool() as UI_左侧道具悬停框;
                        初始化左侧道具栏悬停框(ui, hover, i);
                    }

                    ui.m_道具栏.ResizeToFit();
                    ui.m_道具栏悬停框.ResizeToFit();
                    ui.所有背包 = ui.m_道具栏.GetChildren().Select(t => t as UI_左侧道具).ToList();
                    ui.所有背包外发光 = ui.m_道具栏悬停框.GetChildren().Select(t => t as UI_左侧道具悬停框).ToList();
                }
                else
                {
                    for (int i = 0; i < ui.所有背包外发光.Count; i++)
                    {
                        初始化左侧道具栏悬停框(ui, ui.所有背包外发光[i], i);
                    }
                }

                UI_主界面 当前主界面 = UI_主界面.Inst;
                if (当前主界面 != null)
                {
                    foreach (UI_左侧道具悬停框 hover in ui.所有背包外发光)
                    {
                        if (hover != null)
                        {
                            当前主界面.加入可以被手柄选择的按钮(hover, 0, "左侧道具栏相关");
                        }
                    }
                }

                ui.刷新左侧道具栏();
                UI_背包面板.Inst?.Refresh();
                return true;
            }
            catch (Exception ex)
            {
                Log.LogError("[Cinderia_Mod_Item_Legacy] 同步左侧道具栏UI失败 source=" + source + " ex=" + ex);
                return false;
            }
        }

        private static void 初始化左侧道具栏悬停框(UI_左侧道具栏相关 ui, UI_左侧道具悬停框 hover, int index)
        {
            if (ui == null || hover == null || UI左侧道具栏_一个道具栏初始化方法 == null)
            {
                return;
            }

            UI左侧道具栏_一个道具栏初始化方法.Invoke(ui, new object[] { hover, index });
        }

        private static void 重置左侧道具栏交互状态(UI_左侧道具栏相关 ui)
        {
            if (ui == null)
            {
                return;
            }

            UI左侧道具栏_拖拽中字段(ui) = false;
            UI左侧道具栏_当前悬停index字段(ui) = -1;
            UI左侧道具栏_拖拽者index字段(ui) = -1;
            UI左侧道具栏_拖拽时最近index字段(ui) = -1;
            UI左侧道具栏_上帧悬停index字段(ui) = -1;
            UI左侧道具栏_上帧拖拽时最近index字段(ui) = -1;
            UI左侧道具栏_已悬停时间字段(ui) = 0f;
            UI左侧道具栏_手柄想换索引字段(ui) = -1;
        }

        internal static async UniTask 安全减少一个槽(UI_左侧道具栏相关 ui)
        {
            if (ui == null)
            {
                return;
            }

            bool 已设置减槽中 = false;
            try
            {
                await UniTask.WaitUntil(() => ui == null || !UI左侧道具栏_减少一个槽中字段(ui), PlayerLoopTiming.Update, default(CancellationToken), false);
                if (!左侧道具栏可安全改动(ui))
                {
                    return;
                }

                UI左侧道具栏_减少一个槽中字段(ui) = true;
                已设置减槽中 = true;
                if (!左侧道具栏可安全改动(ui))
                {
                    return;
                }

                MagicCard_Manager mgr = MagicCard_Manager.Inst;
                if (mgr?.道具列表 == null || mgr.道具列表.Count == 0)
                {
                    return;
                }

                int removeIndex = MagicCard_Manager.道具栏位 - 1;
                if (removeIndex < 0 || removeIndex >= mgr.道具列表.Count)
                {
                    return;
                }

                RuntimeMagicCard lastCard = mgr.道具列表.LastOrDefault();
                if (mgr.项链 && lastCard == mgr.项链)
                {
                    int necklaceTargetIndex = Mathf.Max(0, UI_左侧道具栏相关.道具栏位 - 2);
                    if (mgr.项链.index >= 0 && mgr.项链.index < mgr.道具列表.Count && necklaceTargetIndex < mgr.道具列表.Count)
                    {
                        UI_左侧道具栏相关.交换道具(mgr.项链.index, necklaceTargetIndex);
                    }
                    lastCard = mgr.道具列表.LastOrDefault();
                }

                if (lastCard != null)
                {
                    UI_左侧道具栏相关.丢道具(mgr.道具列表.Last());
                }

                if (!左侧道具栏可安全改动(ui))
                {
                    return;
                }

                if (ui.m_道具栏.numItems > removeIndex)
                {
                    ui.m_道具栏.RemoveChildrenToPool(removeIndex, removeIndex);
                }
                if (ui.m_道具栏悬停框.numItems > removeIndex)
                {
                    ui.m_道具栏悬停框.RemoveChildrenToPool(removeIndex, removeIndex);
                }

                ui.m_道具栏.ResizeToFit();
                ui.m_道具栏悬停框.ResizeToFit();
                ui.所有背包 = ui.m_道具栏.GetChildren().Select(t => t as UI_左侧道具).ToList();
                ui.所有背包外发光 = ui.m_道具栏悬停框.GetChildren().Select(t => t as UI_左侧道具悬停框).ToList();

                mgr = MagicCard_Manager.Inst;
                removeIndex = MagicCard_Manager.道具栏位 - 1;
                if (mgr?.道具列表 != null && removeIndex >= 0 && removeIndex < mgr.道具列表.Count)
                {
                    mgr.道具列表.RemoveAt(removeIndex);
                }
                MagicCard_Manager.额外道具栏位 = Mathf.Max(0, MagicCard_Manager.额外道具栏位 - 1);

                if (左侧道具栏可安全改动(ui))
                {
                    ui.刷新左侧道具栏();
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning("[Cinderia_Mod_Item_Legacy] 已拦截左侧道具栏减槽异常: " + ex.Message);
            }
            finally
            {
                if (ui != null && 已设置减槽中)
                {
                    UI左侧道具栏_减少一个槽中字段(ui) = false;
                }
            }
        }

        private static bool 左侧道具栏可安全改动(UI_左侧道具栏相关 ui)
        {
            if (ui == null || ui.isDisposed)
            {
                return false;
            }

            if (!UI左侧道具栏_初始化完成字段(ui))
            {
                return false;
            }

            if (UI_主界面.Inst == null || UI_主界面.Inst.isDisposed || Game.Inst == null || Game.Inst.UI == null)
            {
                return false;
            }

            if (ui.m_道具栏 == null || ui.m_道具栏.isDisposed || ui.m_道具栏悬停框 == null || ui.m_道具栏悬停框.isDisposed)
            {
                return false;
            }

            if (ui.displayObject == null || ui.m_道具栏.displayObject == null || ui.m_道具栏悬停框.displayObject == null)
            {
                return false;
            }

            return true;
        }

        private static float GetDuplicatorChanceByLevel(int level)
        {
            switch (level)
            {
                case 1: return Mathf.Clamp01(Cfg_复制器_绿概率?.Value ?? 0.30f);
                case 2: return Mathf.Clamp01(Cfg_复制器_蓝概率?.Value ?? 0.50f);
                case 3: return Mathf.Clamp01(Cfg_复制器_紫概率?.Value ?? 0.70f);
                case 4: return Mathf.Clamp01(Cfg_复制器_橙概率?.Value ?? 0.90f);
                default: return 0f;
            }
        }

        internal static float GetCurrentDuplicatorTriggerChance()
        {
            if (!(Cfg_复制器_启用?.Value ?? true))
                return 0f;

            MagicCard_Manager mgr = MagicCard_Manager.Inst;
            if (mgr == null || mgr.道具列表 == null)
                return 0f;

            float total = 0f;
            foreach (RuntimeMagicCard card in mgr.道具列表)
            {
                if (card == null || card.data == null)
                    continue;

                string id = card.data.id ?? "";
                if (!id.StartsWith("复制器", StringComparison.Ordinal))
                    continue;

                total += GetDuplicatorChanceByLevel(card.data.ItemLv);
            }
            return Mathf.Clamp01(total);
        }

        internal static void TryDuplicateRoomReward(string reward, Vector3? createPos, int? itemLv, string source)
        {
            try
            {
                if (!(Cfg_复制器_启用?.Value ?? true))
                    return;

                if (_creatingDuplicatedReward)
                    return;

                float chance = GetCurrentDuplicatorTriggerChance();
                if (chance <= 0f)
                    return;

                if (string.IsNullOrEmpty(reward) || reward == "无")
                    return;

                if (!Game.获取一个固定随机数bool("复制器额外奖励_" + reward + "_" + source, chance))
                    return;

                Vector3? duplicatedCreatePos = createPos;
                if (createPos != null)
                {
                    duplicatedCreatePos = createPos.Value + new Vector3(1.2f, 0f, 0.6f);
                }

                _creatingDuplicatedReward = true;
                UniTaskUtils.Split(async () =>
                {
                    try
                    {
                        await WavesManager.CreateReward(reward, duplicatedCreatePos, false, itemLv);
                    }
                    finally
                    {
                        _creatingDuplicatedReward = false;
                    }
                }).Forget();
                Log.LogInfo("[Cinderia_Mod_Item_Legacy] 复制器触发，额外复制房间奖励: source=" + source + ", reward=" + reward + ", itemLv=" + (itemLv?.ToString() ?? "null") + ", chance=" + chance.ToString("0.###") + ", pos=" + (duplicatedCreatePos?.ToString() ?? "null"));
            }
            catch (Exception ex)
            {
                Log.LogError("[Cinderia_Mod_Item_Legacy] TryDuplicateRoomReward error: " + ex);
            }
        }

        private static string ToPercentText(float rate)
        {
            return (Mathf.Clamp01(rate) * 100f).ToString("0.#") + "%";
        }

        internal static void CaptureTopItemAtRunEnd(string source)
        {
            try
            {
                if (!(Cfg_额外掉落记录道具_启用?.Value ?? true))
                    return;

                MagicCard_Manager mgr = MagicCard_Manager.Inst;
                if (mgr == null)
                {
                    Log.LogWarning("[Cinderia_Mod_Item_Legacy] Capture skipped, manager null. source=" + source);
                    return;
                }

                RuntimeMagicCard top = null;
                int bestLv = int.MinValue;

                // 按道具栏顺序扫描，等级更高才替换；同等级保留先出现的（第一个）
                for (int i = 0; i < mgr.道具列表.Count; i++)
                {
                    RuntimeMagicCard card = mgr.道具列表[i];
                    if (card == null || card.data == null)
                        continue;

                    if (card.data.kind != "道具")
                        continue;

                    if (card.data.id == "项链")
                        continue;

                    int lv = card.data.ItemLv;
                    if (top == null || lv > bestLv)
                    {
                        top = card;
                        bestLv = lv;
                    }
                }

                PendingItemId = top != null && top.data != null ? top.data.id : "";

                if (!string.IsNullOrEmpty(PendingItemId))
                {
                    Log.LogInfo("[Cinderia_Mod_Item_Legacy] Captured top item at run end. source=" + source + ", id=" + PendingItemId + ", lv=" + bestLv);
                }
                else
                {
                    Log.LogInfo("[Cinderia_Mod_Item_Legacy] No item captured at run end. source=" + source);
                }
            }
            catch (Exception ex)
            {
                Log.LogError("[Cinderia_Mod_Item_Legacy] CaptureTopItemAtRunEnd error: " + ex);
            }
        }

        internal static void TryDropPendingItem(Vector3 position, string source)
        {
            try
            {
                if (!(Cfg_额外掉落记录道具_启用?.Value ?? true))
                    return;

                if (_bonusGivenThisRun)
                    return;

                string pendingItemId = PendingItemId;

                if (string.IsNullOrEmpty(pendingItemId))
                    return;

                if (Game.Inst == null)
                    return;

                MagicCardData data = MagicCard_Manager.id找data(pendingItemId);
                if (data == null && pendingItemId.StartsWith("复制器", StringComparison.Ordinal))
                {
                    EnsureCustomDuplicatorItems();
                    data = MagicCard_Manager.id找data(pendingItemId);
                }
                if (data == null)
                {
                    Log.LogWarning("[Cinderia_Mod_Item_Legacy] Pending item id not found: " + pendingItemId);
                    PendingItemId = "";
                    return;
                }

                GameObject go = Game.实例化预制体("道具", position);
                道具 drop = go != null ? go.GetComponent<道具>() : null;
                if (drop == null)
                {
                    Log.LogWarning("[Cinderia_Mod_Item_Legacy] Failed to spawn drop object.");
                    return;
                }

                drop.Init(data, null, null, true);

                _bonusGivenThisRun = true;
                PendingItemId = "";

                Log.LogInfo("[Cinderia_Mod_Item_Legacy] Dropped pending item. source=" + source + ", id=" + data.id);
            }
            catch (Exception ex)
            {
                Log.LogError("[Cinderia_Mod_Item_Legacy] TryDropPendingItem error: " + ex);
            }
        }

    }

    // 每局结束：记录“当前最高等级道具（同级取第一个）”
    [HarmonyPatch(typeof(Character), "自杀重置回老家")]
    internal static class Patch_CaptureTopItemOnRunEnd
    {
        private static void Prefix(bool 是角色死亡)
        {
            Cinderia_Mod_Item_Legacy.CaptureTopItemAtRunEnd("Character.自杀重置回老家.Prefix death=" + 是角色死亡);
        }
    }

    // 新局开始：重置“本局是否已发奖励”标记，并在角色创建流程结束后统一同步复制器额外槽位
    [HarmonyPatch(typeof(Character), "角色创建时")]
    internal static class Patch_ResetPerRunState
    {
        private static void Postfix()
        {
            Cinderia_Mod_Item_Legacy.OnNewRunStarted();
        }
    }

    [HarmonyPatch(typeof(Character), "角色出门时")]
    internal static class Patch_DuplicatorReequip_OnLeaveHome
    {
        private static void Postfix()
        {
            Cinderia_Mod_Item_Legacy.延迟重应用复制器装备效果("Character.角色出门时.Postfix", 3);
        }
    }

    [HarmonyPatch(typeof(Character), "加载存档字符串")]
    internal static class Patch_DuplicatorReequip_OnLoadDeckString
    {
        private static void Postfix()
        {
            Cinderia_Mod_Item_Legacy.延迟重应用复制器装备效果("Character.加载存档字符串.Postfix", 3);
        }
    }

    [HarmonyPatch(typeof(UI_左侧道具栏相关), "ConstructFromXML")]
    internal static class Patch_LeftItemBar_Construct_Log
    {
        private static void Postfix()
        {
            MagicCard_Manager mgr = MagicCard_Manager.Inst;
            bool hasDuplicator = mgr?.已拾取魔卡 != null
                && mgr.已拾取魔卡.Any(card => card?.data != null && Cinderia_Mod_Item_Legacy.是否为复制器道具Id(card.data.id));
            if (!hasDuplicator)
            {
                return;
            }

            Cinderia_Mod_Item_Legacy.延迟同步左侧道具栏UI("UI_左侧道具栏相关.ConstructFromXML", 6, 30);
        }
    }

    [HarmonyPatch(typeof(UI_左侧道具栏相关), "_减少一个槽")]
    internal static class Patch_LeftItemBar_SafeReduceSlot
    {
        private static bool Prefix(UI_左侧道具栏相关 __instance, ref UniTask __result)
        {
            __result = Cinderia_Mod_Item_Legacy.安全减少一个槽(__instance);
            return false;
        }
    }

    // 和垃圾屋NPC对话随机给道具时：额外掉落记录道具
    [HarmonyPatch(typeof(NPC), "垃圾屋随机道具")]
    internal static class Patch_DropPendingOnJunkyardNpcDialog
    {
        private static void Postfix(NPC __instance)
        {
            Vector3 pos = __instance != null ? __instance.transform.position : Vector3.zero;

            Cinderia_Mod_Item_Legacy.TryDropPendingItem(pos, "NPC.垃圾屋随机道具.Postfix");
        }
    }

    [HarmonyPatch(typeof(WavesManager), "CreateReward")]
    internal static class Patch_Duplicator_OnCreateReward
    {
        private static void Postfix(string reward, Vector3? createPos, bool openDoorAfterPick, int? itemLv)
        {
            if (openDoorAfterPick)
                return;

            Cinderia_Mod_Item_Legacy.TryDuplicateRoomReward(reward, createPos, itemLv, "WavesManager.CreateReward");
        }
    }

    [HarmonyPatch(typeof(战斗结算时), "清场时")]
    internal static class Patch_TreasureMap4_BattleClearReward
    {
        private static bool Prefix(战斗结算时 __instance)
        {
            if (__instance?.buff?.data == null || __instance.buff.data.id != "藏宝图四")
            {
                return true;
            }

            bool handled = Cinderia_Mod_Item_Legacy.TryHandleTreasureMap4BattleClearReward(__instance);
            return !handled;
        }
    }

    [HarmonyPatch(typeof(战斗结算时包括继续游戏), "清场时")]
    internal static class Patch_TreasureMap4_BattleClearReward_WithContinue
    {
        private static bool Prefix(战斗结算时包括继续游戏 __instance)
        {
            if (__instance?.buff?.data == null || __instance.buff.data.id != "藏宝图四")
            {
                return true;
            }

            bool handled = Cinderia_Mod_Item_Legacy.TryHandleTreasureMap4BattleClearReward(__instance);
            return !handled;
        }
    }

}
