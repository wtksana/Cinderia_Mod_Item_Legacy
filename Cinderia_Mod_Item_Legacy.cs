using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using HarmonyLib;
using Rogue;
using Rogue.Items;
using Rogue.Units;
using System;
using System.IO;
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

        private static string _pendingItemId = "";
        private static bool _bonusGivenThisRun;

        private static string DataFilePath
        {
            get
            {
                string modDir = Path.GetDirectoryName(typeof(Cinderia_Mod_Item_Legacy).Assembly.Location);
                if (string.IsNullOrEmpty(modDir))
                {
                    modDir = Paths.PluginPath;
                }
                return Path.Combine(modDir, "Cinderia_Mod_Item_Legacy_LastRunTopItem.txt");
            }
        }

        private void Awake()
        {
            Log = Logger;
            LoadPendingItem();

            _harmony = new Harmony("Cinderia_Mod_Item_Legacy");
            _harmony.PatchAll();

            Log.LogInfo("[Cinderia_Mod_Item_Legacy] Plugin loaded. pendingItem=" + _pendingItemId);
        }

        internal static void OnNewRunStarted()
        {
            _bonusGivenThisRun = false;
            LoadPendingItem();
            Log.LogInfo("[Cinderia_Mod_Item_Legacy] New run started. pendingItem=" + _pendingItemId);
        }

        internal static void CaptureTopItemAtRunEnd(string source)
        {
            try
            {
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

                _pendingItemId = top != null && top.data != null ? top.data.id : "";
                SavePendingItem();

                if (!string.IsNullOrEmpty(_pendingItemId))
                {
                    Log.LogInfo("[Cinderia_Mod_Item_Legacy] Captured top item at run end. source=" + source + ", id=" + _pendingItemId + ", lv=" + bestLv);
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
                if (_bonusGivenThisRun)
                    return;

                if (string.IsNullOrEmpty(_pendingItemId))
                    return;

                if (Game.Inst == null)
                    return;

                MagicCardData data = MagicCard_Manager.id找data(_pendingItemId);
                if (data == null)
                {
                    Log.LogWarning("[Cinderia_Mod_Item_Legacy] Pending item id not found: " + _pendingItemId);
                    _pendingItemId = "";
                    SavePendingItem();
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
                _pendingItemId = "";
                SavePendingItem();

                Log.LogInfo("[Cinderia_Mod_Item_Legacy] Dropped pending item. source=" + source + ", id=" + data.id);
            }
            catch (Exception ex)
            {
                Log.LogError("[Cinderia_Mod_Item_Legacy] TryDropPendingItem error: " + ex);
            }
        }

        private static void LoadPendingItem()
        {
            try
            {
                _pendingItemId = "";
                if (!File.Exists(DataFilePath))
                    return;

                string text = File.ReadAllText(DataFilePath).Trim();
                _pendingItemId = text;
            }
            catch (Exception ex)
            {
                Log.LogError("[Cinderia_Mod_Item_Legacy] LoadPendingItem error: " + ex);
                _pendingItemId = "";
            }
        }

        private static void SavePendingItem()
        {
            try
            {
                File.WriteAllText(DataFilePath, _pendingItemId ?? "");
            }
            catch (Exception ex)
            {
                Log.LogError("[Cinderia_Mod_Item_Legacy] SavePendingItem error: " + ex);
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

    // 第一次开普通道具宝箱后：额外掉落记录道具
    [HarmonyPatch(typeof(道具宝箱大), "获得奖励")]
    internal static class Patch_DropPendingOnFirstNormalChest
    {
        private static void Postfix(道具宝箱大 __instance)
        {
            Vector3 pos = __instance != null && __instance.爆金币点 != null
                ? __instance.爆金币点.position
                : (__instance != null ? __instance.transform.position : Vector3.zero);

            Cinderia_Mod_Item_Legacy.TryDropPendingItem(pos, "道具宝箱大.获得奖励.Postfix");
        }
    }

    // 第一次开侵蚀宝箱后：额外掉落记录道具
    [HarmonyPatch(typeof(侵蚀宝箱), "爆资源")]
    internal static class Patch_DropPendingOnFirstHellChest
    {
        private static void Postfix(侵蚀宝箱 __instance)
        {
            Vector3 pos = __instance != null && __instance.爆金币点 != null
                ? __instance.爆金币点.position
                : (__instance != null ? __instance.transform.position : Vector3.zero);

            Cinderia_Mod_Item_Legacy.TryDropPendingItem(pos, "侵蚀宝箱.爆资源.Postfix");
        }
    }

}
