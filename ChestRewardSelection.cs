using Cysharp.Threading.Tasks;
using HarmonyLib;
using Rogue;
using Rogue.Buffs;
using Rogue.Data;
using Rogue.Global;
using Rogue.Items;
using Rogue.Units;
using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using UI;
using UnityEngine;
using DataMagicCard = Rogue.Data.MagicCard;

namespace Cinderia_Mod_Item_Legacy
{
    [HarmonyPatch(typeof(道具宝箱大), "获得奖励")]
    internal static class Patch_ChestRewardSelection
    {
        private static bool Prefix(道具宝箱大 __instance, ref UniTask __result)
        {
            if (!ChestRewardSelectionManager.是否支持自选(__instance))
            {
                return true;
            }

            __result = ChestRewardSelectionManager.执行自选开箱流程(__instance);
            return false;
        }
    }

    internal static class ChestRewardSelectionManager
    {
        private static readonly AccessTools.FieldRef<道具宝箱大, int?> 指定宝箱等级字段 =
            AccessTools.FieldRefAccess<道具宝箱大, int?>("指定宝箱等级");
        private static readonly AccessTools.FieldRef<Rogue.Units.Unit, UnitEvent> 单位事件字段 =
            AccessTools.FieldRefAccess<Rogue.Units.Unit, UnitEvent>("unitEvent");
        private static readonly AccessTools.FieldRef<可拾取物, bool> 已拾取字段 =
            AccessTools.FieldRefAccess<可拾取物, bool>("isPicked");
        private static readonly AccessTools.FieldRef<可拾取物, string> 主角拾取动作名字段 =
            AccessTools.FieldRefAccess<可拾取物, string>("主角拾取动作名");

        internal static bool 是否支持自选(道具宝箱大 宝箱)
        {
            if (!Cinderia_Mod_Item_Legacy.是否启用自选开箱())
            {
                return false;
            }

            if (宝箱 == null)
            {
                return false;
            }

            Type 类型 = 宝箱.GetType();
            return 类型 == typeof(道具宝箱大) || 类型 == typeof(海盗宝箱中);
        }

        internal static async UniTask 执行自选开箱流程(道具宝箱大 宝箱)
        {
            bool 已执行拾取基类逻辑 = false;
            开箱候选上下文 上下文 = null;

            try
            {
                上下文 = 构建候选上下文(宝箱);
                I可交互.锁 = true;
                await 执行可拾取物拾取前置(宝箱);
                已执行拾取基类逻辑 = true;

                if (上下文 == null || 上下文.候选道具.Count == 0)
                {
                    DataMagicCard 回退道具 = 宝箱.获取要爆的道具();
                    I可交互.锁 = false;
                    await 播放开箱并掉落奖励(宝箱, 回退道具);
                    return;
                }

                DataMagicCard 选中道具 = null;

                try
                {
                    Character.Inst.进入无敌("UI");
                    选中道具 = await ChestRewardSelectionOverlay.显示并等待选择(
                        上下文.候选道具,
                        上下文.品质等级,
                        上下文.显示名称);
                }
                catch (Exception ex)
                {
                    Cinderia_Mod_Item_Legacy.Log.LogError("[ChestRewardSelection] 选择界面异常，将回退为原版等权随机。 " + ex);
                }
                finally
                {
                    if (Character.Inst != null)
                    {
                        Character.Inst.结束无敌("UI");
                    }
                }

                if (选中道具 == null)
                {
                    选中道具 = 等权随机候选道具(上下文.候选道具);
                }

                DataMagicCard 最终道具 = 结算最终道具(宝箱, 选中道具) ?? 宝箱.获取要爆的道具();
                I可交互.锁 = false;
                await 播放开箱并掉落奖励(宝箱, 最终道具);
            }
            catch (Exception ex)
            {
                Cinderia_Mod_Item_Legacy.Log.LogError("[ChestRewardSelection] 自选开箱流程异常。 " + ex);

                if (Character.Inst != null)
                {
                    Character.Inst.结束无敌("UI");
                }

                I可交互.锁 = false;

                if (!已执行拾取基类逻辑)
                {
                    await 执行可拾取物拾取前置(宝箱);
                    已执行拾取基类逻辑 = true;
                }

                DataMagicCard 回退道具 = null;
                if (上下文 != null && 上下文.候选道具.Count > 0)
                {
                    回退道具 = 等权随机候选道具(上下文.候选道具);
                    回退道具 = 结算最终道具(宝箱, 回退道具) ?? 宝箱.获取要爆的道具();
                }
                else
                {
                    回退道具 = 宝箱.获取要爆的道具();
                }

                await 播放开箱并掉落奖励(宝箱, 回退道具);
            }
        }

