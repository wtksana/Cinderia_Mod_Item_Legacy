using BepInEx.Configuration;
using Cysharp.Threading.Tasks;
using Rogue;
using Rogue.Items;
using Rogue.Units;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DataMagicCard = Rogue.Data.MagicCard;

namespace Cinderia_Mod_Item_Legacy
{
    internal class 配置描述
    {
        public string 名称;
        public string 说明;
        public string 生效时机;
    }

    /// <summary>
    /// F9 配置界面和 F10 道具选择界面
    /// </summary>
    internal class DebugGUI : MonoBehaviour
    {
        private static DebugGUI _instance;
        private bool _显示配置界面;
        private bool _显示道具选择界面;
        private Vector2 _配置滚动位置;
        private Vector2 _道具滚动位置;
        private Vector2 _道具详情滚动位置;

        // 配置界面临时值
        private Dictionary<ConfigEntryBase, string> _配置临时值 = new Dictionary<ConfigEntryBase, string>();
        private Dictionary<string, 配置描述> _配置描述映射;

        // 样式相关
        private Font _uiFont;
        private GUIStyle _窗口样式;
        private GUIStyle _标题样式;
        private GUIStyle _配置标签样式;
        private GUIStyle _按钮样式;
        private GUIStyle _道具按钮样式;
        private GUIStyle _道具按钮悬停样式;
        private GUIStyle _输入框样式;
        private GUIStyle _切换按钮样式;
        private GUIStyle _sectionStyle;
        private GUIStyle _sectionTitleStyle;
        private GUIStyle _itemTitleStyle;
        private GUIStyle _detailTitleStyle;
        private GUIStyle _detailTextStyle;
        private GUIStyle _badgeStyle;
        private Texture2D _窗口背景;
        private Texture2D _按钮背景;
        private Texture2D _按钮悬停背景;
        private Texture2D _道具按钮背景;
        private Texture2D _道具按钮悬停背景;
        private Texture2D _输入框背景;
        private Texture2D _sectionTexture;
        private Texture2D _itemTexture;
        private Texture2D _itemHoverTexture;
        private Texture2D _itemPreviewTexture;
        private Texture2D _badgeTexture;

        // 道具选择相关
        private List<DataMagicCard> _候选道具列表;
        private int _当前道具等级过滤 = -1; // -1 表示显示所有等级
        private int _当前预览索引 = 0;

        internal static void 初始化()
        {
            if (_instance != null) return;

            GameObject go = new GameObject("Cinderia_Mod_DebugGUI");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<DebugGUI>();
        }

