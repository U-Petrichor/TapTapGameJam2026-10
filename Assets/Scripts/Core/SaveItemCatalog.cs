using System;
using System.Collections.Generic;
using TapTapGameJam.Items;
using UnityEngine;

namespace TapTapGameJam.Core
{
    /// <summary>Built in the editor; direct prefab references also work in standalone builds.</summary>
    public sealed class SaveItemCatalog : ScriptableObject
    {
        [Serializable] public sealed class Entry { public string id; public PickableItem prefab; }
        public List<Entry> entries = new List<Entry>();
        public static SaveItemCatalog Load() => Resources.Load<SaveItemCatalog>("SaveItemCatalog");
        public PickableItem Find(string id)
        {
            foreach (var entry in entries) if (entry.id == id) return entry.prefab;
            return null;
        }
    }
}