        private static async UniTask 执行可拾取物拾取前置(道具宝箱大 宝箱)
        {
            可拾取物 可拾取宝箱 = 宝箱;
            已拾取字段(可拾取宝箱) = true;

            if (Character.Inst != null && Character.Inst.castingSkill)
            {
                Character.Inst.castingSkill.Interrupt("捡东西");
                await UniTask.NextFrame();
            }

            if (可拾取宝箱.拾取时直接消失)
            {
                if (可拾取宝箱.拾取特效)
                {
                    可拾取宝箱.拾取特效.transform.parent = null;
                }

                可拾取宝箱.gameObject.SetActive(false);
                _ = UniTaskUtils.Split(async delegate
                {
                    await UniTaskUtils.Delay(5f, null, false);
                    if (可拾取宝箱)
                    {
                        if (可拾取宝箱.拾取特效)
                        {
                            可拾取宝箱.拾取特效.transform.parent = 可拾取宝箱.transform;
                        }

                        if (可拾取宝箱.gameObject)
                        {
                            UnityEngine.Object.Destroy(可拾取宝箱.gameObject);
                        }
                    }
                });
            }

            if (可拾取宝箱.拾取特效)
            {
                可拾取宝箱.拾取特效.gameObject.SetActive(true);
                可拾取宝箱.拾取特效.Play(true);
            }

            string 主角拾取动作名 = 主角拾取动作名字段(可拾取宝箱);
            if (主角拾取动作名.HasValue() && Character.Inst != null)
            {
                Character 主角 = Game.Inst.Character;
                主角.进入接管("拾取");
                主角.SetForward((可拾取宝箱.transform.position - 主角).ChangeZ(0f).normalized, false, false);
                主角.Play(主角拾取动作名, false, new Vector3?(主角.transform.forward), 1f, 0, false, false, false, 1f);
                _ = UniTaskUtils.Split(async delegate
                {
                    await UniTaskUtils.Delay(主角.GetAnimEndTime(), null, false);
                    await UniTaskUtils.WaitUntil(() => !I可交互.锁, null);
                    if (主角)
                    {
                        主角.结束接管("拾取");
                    }
                });
            }
        }

        private static 开箱候选上下文 构建候选上下文(道具宝箱大 宝箱)
        {
            string 掉落类型 = 获取掉落类型(宝箱);
            if (string.IsNullOrEmpty(掉落类型))
            {
                return null;
            }

            Drop 爆道具数据 = 道具.获取爆道具数据(FateManager.限制后Chapter, 掉落类型);
            if (爆道具数据 == null)
            {
                return null;
            }

            int? 指定等级 = null;
            if (宝箱.GetType() == typeof(道具宝箱大))
            {
                指定等级 = 指定宝箱等级字段(宝箱);
            }

            int 品质等级 = 指定等级 ?? 获取道具随机等级(爆道具数据);
            List<DataMagicCard> 候选道具 = 获取候选道具池(品质等级);

            if (候选道具.Count == 0)
            {
                Cinderia_Mod_Item_Legacy.Log.LogWarning("[ChestRewardSelection] 候选池为空，类型=" + 掉落类型 + "，品质=" + 品质等级);
                return null;
            }

            return new 开箱候选上下文
            {
                显示名称 = 获取宝箱显示名称(宝箱),
                掉落类型 = 掉落类型,
                品质等级 = 品质等级,
                候选道具 = 候选道具
            };
        }

        private static string 获取掉落类型(道具宝箱大 宝箱)
        {
            Type 类型 = 宝箱.GetType();
            if (类型 == typeof(道具宝箱大))
            {
                return "宝箱房权重";
            }

            if (类型 == typeof(海盗宝箱小))
            {
                return "海盗宝箱小";
            }

            if (类型 == typeof(海盗宝箱中))
            {
                return "海盗宝箱中";
            }

            return null;
        }

        private static string 获取宝箱显示名称(道具宝箱大 宝箱)
        {
            Type 类型 = 宝箱.GetType();
            if (类型 == typeof(海盗宝箱小))
            {
                return "破烂海盗宝箱";
            }

            if (类型 == typeof(海盗宝箱中))
            {
                return "普通海盗宝箱";
            }

            return "宝箱";
        }