        private void 初始化配置描述()
        {
            _配置描述映射 = new Dictionary<string, 配置描述>
            {
                ["Duplicator.启用"] = new 配置描述 { 名称 = "复制器 - 启用", 说明 = "是否启用复制器道具功能", 生效时机 = "下一局生效" },
                ["Duplicator.绿概率"] = new 配置描述 { 名称 = "复制器 - 绿色概率", 说明 = "绿色复制器复制房间奖励的概率 (0-1)", 生效时机 = "立即生效" },
                ["Duplicator.蓝概率"] = new 配置描述 { 名称 = "复制器 - 蓝色概率", 说明 = "蓝色复制器复制房间奖励的概率 (0-1)", 生效时机 = "立即生效" },
                ["Duplicator.紫概率"] = new 配置描述 { 名称 = "复制器 - 紫色概率", 说明 = "紫色复制器复制房间奖励的概率 (0-1)", 生效时机 = "立即生效" },
                ["Duplicator.橙概率"] = new 配置描述 { 名称 = "复制器 - 橙色概率", 说明 = "橙色复制器复制房间奖励的概率 (0-1)", 生效时机 = "立即生效" },
                ["ChestSelection.启用"] = new 配置描述 { 名称 = "自选开箱 - 启用", 说明 = "开宝箱时是否弹出道具选择界面", 生效时机 = "立即生效" },
                ["LegacyInheritance.启用"] = new 配置描述 { 名称 = "上局继承 - 启用", 说明 = "下局首房间是否弹出上局道具继承选择", 生效时机 = "下一局生效" },
                ["LegacyInheritance.候选道具列表"] = new 配置描述 { 名称 = "上局继承 - 候选列表", 说明 = "内部记录上局道具，逗号分隔 (自动维护)", 生效时机 = "下一局生效" },
                ["TreasureMap.藏宝图4_小宝箱概率"] = new 配置描述 { 名称 = "藏宝图4 - 小宝箱概率", 说明 = "藏宝图4清场掉小宝箱的权重 (0-1)", 生效时机 = "立即生效" },
                ["TreasureMap.藏宝图4_中宝箱概率"] = new 配置描述 { 名称 = "藏宝图4 - 中宝箱概率", 说明 = "藏宝图4清场掉中宝箱的权重 (0-1)", 生效时机 = "立即生效" },
                ["TreasureMap.藏宝图4_大宝箱概率"] = new 配置描述 { 名称 = "藏宝图4 - 大宝箱概率", 说明 = "藏宝图4清场掉大宝箱的权重 (0-1)", 生效时机 = "立即生效" },
                ["SkillSelection.额外刷新次数"] = new 配置描述 { 名称 = "技能选择 - 额外刷新", 说明 = "技能选择界面额外增加的刷新次数", 生效时机 = "下一局生效" }
            };
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F9))
            {
                _显示配置界面 = !_显示配置界面;
                if (_显示配置界面)
                {
                    初始化配置描述();
                    初始化配置临时值();
                }
            }

