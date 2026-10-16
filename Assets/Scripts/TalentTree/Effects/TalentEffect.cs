using System;
using UnityEngine;

namespace TalentTree.Effects
{
    /// <summary>
    /// 天赋效果的数据基类。具体玩法可以继承它，使用 SerializeReference 保存自定义参数。
    /// NumericValue 用于简单数值型效果，Payload 为复杂效果预留参数入口。
    /// </summary>
    [Serializable]
    public class TalentEffect
    {
        [SerializeField] private string _effectKey;
        [SerializeField] private float _numericValue;
        [SerializeField, TextArea] private string _payload;

        public string EffectKey => _effectKey;
        public float NumericValue => _numericValue;
        public string Payload => _payload;

        public void SetData(string effectKey, float numericValue, string payload)
        {
            _effectKey = effectKey;
            _numericValue = numericValue;
            _payload = payload;
        }
    }
}