        private static int 获取道具随机等级(Drop data)
        {
            return RandomUtils.PseudoRandom<int>("道具随机等级", true, new ValueTuple<int, float>[]
            {
                new ValueTuple<int, float>(0, data.白权重),
                new ValueTuple<int, float>(1, data.绿权重),
                new ValueTuple<int, float>(2, data.蓝权重),
                new ValueTuple<int, float>(3, data.紫权重),
                new ValueTuple<int, float>(4, data.橙权重)
            });
        }

        private static List<DataMagicCard> 获取候选道具池(int 品质等级)
        {
            Rogue.ExcelData excel = Cinderia_Mod_Item_Legacy.获取Excel数据();
            if (excel?.magicCards == null || MagicCard_Manager.Inst == null || Game.Inst?.Character?.data == null)
            {
                return new List<DataMagicCard>();
            }

            HashSet<string> 可掉落基础名 = new HashSet<string>(
                MagicCard_Manager.Inst.剩余魔卡卡池
                    .Where(c => c != null && c.kind == "道具" && !string.IsNullOrEmpty(c.id))
                    .Select(c => 提取基础道具Id(c.id)));

            HashSet<string> 已拾取基础名 = new HashSet<string>(
                MagicCard_Manager.Inst.已拾取魔卡
                    .Where(card => card != null && card.data != null && !string.IsNullOrEmpty(card.data.id))
                    .Select(card => 提取基础道具Id(card.data.id)));

            HashSet<string> 已拾取组 = new HashSet<string>(
                MagicCard_Manager.Inst.已拾取魔卡
                    .Where(card => card != null && card.data != null)
                    .SelectMany(card => card.data.groups ?? Array.Empty<string>())
                    .Where(group => !string.IsNullOrEmpty(group)));

            if (品质等级 == 0)
            {
                return excel.magicCards
                    .Where(t => t != null && t.kind == "道具" && t.ItemLv == 0)
                    .Where(t => !(t.keyward?.Contains("没法爆出来") ?? false))
                    .Where(t => t.color.IsNullOrEmpty() || t.color == Game.Inst.Character.data.id)
                    .Where(t => !已拾取基础名.Contains(提取基础道具Id(t.id)))
                    .Where(t => !存在互斥组冲突(t, 已拾取组))
                    .Where(t => 满足前置条件(t, 已拾取组))
                    .ToList();
            }

            return excel.magicCards
                .Where(c => c != null && c.kind == "道具")
                .Where(c => 品质等级 == -1 || c.ItemLv == 品质等级)
                .Where(c => Cinderia_Mod_Item_Legacy.是否为自定义开箱候选道具(c)
                    || 可掉落基础名.Contains(提取基础道具Id(c.id)))
                .Where(c => c.color.IsNullOrEmpty() || c.color == Game.Inst.Character.data.id)
                .Where(c => Cinderia_Mod_Item_Legacy.是否为自定义开箱候选道具(c)
                    || !(c.keyward?.Contains("没法爆出来") ?? false))
                .Where(c => Cinderia_Mod_Item_Legacy.是否为自定义开箱候选道具(c) || !已拾取基础名.Contains(提取基础道具Id(c.id)))
                .Where(c => !存在互斥组冲突(c, 已拾取组))
                .Where(c => 满足前置条件(c, 已拾取组))
                .ToList();
        }

        private static bool 存在互斥组冲突(DataMagicCard 道具, HashSet<string> 已拾取组)
        {
            if (道具?.mutex == null || 道具.mutex.Length == 0)
            {
                return false;
            }

            return 道具.mutex.Any(已拾取组.Contains);
        }

        private static bool 满足前置条件(DataMagicCard 道具, HashSet<string> 已拾取组)
        {
            if (道具?.preconditions == null || 道具.preconditions.Length == 0)
            {
                return true;
            }

            if (道具.preconditions[0].IsNullOrEmpty())
            {
                return true;
            }

            if (道具.preconditions[0][0] != '☆')
            {
                return 道具.preconditions.Any(已拾取组.Contains);
            }

            return 道具.preconditions
                .Select((condition, index) => index == 0 ? condition.Substring(1) : condition)
                .All(已拾取组.Contains);
        }

        private static string 提取基础道具Id(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return id;
            }

            return new string(id.Where(c => (c < '0' || c > '9') && c != '+').ToArray());
        }

