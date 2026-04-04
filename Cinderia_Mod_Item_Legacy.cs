using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using HarmonyLib;
using Newtonsoft.Json;
using Rogue;
using Rogue.Items;
using Rogue.NPCs;
using Rogue.Units;
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using MagicCardData = Rogue.Data.MagicCard;
using RuntimeMagicCard = Rogue.MagicCard;

namespace Cinderia_Mod_Item_Legacy
{
    [BepInPlugin("Cinderia_Mod_Item_Legacy", "Cinderia_Mod_Item_Legacy", "1.1.0")]
    public class Cinderia_Mod_Item_Legacy : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        private Harmony _harmony;
        private static ConfigFile _configFile;

        private static bool _bonusGivenThisRun;
        private static bool _itemDumpDoneThisSession;
        private static bool _treasureMap4TweakedThisSession;

        // ===== 藏宝图4配置（BepInEx cfg） =====
        private static ConfigEntry<float> Cfg_TreasureMap4_战斗结算触发概率;
        private static ConfigEntry<float> Cfg_TreasureMap4_小宝箱最终概率;
        private static ConfigEntry<float> Cfg_TreasureMap4_中宝箱最终概率;
        private static ConfigEntry<float> Cfg_TreasureMap4_大宝箱最终概率;

        // ===== 额外掉落记录道具配置（BepInEx cfg） =====
        private static ConfigEntry<bool> Cfg_额外掉落记录道具_启用;
        private static ConfigEntry<string> Cfg_额外掉落记录道具_ID;

        private static string ItemDumpFilePath
        {
            get
            {
                string modDir = Path.GetDirectoryName(typeof(Cinderia_Mod_Item_Legacy).Assembly.Location);
                if (string.IsNullOrEmpty(modDir))
                {
                    modDir = Paths.PluginPath;
                }
                return Path.Combine(modDir, "Cinderia_Mod_Item_Legacy_AllItems.json");
            }
        }

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

