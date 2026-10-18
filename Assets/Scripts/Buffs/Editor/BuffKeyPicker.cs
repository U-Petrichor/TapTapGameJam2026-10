using System;
using System.Collections.Generic;
using UnityEditor;

namespace TapTapGameJam.Buffs.EditorTools
{
    /// <summary>
    /// 编辑器里选 BuffKey 的统一入口。天赋树窗口和装扮数据都走这里，
    /// 保证「同一个 key 在两个地方显示成同一句话、同一个单位提示」。
    ///
    /// 为什么不各自手打 key：手打是「拼错 → 静默不生效」的最大来源。
    /// 选项上直接标出「百分比 / 减免 / 固定值 / Tag」，配数值的人不用再猜单位。
    /// </summary>
    public static class BuffKeyPicker
    {
        private static BuffKey[] _keys;
        private static string[] _ids;
        private static string[] _labels;

        public static IReadOnlyList<string> Ids
        {
            get
            {
                Ensure();
                return _ids;
            }
        }

        public static IReadOnlyList<string> Labels
        {
            get
            {
                Ensure();
                return _labels;
            }
        }

        private static void Ensure()
        {
            if (_keys != null)
            {
                return;
            }

            var keys = new List<BuffKey>();
            var ids = new List<string>();
            var labels = new List<string>();

            foreach (BuffKey key in Enum.GetValues(typeof(BuffKey)))
            {
                BuffKeyDefinition definition = BuffKeys.Get(key);
                keys.Add(key);
                ids.Add(definition.Id);
                labels.Add($"{definition.DisplayName}（{definition.Id}）· {KindLabel(definition.Kind)}");
            }

            _keys = keys.ToArray();
            _ids = ids.ToArray();
            _labels = labels.ToArray();
        }

        /// <summary>
        /// 天赋用：资产里存的是字符串 id（历史格式，不能改）。
        /// 资产里已有、但代码里没有的 key 会显示成「（未知 key：xxx）」并原样保留，不会被静默清空。
        /// </summary>
        public static string DrawIdField(string label, string currentId)
        {
            Ensure();

            var options = new List<string> { "（未设置）" };
            options.AddRange(_labels);

            int known = Array.IndexOf(_ids, currentId);
            int selected;
            if (string.IsNullOrEmpty(currentId))
            {
                selected = 0;
            }
            else if (known >= 0)
            {
                selected = known + 1;
            }
            else
            {
                options.Add($"（未知 key：{currentId}）");
                selected = options.Count - 1;
            }

            int picked = EditorGUILayout.Popup(label, selected, options.ToArray());
            if (picked == selected)
            {
                return currentId;
            }

            if (picked == 0)
            {
                return string.Empty;
            }

            return picked <= _ids.Length ? _ids[picked - 1] : currentId;
        }

        /// <summary>装扮用：直接存枚举，不需要字符串中转。</summary>
        public static BuffKey DrawKeyField(string label, BuffKey current)
        {
            Ensure();

            int selected = Array.IndexOf(_keys, current);
            if (selected < 0)
            {
                selected = 0;
            }

            int picked = EditorGUILayout.Popup(label, selected, _labels);
            return _keys[picked];
        }

        public static string KindLabel(BuffKind kind)
        {
            switch (kind)
            {
                case BuffKind.Grow: return "百分比";
                case BuffKind.Shrink: return "减免百分比";
                case BuffKind.Flat: return "固定值";
                case BuffKind.Flag: return "能力 Tag";
                default: return kind.ToString();
            }
        }

        /// <summary>数值输入框的标题，直接告诉填的人该填什么单位。</summary>
        public static string UnitLabel(BuffKind kind)
        {
            switch (kind)
            {
                case BuffKind.Shrink: return "数值（0.1 = 减免 10%）";
                case BuffKind.Flat: return "数值（固定值，不是百分比）";
                default: return "数值（0.1 = +10%）";
            }
        }
    }
}