            if (Input.GetKeyDown(KeyCode.F10))
            {
                _显示道具选择界面 = !_显示道具选择界面;
                if (_显示道具选择界面)
                {
                    刷新候选道具列表();
                }
            }
        }

        private void OnGUI()
        {
            if (_显示配置界面)
            {
                初始化样式();
                绘制配置界面();
            }

            if (_显示道具选择界面)
            {
                初始化样式();
                绘制道具选择界面();
            }
        }

        private void 初始化样式()
        {
            if (_窗口样式 != null) return;

            _uiFont = 创建中文字体(18);

            // 创建纹理
            _窗口背景 = 创建纯色纹理(new Color(0.08f, 0.1f, 0.13f, 0.95f));
            _按钮背景 = 创建纯色纹理(new Color(0.2f, 0.25f, 0.32f, 1f));
            _按钮悬停背景 = 创建纯色纹理(new Color(0.25f, 0.32f, 0.42f, 1f));
            _道具按钮背景 = 创建纯色纹理(new Color(0.14f, 0.17f, 0.22f, 1f));
            _道具按钮悬停背景 = 创建纯色纹理(new Color(0.18f, 0.22f, 0.28f, 1f));
            _输入框背景 = 创建纯色纹理(new Color(0.12f, 0.14f, 0.18f, 1f));
            _sectionTexture = 创建纯色纹理(new Color(0.12f, 0.14f, 0.18f, 0.78f));
            _itemTexture = 创建纯色纹理(new Color(0.14f, 0.17f, 0.22f, 1f));
            _itemHoverTexture = 创建纯色纹理(new Color(0.18f, 0.22f, 0.28f, 1f));
            _itemPreviewTexture = 创建纯色纹理(new Color(0.2f, 0.25f, 0.32f, 1f));
            _badgeTexture = 创建纯色纹理(Color.white);

            // 窗口样式
            _窗口样式 = new GUIStyle(GUI.skin.box);
            _窗口样式.normal.background = _窗口背景;
            _窗口样式.padding = new RectOffset(20, 20, 20, 20);

            // 标题样式
            _标题样式 = new GUIStyle(GUI.skin.label);
            _标题样式.font = _uiFont;
            _标题样式.fontSize = 28;
            _标题样式.fontStyle = FontStyle.Bold;
            _标题样式.normal.textColor = new Color(0.96f, 0.94f, 0.88f, 1f);
            _标题样式.alignment = TextAnchor.MiddleCenter;

            // 配置标签样式
            _配置标签样式 = new GUIStyle(GUI.skin.label);
            _配置标签样式.font = _uiFont;
            _配置标签样式.fontSize = 20;
            _配置标签样式.normal.textColor = new Color(0.91f, 0.9f, 0.84f, 1f);

            // 按钮样式
            _按钮样式 = new GUIStyle(GUI.skin.button);
            _按钮样式.font = _uiFont;
            _按钮样式.fontSize = 18;
            _按钮样式.fontStyle = FontStyle.Bold;
            _按钮样式.normal.background = _按钮背景;
            _按钮样式.hover.background = _按钮悬停背景;
            _按钮样式.normal.textColor = new Color(0.95f, 0.95f, 0.96f, 1f);
            _按钮样式.padding = new RectOffset(12, 12, 8, 8);

            // 道具按钮样式
            _道具按钮样式 = new GUIStyle(GUI.skin.button);
            _道具按钮样式.font = _uiFont;
            _道具按钮样式.fontSize = 16;
            _道具按钮样式.normal.background = _道具按钮背景;
            _道具按钮样式.hover.background = _道具按钮悬停背景;
            _道具按钮样式.normal.textColor = new Color(0.95f, 0.95f, 0.96f, 1f);
            _道具按钮样式.alignment = TextAnchor.MiddleLeft;
            _道具按钮样式.padding = new RectOffset(12, 12, 8, 8);

            _道具按钮悬停样式 = new GUIStyle(_道具按钮样式);
            _道具按钮悬停样式.normal.background = _按钮悬停背景;

            // 输入框样式
            _输入框样式 = new GUIStyle(GUI.skin.textField);
            _输入框样式.font = _uiFont;
            _输入框样式.fontSize = 18;
            _输入框样式.normal.background = _输入框背景;
            _输入框样式.normal.textColor = new Color(0.95f, 0.95f, 0.96f, 1f);
            _输入框样式.padding = new RectOffset(10, 10, 8, 8);
            _输入框样式.alignment = TextAnchor.MiddleLeft;

            // 切换按钮样式
            _切换按钮样式 = new GUIStyle(GUI.skin.toggle);
            _切换按钮样式.font = _uiFont;
            _切换按钮样式.fontSize = 20;
            _切换按钮样式.normal.textColor = new Color(0.91f, 0.9f, 0.84f, 1f);

            // Section 样式
            _sectionStyle = new GUIStyle(GUI.skin.box);
            _sectionStyle.padding = new RectOffset(18, 18, 16, 16);
            _sectionStyle.normal.background = _sectionTexture;

            _sectionTitleStyle = new GUIStyle(GUI.skin.label);
            _sectionTitleStyle.font = _uiFont;
            _sectionTitleStyle.fontSize = 18;
            _sectionTitleStyle.fontStyle = FontStyle.Bold;
            _sectionTitleStyle.normal.textColor = new Color(0.91f, 0.9f, 0.84f, 1f);

            _itemTitleStyle = new GUIStyle(GUI.skin.label);
            _itemTitleStyle.font = _uiFont;
            _itemTitleStyle.fontSize = 16;
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
            _detailTextStyle.fontSize = 20;
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
        }

        private void 初始化配置临时值()
        {
            _配置临时值.Clear();
            var 所有配置 = Cinderia_Mod_Item_Legacy.获取所有配置();
            foreach (var cfg in 所有配置)
            {
                _配置临时值[cfg] = cfg.BoxedValue?.ToString() ?? "";
            }
        }

        private void 刷新候选道具列表()
        {
            try
            {
                if (_当前道具等级过滤 < 0)
                {
                    _候选道具列表 = new List<DataMagicCard>();
                    Cinderia_Mod_Item_Legacy.Log.LogInfo("[DebugGUI] 等级过滤为 -1，清空列表");
                    return;
                }

                _候选道具列表 = 获取候选道具池(_当前道具等级过滤);
                _当前预览索引 = 0;
                Cinderia_Mod_Item_Legacy.Log.LogInfo(string.Format("[DebugGUI] 刷新等级 {0} 的道具列表，共 {1} 个", _当前道具等级过滤, _候选道具列表.Count));
            }
            catch (Exception ex)
            {
                Cinderia_Mod_Item_Legacy.Log.LogError("[DebugGUI] 刷新候选道具列表失败: " + ex);
                _候选道具列表 = new List<DataMagicCard>();
            }
        }

        private List<DataMagicCard> 获取候选道具池(int 品质等级)
        {
            // 确保复制器已注入
            Cinderia_Mod_Item_Legacy.EnsureCustomDuplicatorItems();

            Rogue.ExcelData excel = Cinderia_Mod_Item_Legacy.获取Excel数据();
            if (excel?.magicCards == null)
            {
                Cinderia_Mod_Item_Legacy.Log.LogWarning("[DebugGUI] Excel 数据为空");
                return new List<DataMagicCard>();
            }

            // 检查复制器
            var 复制器 = excel.magicCards.FirstOrDefault(c => c != null && c.id != null && c.id.Contains("复制器"));
            if (复制器 != null)
            {
                string keyward文本 = 复制器.keyward != null ? string.Join(",", 复制器.keyward) : "null";
                Cinderia_Mod_Item_Legacy.Log.LogInfo(string.Format("[DebugGUI] 找到复制器: id={0}, kind={1}, ItemLv={2}, keyward={3}",
                    复制器.id, 复制器.kind, 复制器.ItemLv, keyward文本));
            }
            else
            {
                Cinderia_Mod_Item_Legacy.Log.LogWarning("[DebugGUI] 未找到复制器道具");
            }

            // 只返回道具类型的魔卡
            var 结果 = excel.magicCards
                .Where(c => c != null && c.kind == "道具")
                .Where(c => c.ItemLv == 品质等级)
                .Where(c => !(c.keyward?.Contains("没法爆出来") ?? false))
                .OrderBy(c =>
                {
                    try
                    {
                        return Game.获取多语言_MagicCard名称(c.id);
                    }
                    catch
                    {
                        return c.id;
                    }
                })
                .ToList();

            Cinderia_Mod_Item_Legacy.Log.LogInfo(string.Format("[DebugGUI] 等级 {0} 筛选结果: {1} 个道具", 品质等级, 结果.Count));
            return 结果;
        }

        private void 绘制配置界面()
        {
            float 窗口宽 = Mathf.Min(Screen.width - 100, 1000);
            float 窗口高 = Mathf.Min(Screen.height - 100, 700);
            Rect 窗口区域 = new Rect(
                (Screen.width - 窗口宽) * 0.5f,
                (Screen.height - 窗口高) * 0.5f,
                窗口宽,
                窗口高);

            GUI.Box(窗口区域, "", _窗口样式);

            GUILayout.BeginArea(new Rect(窗口区域.x + 20, 窗口区域.y + 20, 窗口区域.width - 40, 窗口区域.height - 40));

            GUILayout.Label("Mod 配置", _标题样式);
            GUILayout.Space(20);

            _配置滚动位置 = GUILayout.BeginScrollView(_配置滚动位置);

            var 所有配置 = Cinderia_Mod_Item_Legacy.获取所有配置();
            foreach (var cfg in 所有配置)
            {
                string 配置键 = cfg.Definition.Section + "." + cfg.Definition.Key;
                配置描述 描述 = null;
                _配置描述映射?.TryGetValue(配置键, out 描述);

                GUILayout.BeginVertical();

                // 第一行：配置名称、输入框、生效时机
                GUILayout.BeginHorizontal();

                // 配置名称
                GUIStyle 名称样式 = new GUIStyle(_配置标签样式);
                名称样式.fontSize = 22;
                GUILayout.Label(描述?.名称 ?? (cfg.Definition.Section + " - " + cfg.Definition.Key), 名称样式, GUILayout.Width(320));

                GUILayout.FlexibleSpace();

                // 配置值编辑（居中）
                if (cfg.SettingType == typeof(bool))
                {
                    bool 当前值 = (bool)cfg.BoxedValue;
                    bool 新值 = GUILayout.Toggle(当前值, 当前值 ? "启用" : "禁用", _切换按钮样式, GUILayout.Width(100));
                    if (新值 != 当前值)
                    {
                        cfg.BoxedValue = 新值;
                    }
                }
                else if (cfg.SettingType == typeof(int))
                {
                    if (!_配置临时值.ContainsKey(cfg))
                    {
                        _配置临时值[cfg] = cfg.BoxedValue.ToString();
                    }
                    _配置临时值[cfg] = GUILayout.TextField(_配置临时值[cfg], _输入框样式, GUILayout.Width(150), GUILayout.Height(35));
                    if (int.TryParse(_配置临时值[cfg], out int 新值))
                    {
                        cfg.BoxedValue = 新值;
                    }
                }
                else if (cfg.SettingType == typeof(float))
                {
                    if (!_配置临时值.ContainsKey(cfg))
                    {
                        _配置临时值[cfg] = cfg.BoxedValue.ToString();
                    }
                    _配置临时值[cfg] = GUILayout.TextField(_配置临时值[cfg], _输入框样式, GUILayout.Width(150), GUILayout.Height(35));
                    if (float.TryParse(_配置临时值[cfg], out float 新值))
                    {
                        cfg.BoxedValue = 新值;
                    }
                }
                else if (cfg.SettingType == typeof(string))
                {
                    if (!_配置临时值.ContainsKey(cfg))
                    {
                        _配置临时值[cfg] = cfg.BoxedValue?.ToString() ?? "";
                    }
                    _配置临时值[cfg] = GUILayout.TextField(_配置临时值[cfg], _输入框样式, GUILayout.Width(300), GUILayout.Height(35));
                    cfg.BoxedValue = _配置临时值[cfg];
                }

                GUILayout.FlexibleSpace();

                // 生效时机
                if (描述 != null && !string.IsNullOrEmpty(描述.生效时机))
                {
                    Color 旧颜色 = GUI.color;
                    GUI.color = new Color(0.7f, 0.75f, 0.8f, 1f);
                    GUIStyle 时机样式 = new GUIStyle(_配置标签样式);
                    时机样式.fontSize = 18;
                    GUILayout.Label("[" + 描述.生效时机 + "]", 时机样式, GUILayout.Width(130));
                    GUI.color = 旧颜色;
                }

                GUILayout.EndHorizontal();

                // 第二行：配置说明
                if (描述 != null && !string.IsNullOrEmpty(描述.说明))
                {
                    GUILayout.Space(4);
                    Color 旧颜色 = GUI.color;
                    GUI.color = new Color(0.65f, 0.68f, 0.72f, 1f);
                    GUIStyle 说明样式 = new GUIStyle(_配置标签样式);
                    说明样式.fontSize = 17;
                    GUILayout.Label("  " + 描述.说明, 说明样式);
                    GUI.color = 旧颜色;
                }

                GUILayout.Space(16);
                GUILayout.EndVertical();
            }

            GUILayout.EndScrollView();

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("保存配置", _按钮样式, GUILayout.Height(35)))
            {
                Cinderia_Mod_Item_Legacy.保存配置();
                Cinderia_Mod_Item_Legacy.Log.LogInfo("[DebugGUI] 配置已保存");
            }
            if (GUILayout.Button("关闭 (F9)", _按钮样式, GUILayout.Height(35)))
            {
                _显示配置界面 = false;
            }
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private void 绘制道具选择界面()
        {
            float 窗口宽 = Mathf.Min(Screen.width - 80, 1200);
            float 窗口高 = Mathf.Min(Screen.height - 80, 760);
            Rect 窗口区域 = new Rect(
                (Screen.width - 窗口宽) * 0.5f,
                (Screen.height - 窗口高) * 0.5f,
                窗口宽,
                窗口高);

            // 半透明背景
            Color 旧颜色 = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = 旧颜色;

            GUI.Box(窗口区域, "", _窗口样式);

            // 顶部强调条
            if (_当前道具等级过滤 >= 0)
            {
                Color 等级颜色 = 获取稀有度颜色(_当前道具等级过滤);
                GUI.color = 等级颜色;
                GUI.DrawTexture(new Rect(窗口区域.x, 窗口区域.y, 窗口区域.width, 6f), Texture2D.whiteTexture);
                GUI.color = 旧颜色;
            }

            GUILayout.BeginArea(new Rect(窗口区域.x + 28, 窗口区域.y + 18, 窗口区域.width - 56, 窗口区域.height - 36));

            // 标题
            GUILayout.Label("道具选择", _标题样式);
            GUILayout.Space(20);

            // 等级过滤按钮
            GUILayout.BeginHorizontal();
            GUILayout.Label("选择等级:", _配置标签样式, GUILayout.Width(100));
            for (int i = 0; i <= 4; i++)
            {
                Color 等级颜色 = 获取稀有度颜色(i);
                string 等级名称 = 获取等级名称(i);

                GUI.color = 等级颜色;
                bool 已选中 = _当前道具等级过滤 == i;
                GUIStyle 按钮样式 = 已选中 ? _道具按钮悬停样式 : _badgeStyle;

                if (GUILayout.Button(等级名称, 按钮样式, GUILayout.Width(120), GUILayout.Height(35)))
                {
                    _当前道具等级过滤 = i;
                    刷新候选道具列表();
                }
                GUI.color = 旧颜色;
            }
            GUILayout.EndHorizontal();

            GUILayout.EndArea();

            // 左右分栏 - 使用绝对坐标，完全复刻自选开箱
            float 剩余高度 = 窗口高 - 200;
            float 左区域宽 = (窗口区域.width - 56 - 16) * 0.52f;
            float 右区域宽 = (窗口区域.width - 56 - 16) - 左区域宽 - 16;

            Rect 左区域 = new Rect(窗口区域.x + 28, 窗口区域.y + 130, 左区域宽, 剩余高度);
            Rect 右区域 = new Rect(左区域.xMax + 16, 窗口区域.y + 130, 右区域宽, 剩余高度);

            int 新预览索引 = _当前预览索引;
            绘制候选列表复刻版(左区域, _当前预览索引, out 新预览索引);
            if (新预览索引 != _当前预览索引)
            {
                _当前预览索引 = 新预览索引;
            }
            绘制道具详情复刻版(右区域, _当前预览索引);

            // 底部提示
            GUILayout.BeginArea(new Rect(窗口区域.x + 28, 窗口区域.yMax - 80, 窗口区域.width - 56, 70));

            // 底部提示
            Color 提示颜色 = GUI.color;
            GUI.color = new Color(0.68f, 0.72f, 0.78f, 1f);
            GUIStyle 提示样式 = new GUIStyle(_配置标签样式);
            提示样式.fontSize = 14;
            GUILayout.Label("点击左侧道具即可掉落，原版保持不变。", 提示样式);
            GUI.color = 提示颜色;

            GUILayout.Space(6);

            // 关闭按钮
            if (GUILayout.Button("关闭 (F10)", _按钮样式, GUILayout.Height(35)))
            {
                _显示道具选择界面 = false;
            }

            GUILayout.EndArea();
        }

        private void 绘制候选列表复刻版(Rect 区域, int 当前绘制预览索引, out int 待提交预览索引)
        {
            const int 每行数量 = 3;
            const float 卡片间距 = 8f;
            const float 卡片高度 = 104f;
            待提交预览索引 = 当前绘制预览索引;

            GUILayout.BeginArea(区域, GUIContent.none, _sectionStyle);
            GUILayout.Label("候选道具", _sectionTitleStyle);
            GUILayout.Space(10f);

            if (_候选道具列表 == null || _候选道具列表.Count == 0)
            {
                GUILayout.Label("请先选择等级", _配置标签样式);
                GUILayout.EndArea();
                return;
            }

            _道具滚动位置 = GUILayout.BeginScrollView(_道具滚动位置, false, true);

            for (int rowStart = 0; rowStart < _候选道具列表.Count; rowStart += 每行数量)
            {
                Rect 行区域 = GUILayoutUtility.GetRect(10f, 卡片高度, GUILayout.ExpandWidth(true));
                float 卡片宽度 = (行区域.width - 卡片间距 * (每行数量 - 1)) / 每行数量;

                for (int col = 0; col < 每行数量; col++)
                {
                    int i = rowStart + col;
                    if (i >= _候选道具列表.Count)
                    {
                        break;
                    }

                    DataMagicCard 道具 = _候选道具列表[i];
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

                    GUI.Box(条目区域, GUIContent.none, 当前预览 ? (_itemPreviewTexture == null ? GUIStyle.none : 创建纯纹理样式(_itemPreviewTexture)) : (_itemTexture == null ? GUIStyle.none : 创建纯纹理样式(鼠标悬停 ? _itemHoverTexture : _itemTexture)));

                    Color 旧颜色 = GUI.color;
                    GUI.color = 获取稀有度颜色(道具.ItemLv);
                    GUI.DrawTexture(new Rect(条目区域.x, 条目区域.y, 6f, 条目区域.height), _badgeTexture);
                    GUI.color = 旧颜色;

                    绘制道具图标(道具, new Rect(条目区域.x + (条目区域.width - 40f) * 0.5f, 条目区域.y + 12f, 40f, 40f));

                    string 名称 = Game.获取多语言_MagicCard名称(道具.id);
                    GUI.Label(
                        new Rect(条目区域.x + 12f, 条目区域.y + 56f, 条目区域.width - 24f, 36f),
                        名称,
                        _itemTitleStyle);

                    if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && 条目区域.Contains(Event.current.mousePosition))
                    {
                        Event.current.Use();
                        掉落道具(道具);
                    }
                }

                GUILayout.Space(8f);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void 绘制道具详情复刻版(Rect 区域, int 当前绘制预览索引)
        {
            GUILayout.BeginArea(区域, GUIContent.none, _sectionStyle);
            GUILayout.Label("道具详情", _sectionTitleStyle);
            GUILayout.Space(10f);

            if (_候选道具列表 == null || _候选道具列表.Count == 0)
            {
                GUILayout.EndArea();
                return;
            }

            _道具详情滚动位置 = GUILayout.BeginScrollView(_道具详情滚动位置, false, true);

            DataMagicCard 当前预览 = _候选道具列表[Mathf.Clamp(当前绘制预览索引, 0, _候选道具列表.Count - 1)];
            string 名称 = Game.获取多语言_MagicCard名称(当前预览.id);

            GUILayout.Label(名称, _detailTitleStyle);
            GUILayout.Space(8f);

            Color 旧颜色 = GUI.color;
            GUI.color = 获取稀有度颜色(当前预览.ItemLv);
            GUILayout.Label("稀有度  " + 获取稀有度文本(当前预览.ItemLv), _badgeStyle);
            GUI.color = 旧颜色;

            GUILayout.Space(8f);
            GUILayout.Label("描述", _sectionTitleStyle);
            GUILayout.Space(4f);

            string 描述 = Game.获取多语言_MagicCard描述(当前预览.id);
            GUILayout.Label(描述, _detailTextStyle);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private GUIStyle 创建纯纹理样式(Texture2D 纹理)
        {
            GUIStyle 样式 = new GUIStyle(GUI.skin.box);
            样式.normal.background = 纹理;
            样式.border = new RectOffset(0, 0, 0, 0);
            样式.padding = new RectOffset(0, 0, 0, 0);
            样式.margin = new RectOffset(0, 0, 0, 0);
            return 样式;
        }

        private string 获取稀有度文本(int 等级)
        {
            switch (等级)
            {
                case 0: return "白";
                case 1: return "绿";
                case 2: return "蓝";
                case 3: return "紫";
                case 4: return "橙";
                default: return "未知";
            }
        }

        private void 绘制道具详情(Rect 区域)
        {
            GUILayout.BeginArea(区域, GUIContent.none, _sectionStyle);
            GUILayout.Label("道具详情", _sectionTitleStyle);
            GUILayout.Space(10);

            if (_候选道具列表 == null || _候选道具列表.Count == 0 || _当前预览索引 < 0 || _当前预览索引 >= _候选道具列表.Count)
            {
                GUILayout.EndArea();
                return;
            }

            DataMagicCard 当前道具 = _候选道具列表[_当前预览索引];

            _道具详情滚动位置 = GUILayout.BeginScrollView(_道具详情滚动位置, false, true);

            // 名称
            string 名称 = "未知道具";
            try
            {
                名称 = Game.获取多语言_MagicCard名称(当前道具.id);
            }
            catch { }

            Color 等级颜色 = 获取稀有度颜色(当前道具.ItemLv);
            Color 旧颜色 = GUI.color;
            GUI.color = 等级颜色;
            GUILayout.Label(名称, _detailTitleStyle);
            GUI.color = 旧颜色;

            GUILayout.Space(8);

            // 稀有度标签
            GUILayout.BeginHorizontal();
            GUI.color = 等级颜色;
            GUILayout.Label(获取等级名称(当前道具.ItemLv), _badgeStyle, GUILayout.Width(120));
            GUI.color = 旧颜色;
            GUILayout.EndHorizontal();

            GUILayout.Space(12);

            // 描述
            string 描述 = "";
            try
            {
                描述 = Game.获取多语言_MagicCard描述(当前道具.id);
            }
            catch { }

            if (!string.IsNullOrEmpty(描述))
            {
                GUILayout.Label(描述, _detailTextStyle);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private string 获取等级名称(int 等级)
        {
            switch (等级)
            {
                case 0: return "稀有度  白";
                case 1: return "稀有度  绿";
                case 2: return "稀有度  蓝";
                case 3: return "稀有度  紫";
                case 4: return "稀有度  橙";
                default: return "未知";
            }
        }

        private string 获取等级名称简短(int 等级)
        {
            switch (等级)
            {
                case 0: return "白";
                case 1: return "绿";
                case 2: return "蓝";
                case 3: return "紫";
                case 4: return "橙";
                default: return "未知";
            }
        }

        private void 绘制道具图标(DataMagicCard 道具, Rect 区域)
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

        private void 掉落道具(DataMagicCard 道具)
        {
            try
            {
                if (Character.Inst == null)
                {
                    Cinderia_Mod_Item_Legacy.Log.LogWarning("[DebugGUI] 角色不存在，无法掉落道具");
                    return;
                }

                Vector3 掉落位置 = Character.Inst.transform.position + Vector3.right * 1f;
                var 道具对象 = Game.实例化预制体("道具", 掉落位置);
                if (道具对象 != null)
                {
                    var 道具组件 = 道具对象.GetComponent<道具>();
                    if (道具组件 != null)
                    {
                        道具组件.Init(道具);
                        Cinderia_Mod_Item_Legacy.Log.LogInfo("[DebugGUI] 已掉落道具: " + 道具.id);
                    }
                }
            }
            catch (Exception ex)
            {
                Cinderia_Mod_Item_Legacy.Log.LogError("[DebugGUI] 掉落道具失败: " + ex);
            }
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

        private static Color 获取稀有度颜色(int 等级)
        {
            switch (等级)
            {
                case 0: return new Color(0.82f, 0.82f, 0.82f, 1f);
                case 1: return new Color(0.4f, 0.85f, 0.4f, 1f);
                case 2: return new Color(0.4f, 0.7f, 1f, 1f);
                case 3: return new Color(0.75f, 0.4f, 0.95f, 1f);
                case 4: return new Color(1f, 0.65f, 0.2f, 1f);
                default: return new Color(0.82f, 0.82f, 0.82f, 1f);
            }
        }
    }
}