            DumpAllItemsToFile();
            ApplyTreasureMap4Tweaks();
        }

        internal static void ApplyTreasureMap4Tweaks()
        {
            try
            {
                if (_treasureMap4TweakedThisSession)
                    return;

                if (Game.Inst == null || Game.Inst.excel == null || Game.Inst.excel.magicCards == null || Game.Inst.excel.buffs == null)
                {
                    Log.LogWarning("[Cinderia_Mod_Item_Legacy] ApplyTreasureMap4Tweaks skipped, excel not ready.");
                    return;
                }

                // 1) 找到道具数据
                MagicCardData map4 = Game.Inst.excel.magicCards.FirstOrDefault(c => c != null && c.id == "藏宝图4");
                if (map4 == null)
                {
                    Log.LogWarning("[Cinderia_Mod_Item_Legacy] Item not found: 藏宝图4");
                    _treasureMap4TweakedThisSession = true;
                    return;
                }

                // 2) 改藏宝图四触发与子概率
                Rogue.Data.Buff buff藏宝图四 = Game.Inst.excel.buffs.FirstOrDefault(b => b != null && b.id == "藏宝图四");

                if (buff藏宝图四 != null)
                {
                    float triggerChance = Mathf.Clamp01(Cfg_TreasureMap4_战斗结算触发概率?.Value ?? 0.8f);
                    float finalSmallTarget = Mathf.Max(0f, Cfg_TreasureMap4_小宝箱最终概率?.Value ?? 0.45f);
                    float finalMidTarget = Mathf.Max(0f, Cfg_TreasureMap4_中宝箱最终概率?.Value ?? 0.25f);
                    float finalBigTarget = Mathf.Max(0f, Cfg_TreasureMap4_大宝箱最终概率?.Value ?? 0.10f);

                    float oldTriggerChance = buff藏宝图四.triggerChance;
                    buff藏宝图四.triggerChance = triggerChance;
                    Log.LogInfo("[Cinderia_Mod_Item_Legacy] [藏宝图四] triggerChance: " + oldTriggerChance + " -> " + buff藏宝图四.triggerChance);

                    // 这个 buff 是通过 skill 字段触发具体“刷宝箱标记”逻辑
                    if (!string.IsNullOrEmpty(buff藏宝图四.skill) && Game.Inst.excel.skills != null)
                    {
                        Rogue.Data.Skill skill藏宝图四 = Game.Inst.excel.skills.FirstOrDefault(s => s != null && s.id == buff藏宝图四.skill);
                        if (skill藏宝图四 != null)
                        {
                            // 继续深入查看子技能（真正分小/中/大宝箱概率很可能在子技能里）
                            if (skill藏宝图四.son != null)
                            {
                                foreach (string childSkillId in skill藏宝图四.son)
                                {
                                    Rogue.Data.Skill childSkill = Game.Inst.excel.skills.FirstOrDefault(s => s != null && s.id == childSkillId);
                                    if (childSkill == null)
                                    {
                                        Log.LogWarning("[Cinderia_Mod_Item_Legacy] Child skill not found: " + childSkillId);
                                        continue;
                                    }

                                    if (childSkill.sonChance != null && childSkill.sonChance.Length >= 3)
                                    {
                                        float[] oldChild = childSkill.sonChance.ToArray();
                                        float[] solvedSonChance = SolveTreasureMapSonChance(
                                            triggerChance,
                                            finalSmallTarget,
                                            finalMidTarget,
                                            finalBigTarget);

                                        childSkill.sonChance[0] = solvedSonChance[0];
                                        childSkill.sonChance[1] = solvedSonChance[1];
                                        childSkill.sonChance[2] = solvedSonChance[2];
                                        Log.LogInfo("[Cinderia_Mod_Item_Legacy] [藏宝图四] child sonChance changed: ["
                                            + string.Join(", ", oldChild.Select(v => v.ToString("0.###")))
                                            + "] -> ["
                                            + string.Join(", ", childSkill.sonChance.Select(v => v.ToString("0.###")))
                                            + "]");

                                        float finalSmall = triggerChance * childSkill.sonChance[0];
                                        float finalMid = triggerChance * (1f - childSkill.sonChance[0]) * childSkill.sonChance[1];
                                        float finalBig = triggerChance * (1f - childSkill.sonChance[0]) * (1f - childSkill.sonChance[1]) * childSkill.sonChance[2];
                                        Log.LogInfo("[Cinderia_Mod_Item_Legacy] [藏宝图四] final rates => 小:"
                                            + finalSmall.ToString("0.###")
                                            + ", 中:" + finalMid.ToString("0.###")
                                            + ", 大:" + finalBig.ToString("0.###")
                                            + ", 总:" + (finalSmall + finalMid + finalBig).ToString("0.###"));

                                        map4.introduce = BuildTreasureMap4Introduce(finalSmall, finalMid, finalBig);
                                    }
                                }
                            }
                        }
                        else
                        {
                            Log.LogWarning("[Cinderia_Mod_Item_Legacy] Skill not found: " + buff藏宝图四.skill);
                        }
                    }
                }
                else
                {
                    Log.LogWarning("[Cinderia_Mod_Item_Legacy] Buff not found: 藏宝图四");
                }
                Log.LogInfo("[Cinderia_Mod_Item_Legacy] [藏宝图4] 新描述: " + (map4.introduce ?? ""));

                _treasureMap4TweakedThisSession = true;
            }
            catch (Exception ex)
            {
                Log.LogError("[Cinderia_Mod_Item_Legacy] ApplyTreasureMap4Tweaks error: " + ex);
            }
        }

        private void InitConfig()
        {
            const string section = "TreasureMap4";
            Cfg_TreasureMap4_战斗结算触发概率 = Config.Bind(section, "战斗结算触发概率", 0.8f, "藏宝图四buff的triggerChance（0~1）");
            Cfg_TreasureMap4_小宝箱最终概率 = Config.Bind(section, "小宝箱最终概率", 0.45f, "清空房间后最终获得小海盗宝箱的概率（0~1）");
            Cfg_TreasureMap4_中宝箱最终概率 = Config.Bind(section, "中宝箱最终概率", 0.25f, "清空房间后最终获得中海盗宝箱的概率（0~1）");
            Cfg_TreasureMap4_大宝箱最终概率 = Config.Bind(section, "大宝箱最终概率", 0.10f, "清空房间后最终获得大海盗宝箱的概率（0~1）");

            const string dropSection = "LegacyDrop";
            Cfg_额外掉落记录道具_启用 = Config.Bind(dropSection, "启用", true, "是否启用‘每局结束记录最高道具，并在下局第一次开宝箱时额外掉落该道具’");
            Cfg_额外掉落记录道具_ID = Config.Bind(dropSection, "记录道具ID", "", "内部使用：记录上局最高道具ID。可手动填写以指定下次额外掉落道具。留空表示无。") ;

            Log.LogInfo("[Cinderia_Mod_Item_Legacy] Config loaded. trigger="
                + Cfg_TreasureMap4_战斗结算触发概率.Value.ToString("0.###")
                + ", small=" + Cfg_TreasureMap4_小宝箱最终概率.Value.ToString("0.###")
                + ", middle=" + Cfg_TreasureMap4_中宝箱最终概率.Value.ToString("0.###")
                + ", big=" + Cfg_TreasureMap4_大宝箱最终概率.Value.ToString("0.###")
                + ", legacyDropEnabled=" + Cfg_额外掉落记录道具_启用.Value
                + ", pendingItem=" + (Cfg_额外掉落记录道具_ID.Value ?? ""));
        }

        internal static void DumpAllItemsToFile()
        {
            try
            {
                if (_itemDumpDoneThisSession)
                {
                    return;
                }

                if (Game.Inst == null || Game.Inst.excel == null || Game.Inst.excel.magicCards == null)
                {
                    Log.LogWarning("[Cinderia_Mod_Item_Legacy] DumpAllItemsToFile skipped, excel not ready.");
                    return;
                }

                MagicCardData[] allCards = Game.Inst.excel.magicCards;
                MagicCardData[] items = allCards
                    .Where(c => c != null && c.kind == "道具")
                    .OrderBy(c => c.ItemLv)
                    .ThenBy(c => c.id)
                    .ToArray();

                File.WriteAllText(ItemDumpFilePath, ToJson(items));

                _itemDumpDoneThisSession = true;
                Log.LogInfo("[Cinderia_Mod_Item_Legacy] Item dump saved: " + ItemDumpFilePath + " (count=" + items.Length + ")");
            }
            catch (Exception ex)
            {
                Log.LogError("[Cinderia_Mod_Item_Legacy] DumpAllItemsToFile error: " + ex);
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

        private static float[] SolveTreasureMapSonChance(float triggerChance, float finalSmall, float finalMid, float finalBig)
        {
            float t = Mathf.Clamp01(triggerChance);
            float fs = Mathf.Max(0f, finalSmall);
            float fm = Mathf.Max(0f, finalMid);
            float fb = Mathf.Max(0f, finalBig);

            if (t <= 0f)
            {
                Log.LogWarning("[Cinderia_Mod_Item_Legacy] triggerChance <= 0，无法命中任何宝箱概率，sonChance将置0。");
                return new[] { 0f, 0f, 0f };
            }

            float total = fs + fm + fb;
            if (total > t)
            {
                float scale = t / total;
                fs *= scale;
                fm *= scale;
                fb *= scale;
                Log.LogWarning("[Cinderia_Mod_Item_Legacy] 目标最终概率之和超过triggerChance，已按比例缩放。scale=" + scale.ToString("0.###"));
            }

            float s0 = Mathf.Clamp01(fs / t);

            float remainAfterS0 = Mathf.Max(0f, t - fs);
            float s1 = remainAfterS0 > 1e-6f ? Mathf.Clamp01(fm / remainAfterS0) : 0f;

            float remainAfterS1 = Mathf.Max(0f, remainAfterS0 - fm);
            float s2 = remainAfterS1 > 1e-6f ? Mathf.Clamp01(fb / remainAfterS1) : 0f;

            return new[] { s0, s1, s2 };
        }

        private static string BuildTreasureMap4Introduce(float finalSmall, float finalMid, float finalBig)
        {
            return "清空房间：有" + ToPercentText(finalSmall) + "几率找到一个破烂海盗宝箱。\n"
                + "清空房间：有" + ToPercentText(finalMid) + "几率找到一个普通海盗宝箱。\n"
                + "清空房间：有" + ToPercentText(finalBig) + "几率找到一个高级海盗宝箱。\n"
                + "装备：打开宝箱时获得1点随机属性。";
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

    // 新局开始：重置“本局是否已发奖励”标记
    [HarmonyPatch(typeof(MagicCard_Manager), "加载英雄表")]
    internal static class Patch_ResetPerRunState
    {
        private static void Postfix()
        {
            Cinderia_Mod_Item_Legacy.OnNewRunStarted();
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

}
