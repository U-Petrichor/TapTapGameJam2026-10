using System;
using System.Collections.Generic;
using TapTapGameJam.Buffs;
using UnityEngine;

namespace TapTapGameJam.Items
{
    /// <summary>
    /// 一件装扮上配置的一条 Buff。
    ///
    /// 只填「key + 数值」两个字段，<b>是百分比还是固定值由 key 自己决定</b>（见 <see cref="BuffModifier.FromValue"/>），
    /// 所以配表时不会选错 op，也不会把 Tag 当成百分比。
    ///
    /// 数值语义：
    /// - 百分比类（移动速度、金币获取…）：<c>0.1</c> = +10%
    /// - 减免类（受伤、负重…）：<c>0.1</c> = 减免 10%（填正数）
    /// - 固定值类（背包档位、幸运…）：填绝对值
    /// - 能力 Tag：数值忽略
    /// </summary>
    [Serializable]
    public struct DressBuffEntry
    {
        [Tooltip("要改的数值")]
        public BuffKey Key;

        [Tooltip("百分比填 0.1 = +10%；减免填 0.1 = 减免 10%；固定值填绝对值；Tag 忽略")]
        public float Value;

        public DressBuffEntry(BuffKey key, float value)
        {
            Key = key;
            Value = value;
        }

        public override string ToString() => $"{BuffKeys.ToId(Key)}={Value:0.###}";
    }

    /// <summary>
    /// 装扮的静态数据：这件衣服/帽子/鞋提供哪些 Buff。
    ///
    /// 为什么单独抽成 ScriptableObject 而不是写在 <see cref="DressItem"/> 预制体上：
    /// 1. 同一套效果可以给多件不同外观的装扮复用（换贴图不用重配数值）；
    /// 2. 数值改动不碰预制体，不会因为改一个数字产生一堆预制体 diff；
    /// 3. 配表的人和做美术的人可以并行。
    ///
    /// 谁在读：<c>PlayerBuffs.RefreshDress()</c> 遍历三个装备槽，把这里的内容翻译成
    /// <see cref="BuffModifier"/> 并交给 <c>BuffSourceKind.Dress</c>。
    /// </summary>
    [CreateAssetMenu(menuName = "Items/Dress Data", fileName = "DressData")]
    public sealed class DressData : ScriptableObject
    {
        [Tooltip("这件装扮提供的 Buff。留空 = 纯外观")]
        [SerializeField] private List<DressBuffEntry> _buffs = new List<DressBuffEntry>();

        [Tooltip("显示给玩家的描述（可留空）")]
        [SerializeField, TextArea(2, 4)] private string _description;

        public IReadOnlyList<DressBuffEntry> Buffs => _buffs;

        public string Description => _description;

        /// <summary>编辑器 / 运行时构建用。运行时不要往里加东西。</summary>
        public List<DressBuffEntry> MutableBuffs => _buffs ??= new List<DressBuffEntry>();

        private void OnValidate()
        {
            _buffs ??= new List<DressBuffEntry>();
        }
    }
}