        private static DataMagicCard 等权随机候选道具(List<DataMagicCard> 候选道具)
        {
            if (候选道具 == null || 候选道具.Count == 0)
            {
                return null;
            }

            return RandomUtils.PseudoRandom<DataMagicCard>(
                "随机道具按等级",
                true,
                候选道具.Select(i => new ValueTuple<DataMagicCard, float>(i, 1f)).ToArray());
        }

        private static DataMagicCard 结算最终道具(道具宝箱大 宝箱, DataMagicCard 选中道具)
        {
            if (选中道具 == null)
            {
                return null;
            }

            if (宝箱.GetType() == typeof(道具宝箱大))
            {
                DataMagicCard 最终道具 = 开宝箱道具升等级.试着升个级(选中道具) ?? 选中道具;
                return 道具宝箱大.侵蚀不幸(new List<DataMagicCard> { 最终道具 }).FirstOrDefault() ?? 最终道具;
            }

            return 选中道具;
        }

        private static async UniTask 播放开箱并掉落奖励(道具宝箱大 宝箱, DataMagicCard 道具)
        {
            宝箱.宝箱持续发光.ParticleSystemSlowStop(1f);
            宝箱.spine.AnimationState.SetAnimation(0, "Open", false);
            宝箱.spine.AnimationState.AddAnimation(0, "Opened", true, 0f);
            FmodManager.PlaySoundByName("宝箱准备打开", 宝箱.gameObject, null, 1f, false, false);

            int 等级用来做表现 = 宝箱.获取道具等级用来做表现(道具);
            if (等级用来做表现 >= 4)
            {
                UI_道具图鉴界面.记录成就("传说道具", -1f, UI_道具图鉴界面.成就记数据方式.累加);
            }

            UI_道具图鉴界面.记录成就("宝箱", 1f, UI_道具图鉴界面.成就记数据方式.累加);
            FmodManager.PlaySoundByName(string.Format("宝箱汇聚{0}", 等级用来做表现), 宝箱.gameObject, null, 1f, false, false);

            float 命中特效时间 = 宝箱.spine.GetEventTime("Open", "Hit") - 0.1f;
            宝箱.宝箱准备打开.ParticleSystemSlowPlay(命中特效时间 * 0.25f, true, null);
            宝箱.宝箱准备打开粒子.ParticleSystemSlowPlay(命中特效时间 / 2f, true, null);

            await UniTaskUtils.Delay(命中特效时间 * 0.25f, null, false);
            宝箱.宝箱准备打开.ParticleSystemSlowStop(命中特效时间 * 0.75f);
            宝箱.宝箱准备打开颜色[等级用来做表现].ParticleSystemSlowPlay(命中特效时间 * 0.75f, true, null);

            await UniTaskUtils.Delay(命中特效时间 * 0.75f, null, false);
            宝箱.宝箱准备打开粒子.ParticleSystemSlowStop(0.1f);
            宝箱.宝箱准备打开颜色[等级用来做表现].ParticleSystemSlowStop(0.1f);
            宝箱.宝箱打开颜色[等级用来做表现].SetGameObjectActive(true);
            宝箱.宝箱打开.ParticleSystemPlay(true, null);
            FmodManager.PlaySoundByName("宝箱打开", 宝箱.gameObject, null, 1f, false, false);
            FmodManager.PlaySoundByName(string.Format("宝箱爆发{0}", 等级用来做表现), 宝箱.gameObject, null, 1f, false, false);

            await UniTaskUtils.Delay(0.1f, null, false);
            宝箱.爆奖励(道具);

            UnitEvent 单位事件 = Character.Inst != null ? 单位事件字段(Character.Inst) : null;
            Action 开宝箱时Event = 单位事件 != null ? 单位事件.开宝箱时Event : null;
            if (开宝箱时Event != null)
            {
                开宝箱时Event();
            }

            宝箱.OnDeselect(true);
            WavesManager.RewardPicked();
        }

