using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TalentTree
{
    /// <summary>A textured stroke, which can be shared by several talent connections.</summary>
    public sealed class TalentConnectionView : MonoBehaviour
    {
        [SerializeField] private Graphic _graphic;
        [SerializeField] private List<TalentGraphEdgeData> _connections = new();
        [SerializeField] private Color _lockedColor = new(0.3f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color _unlockedColor = new(1f, 0.82f, 0.12f, 1f);

        internal void Refresh(PlayerTalentTree progress, TalentTreeAsset asset)
        {
            if (_graphic == null) _graphic = GetComponent<Graphic>();
            if (_graphic == null) return;

            bool active = false;
            if (progress != null && asset != null && _connections != null)
            {
                foreach (TalentGraphEdgeData connection in _connections)
                {
                    if (connection == null || !IsUnlocked(connection.First, progress, asset) ||
                        !IsUnlocked(connection.Second, progress, asset)) continue;
                    active = true;
                    break;
                }
            }

            _graphic.color = active ? _unlockedColor : _lockedColor;
            _graphic.raycastTarget = false;
        }

        // The graph evaluator treats roots as available without requiring a stored level.
        private static bool IsUnlocked(TalentData talent, PlayerTalentTree progress, TalentTreeAsset asset)
            => talent != null && (progress.GetLevel(talent) > 0 || asset.FindNode(talent)?.IsRoot == true);
    }
}
