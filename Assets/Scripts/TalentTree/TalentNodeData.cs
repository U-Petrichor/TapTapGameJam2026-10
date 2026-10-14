using System;
using UnityEngine;

namespace TalentTree
{
    [Serializable]
    public sealed class TalentNodeData
    {
        [SerializeField] private TalentData _data;
        [SerializeField] private Vector2 _position = new(100f, 100f);
        [SerializeField] private bool _isRoot;
        [SerializeField] private TalentUnlockRule _unlockRule = TalentUnlockRule.AnyPathFromRoot;

        public TalentData Data => _data;
        public Vector2 Position { get => _position; set => _position = value; }
        public bool IsRoot { get => _isRoot; set => _isRoot = value; }
        public TalentUnlockRule UnlockRule { get => _unlockRule; set => _unlockRule = value; }

        public void SetData(TalentData data) => _data = data;
    }
}