        private sealed class 开箱候选上下文
        {
            public string 显示名称;
            public string 掉落类型;
            public int 品质等级;
            public List<DataMagicCard> 候选道具;
        }
    }

    internal sealed class ChestRewardSelectionOverlay : MonoBehaviour
    {
        private static ChestRewardSelectionOverlay _instance;

        private 选择请求 _currentRequest;
        private Font _uiFont;
        private Texture2D _panelTexture;
        private Texture2D _sectionTexture;
        private Texture2D _itemTexture;
        private Texture2D _itemHoverTexture;
        private Texture2D _itemPreviewTexture;
        private Texture2D _badgeTexture;
        private GUIStyle _windowStyle;
        private GUIStyle _sectionStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _sectionTitleStyle;
        private GUIStyle _itemTitleStyle;
        private GUIStyle _detailTitleStyle;
        private GUIStyle _detailTextStyle;
        private GUIStyle _badgeStyle;
        private GUIStyle _hintStyle;

        internal static UniTask<DataMagicCard> 显示并等待选择(List<DataMagicCard> 候选道具, int 品质等级, string 宝箱名称)
        {
            if (候选道具 == null || 候选道具.Count == 0)
            {
                return UniTask.FromResult<DataMagicCard>(null);
            }

            if (候选道具.Count == 1)
            {
                return UniTask.FromResult(候选道具[0]);
            }

            ChestRewardSelectionOverlay overlay = 获取或创建实例();
            return overlay.打开选择(候选道具, 品质等级, 宝箱名称);
        }

        private static ChestRewardSelectionOverlay 获取或创建实例()
        {
            if (_instance != null)
            {
                return _instance;
            }

            GameObject go = new GameObject("ChestRewardSelectionOverlay");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ChestRewardSelectionOverlay>();
            return _instance;
        }

        private UniTask<DataMagicCard> 打开选择(List<DataMagicCard> 候选道具, int 品质等级, string 宝箱名称)
        {
            if (_currentRequest != null)
            {
                Cinderia_Mod_Item_Legacy.Log.LogWarning("[ChestRewardSelection] 已存在未完成的选择请求，将回退为随机。");
                return UniTask.FromResult<DataMagicCard>(null);
            }

            UniTaskCompletionSource<DataMagicCard> tcs = new UniTaskCompletionSource<DataMagicCard>();
            _currentRequest = new 选择请求
            {
                候选道具 = 候选道具,
                品质等级 = 品质等级,
                宝箱名称 = 宝箱名称,
                完成源 = tcs,
                预览索引 = 0,
                滚动位置 = Vector2.zero,
                详情滚动位置 = Vector2.zero
            };
            return tcs.Task;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            if (_currentRequest != null && _currentRequest.完成源 != null)
            {
                _currentRequest.完成源.TrySetResult(null);
                _currentRequest = null;
            }
        }

        private void OnGUI()
        {
            if (_currentRequest == null)
            {
                return;
            }

            选择请求 当前请求 = _currentRequest;
            int 当前绘制预览索引 = Mathf.Clamp(当前请求.预览索引, 0, 当前请求.候选道具.Count - 1);
            int 待提交预览索引 = -1;

            初始化样式();
            GUI.depth = -1000;

            Rect 全屏区域 = new Rect(0f, 0f, Screen.width, Screen.height);
            Color 旧颜色 = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(全屏区域, Texture2D.whiteTexture);
            GUI.color = 旧颜色;

            float 面板宽 = Mathf.Min(Screen.width - 80f, 1200f);
            float 面板高 = Mathf.Min(Screen.height - 80f, 760f);
            Rect 面板区域 = new Rect(
                (Screen.width - 面板宽) * 0.5f,
                (Screen.height - 面板高) * 0.5f,
                面板宽,
                面板高);

            GUI.Box(面板区域, GUIContent.none, _windowStyle);

            Color 旧GUI颜色 = GUI.color;
            GUI.color = 获取稀有度颜色(_currentRequest.品质等级);
            GUI.DrawTexture(new Rect(面板区域.x, 面板区域.y, 面板区域.width, 6f), _badgeTexture);
            GUI.color = 旧GUI颜色;

            Rect 标题区域 = new Rect(面板区域.x + 28f, 面板区域.y + 18f, 面板区域.width - 56f, 40f);
            GUI.Label(
                标题区域,
                string.Format("{0}：选择一个{1}道具", _currentRequest.宝箱名称, 获取稀有度文本(_currentRequest.品质等级)),
                _titleStyle);

            Rect 左区域 = new Rect(面板区域.x + 24f, 面板区域.y + 72f, 面板宽 * 0.52f - 32f, 面板高 - 132f);
            Rect 右区域 = new Rect(左区域.xMax + 16f, 左区域.y, 面板区域.xMax - 24f - (左区域.xMax + 16f), 左区域.height);
            Rect 提示区域 = new Rect(面板区域.x + 28f, 面板区域.yMax - 42f, 面板区域.width - 56f, 22f);

            bool 已完成选择 = 绘制候选列表(左区域, 当前绘制预览索引, out 待提交预览索引);
            if (已完成选择)
            {
                return;
            }

            绘制详情面板(右区域, 当前绘制预览索引);
            GUI.Label(提示区域, "点击左侧条目即可领取，品质随机逻辑保持原版不变。", _hintStyle);

            if (_currentRequest == 当前请求
                && Event.current.type == EventType.Repaint
                && 待提交预览索引 >= 0
                && 待提交预览索引 != 当前请求.预览索引)
            {
                当前请求.预览索引 = 待提交预览索引;
            }
        }

        private bool 绘制候选列表(Rect 区域, int 当前绘制预览索引, out int 待提交预览索引)
        {
            DataMagicCard 待选择道具 = null;
            const int 每行数量 = 3;
            const float 卡片间距 = 8f;
            const float 卡片高度 = 104f;
            待提交预览索引 = -1;
            GUILayout.BeginArea(区域, GUIContent.none, _sectionStyle);
            GUILayout.Label("候选道具", _sectionTitleStyle);
            GUILayout.Space(10f);
            _currentRequest.滚动位置 = GUILayout.BeginScrollView(_currentRequest.滚动位置, false, true);

            for (int rowStart = 0; rowStart < _currentRequest.候选道具.Count; rowStart += 每行数量)
            {
                Rect 行区域 = GUILayoutUtility.GetRect(10f, 卡片高度, GUILayout.ExpandWidth(true));
                float 卡片宽度 = (行区域.width - 卡片间距 * (每行数量 - 1)) / 每行数量;

                for (int col = 0; col < 每行数量; col++)
                {
                    int i = rowStart + col;
                    if (i >= _currentRequest.候选道具.Count)
                    {
                        break;
                    }

                    DataMagicCard 道具 = _currentRequest.候选道具[i];
                    Rect 条目区域 = new Rect(
                        行区域.x + col * (卡片宽度 + 卡片间距),
                        行区域.y,
                        卡片宽度,
                        卡片高度);
                    bool 当前预览 = 当前绘制预览索引 == i;
                    bool 鼠标悬停 = 条目区域.Contains(Event.current.mousePosition);

                    if (鼠标悬停)
                    {
                        待提交预览索引 = i;
                    }

                    GUI.Box(条目区域, GUIContent.none, 当前预览 ? _itemPreviewTexture == null ? GUIStyle.none : 创建纯纹理样式(_itemPreviewTexture) : _itemTexture == null ? GUIStyle.none : 创建纯纹理样式(鼠标悬停 ? _itemHoverTexture : _itemTexture));

                    Color 旧颜色 = GUI.color;
                    GUI.color = 获取稀有度颜色(道具.ItemLv);
                    GUI.DrawTexture(new Rect(条目区域.x, 条目区域.y, 6f, 条目区域.height), _badgeTexture);
                    GUI.color = 旧颜色;

                    绘制道具图标(道具, new Rect(条目区域.x + (条目区域.width - 40f) * 0.5f, 条目区域.y + 12f, 40f, 40f));

                    string 名称 = Game.获取多语言_MagicCard_name(string.IsNullOrEmpty(道具.name) ? 道具.id : 道具.name);
                    GUI.Label(
                        new Rect(条目区域.x + 12f, 条目区域.y + 56f, 条目区域.width - 24f, 36f),
                        名称,
                        _itemTitleStyle);

                    if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && 条目区域.Contains(Event.current.mousePosition))
                    {
                        Event.current.Use();
                        待选择道具 = 道具;
                    }
                }

                GUILayout.Space(8f);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();

            if (待选择道具 != null)
            {
                完成选择(待选择道具);
                return true;
            }

            return false;
        }

        private void 绘制详情面板(Rect 区域, int 当前绘制预览索引)
        {
            GUILayout.BeginArea(区域, GUIContent.none, _sectionStyle);
            GUILayout.Label("道具详情", _sectionTitleStyle);
            GUILayout.Space(10f);
            _currentRequest.详情滚动位置 = GUILayout.BeginScrollView(_currentRequest.详情滚动位置, false, true);

            DataMagicCard 当前预览 = _currentRequest.候选道具[Mathf.Clamp(当前绘制预览索引, 0, _currentRequest.候选道具.Count - 1)];
            string 名称 = Game.获取多语言_MagicCard_name(string.IsNullOrEmpty(当前预览.name) ? 当前预览.id : 当前预览.name);

            GUILayout.Label(名称, _detailTitleStyle);
            GUILayout.Space(8f);

            Color 旧颜色 = GUI.color;
            GUI.color = 获取稀有度颜色(当前预览.ItemLv);
            GUILayout.Label("稀有度  " + 获取稀有度文本(当前预览.ItemLv), _badgeStyle);
            GUI.color = 旧颜色;

            string 属性文本 = 获取属性文本(当前预览);
            if (!string.IsNullOrEmpty(属性文本))
            {
                GUILayout.Space(8f);
                GUILayout.Label("属性", _detailTitleStyle);
                GUILayout.Label(属性文本, _detailTextStyle);
            }

            GUILayout.Space(8f);
            GUILayout.Label("描述", _detailTitleStyle);
            GUILayout.Label(Game.获取多语言_MagicCard_introduce(当前预览.introduce), _detailTextStyle);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void 完成选择(DataMagicCard 道具)
        {
            if (_currentRequest == null)
            {
                return;
            }

            选择请求 request = _currentRequest;
            _currentRequest = null;
            request.完成源.TrySetResult(道具);
        }

        private void 初始化样式()
        {
            if (_windowStyle != null)
            {
                return;
            }

            _windowStyle = new GUIStyle(GUI.skin.box);
            _windowStyle.padding = new RectOffset(0, 0, 0, 0);
            _windowStyle.border = new RectOffset(1, 1, 1, 1);
            _windowStyle.normal.background = _panelTexture = 创建纯色纹理(new Color(0.08f, 0.1f, 0.13f, 0.84f));

            _sectionStyle = new GUIStyle(GUI.skin.box);
            _sectionStyle.padding = new RectOffset(18, 18, 16, 16);
            _sectionStyle.normal.background = _sectionTexture = 创建纯色纹理(new Color(0.12f, 0.14f, 0.18f, 0.78f));

            _badgeTexture = 创建纯色纹理(Color.white);
            _itemTexture = 创建纯色纹理(new Color(0.14f, 0.17f, 0.22f, 1f));
            _itemHoverTexture = 创建纯色纹理(new Color(0.18f, 0.22f, 0.28f, 1f));
            _itemPreviewTexture = 创建纯色纹理(new Color(0.2f, 0.25f, 0.32f, 1f));

            _uiFont = 创建中文字体(18);

            _titleStyle = new GUIStyle(GUI.skin.label);
            _titleStyle.font = _uiFont;
            _titleStyle.fontSize = 24;
            _titleStyle.fontStyle = FontStyle.Bold;
            _titleStyle.normal.textColor = new Color(0.96f, 0.94f, 0.88f, 1f);
            _titleStyle.wordWrap = true;

            _sectionTitleStyle = new GUIStyle(GUI.skin.label);
            _sectionTitleStyle.font = _uiFont;
            _sectionTitleStyle.fontSize = 18;
            _sectionTitleStyle.fontStyle = FontStyle.Bold;
            _sectionTitleStyle.normal.textColor = new Color(0.91f, 0.9f, 0.84f, 1f);

            _itemTitleStyle = new GUIStyle(GUI.skin.label);
            _itemTitleStyle.font = _uiFont;
            _itemTitleStyle.fontSize = 18;
            _itemTitleStyle.fontStyle = FontStyle.Bold;
            _itemTitleStyle.normal.textColor = new Color(0.95f, 0.95f, 0.96f, 1f);
            _itemTitleStyle.wordWrap = true;
            _itemTitleStyle.alignment = TextAnchor.UpperCenter;

            _detailTitleStyle = new GUIStyle(GUI.skin.label);
            _detailTitleStyle.font = _uiFont;
            _detailTitleStyle.fontSize = 24;
            _detailTitleStyle.fontStyle = FontStyle.Bold;
            _detailTitleStyle.normal.textColor = new Color(0.95f, 0.95f, 0.96f, 1f);
            _detailTitleStyle.wordWrap = true;

            _detailTextStyle = new GUIStyle(GUI.skin.label);
            _detailTextStyle.font = _uiFont;
            _detailTextStyle.fontSize = 22;
            _detailTextStyle.normal.textColor = new Color(0.84f, 0.86f, 0.9f, 1f);
            _detailTextStyle.wordWrap = true;

            _badgeStyle = new GUIStyle(GUI.skin.box);
            _badgeStyle.font = _uiFont;
            _badgeStyle.fontSize = 16;
            _badgeStyle.fontStyle = FontStyle.Bold;
            _badgeStyle.alignment = TextAnchor.MiddleCenter;
            _badgeStyle.padding = new RectOffset(12, 12, 8, 8);
            _badgeStyle.normal.background = _badgeTexture;
            _badgeStyle.normal.textColor = new Color(0.08f, 0.09f, 0.11f, 1f);

            _hintStyle = new GUIStyle(GUI.skin.label);
            _hintStyle.font = _uiFont;
            _hintStyle.fontSize = 14;
            _hintStyle.normal.textColor = new Color(0.68f, 0.72f, 0.78f, 1f);
            _hintStyle.wordWrap = true;
        }

        private static GUIStyle 创建纯纹理样式(Texture2D 纹理)
        {
            return new GUIStyle
            {
                normal = { background = 纹理 }
            };
        }

        private static void 绘制道具图标(DataMagicCard 道具, Rect 区域)
        {
            Rogue.ExcelData excel = Cinderia_Mod_Item_Legacy.获取Excel数据();
            if (道具 == null || excel?.道具图标 == null)
            {
                return;
            }

            Sprite 图标;
            if (!excel.道具图标.TryGetValue(道具.icon, out 图标) || 图标 == null || 图标.texture == null)
            {
                return;
            }

            Rect uv = new Rect(
                图标.rect.x / 图标.texture.width,
                图标.rect.y / 图标.texture.height,
                图标.rect.width / 图标.texture.width,
                图标.rect.height / 图标.texture.height);
            GUI.DrawTextureWithTexCoords(区域, 图标.texture, uv, true);
        }

        private static Font 创建中文字体(int 字号)
        {
            try
            {
                return Font.CreateDynamicFontFromOSFont(
                    new[]
                    {
                        "Microsoft YaHei",
                        "Microsoft JhengHei",
                        "SimHei",
                        "SimSun",
                        "DengXian",
                        "Arial Unicode MS"
                    },
                    字号);
            }
            catch
            {
                return GUI.skin.font;
            }
        }

        private static Texture2D 创建纯色纹理(Color 颜色)
        {
            Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, 颜色);
            tex.Apply();
            return tex;
        }

        private static string 获取描述首行(DataMagicCard 道具)
        {
            string 描述 = Game.获取多语言_MagicCard_introduce(道具.introduce) ?? "";
            return 描述.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        }

        private static string 获取属性摘要(DataMagicCard data)
        {
            List<string> parts = new List<string>();
            if (data.strength > 0)
            {
                parts.Add("力+" + data.strength);
            }

            if (data.agility > 0)
            {
                parts.Add("敏+" + data.agility);
            }

            if (data.intelligence > 0)
            {
                parts.Add("精+" + data.intelligence);
            }

            return string.Join("  ", parts);
        }

        private static string 获取属性文本(DataMagicCard data)
        {
            List<string> 属性列表 = new List<string>();
            if (data.strength > 0)
            {
                属性列表.Add("+" + data.strength + " " + Game.获取多语言_其他("力量"));
            }

            if (data.agility > 0)
            {
                属性列表.Add("+" + data.agility + " " + Game.获取多语言_其他("敏捷"));
            }

            if (data.intelligence > 0)
            {
                属性列表.Add("+" + data.intelligence + " " + Game.获取多语言_其他("精神"));
            }

            return string.Join("\n", 属性列表);
        }

        private static string 获取稀有度文本(int 等级)
        {
            switch (等级)
            {
                case 0: return "白";
                case 1: return "绿";
                case 2: return "蓝";
                case 3: return "紫";
                case 4: return "橙";
                default: return 等级.ToString();
            }
        }

        private static Color 获取稀有度颜色(int 等级)
        {
            switch (等级)
            {
                case 0: return new Color(0.84f, 0.84f, 0.84f, 1f);
                case 1: return new Color(0.41f, 0.84f, 0.48f, 1f);
                case 2: return new Color(0.35f, 0.67f, 0.94f, 1f);
                case 3: return new Color(0.73f, 0.45f, 0.93f, 1f);
                case 4: return new Color(0.96f, 0.71f, 0.24f, 1f);
                default: return new Color(0.82f, 0.82f, 0.82f, 1f);
            }
        }

        private sealed class 选择请求
        {
            public List<DataMagicCard> 候选道具;
            public int 品质等级;
            public string 宝箱名称;
            public UniTaskCompletionSource<DataMagicCard> 完成源;
            public int 预览索引;
            public Vector2 滚动位置;
            public Vector2 详情滚动位置;
        }
    }
}
