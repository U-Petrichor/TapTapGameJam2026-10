using System;
using UnityEngine;

namespace TalentTree
{
    /// <summary>有向连接：First 是前置天赋，Second 是后续天赋。字段名保留以兼容资源引用。</summary>
    [Serializable]
    public sealed class TalentGraphEdgeData
    {
        [SerializeField] private TalentData _first;
        [SerializeField] private TalentData _second;

        public TalentData First => _first;
        public TalentData Second => _second;

        public TalentGraphEdgeData() { }

        public TalentGraphEdgeData(TalentData first, TalentData second)
        {
            _first = first;
            _second = second;
        }

        public bool Connects(TalentData talent)
            => _first == talent || _second == talent;

        public TalentData Other(TalentData talent)
            => _first == talent ? _second : _second == talent ? _first : null;
    }
}
