using System;
using System.Collections.Generic;
using TalentTree;
using TapTapGameJam.Cockroach;
using TapTapGameJam.Items;
using TapTapGameJam.Player;
using UnityEngine;

namespace TapTapGameJam.Core
{
    [Serializable] public sealed class TalentSaveEntry { public string id; public int level; }
    [Serializable] public sealed class ItemSaveEntry
    {
        public string definitionId;
        public PickableItem.ItemLocation location;
        public Vector2Int origin;
        public int rotation;
        public int slot;
        public float remainingNutrition;
    }
    [Serializable] public sealed class NestSaveEntry
    {
        public string id;
        public Vector3 position;
        public Quaternion rotation;
        public CockNest.NestLockState state;
        public int population;
        public float spawnProgress, firstHatchProgress, hatchNutrition;
        // firstHatchProgress 现在记的是"首次孵化已经吃掉的营养量"（不再是秒）；
        // spawnProgress 仍是产卵耗时（秒），hatchNutrition 是常规孵化攒的进度。
        // firstHatchReserve 是巢穴"已从共享池预支、还没被孵化消耗掉"的那部分（旧存档没有这个字段，按 0 处理）。
        public float firstHatchReserve;
        public CockroachCommand command;
    }

    /// <summary>Only value data crosses the checkpoint boundary; never retain live object references.</summary>
    public static class CheckpointState
    {
        public static void Capture(SaveData data, PlayerResources resources)
        {
            foreach (var spawner in UnityEngine.Object.FindObjectsByType<AccessCardSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                data.hasCollectedAccessCard |= spawner.HasCollectedAccessCard;
            var tree = resources.GetComponent<PlayerTalentTree>();
            data.talents = tree != null ? tree.CaptureProgress() : new List<TalentSaveEntry>();
            data.items = new List<ItemSaveEntry>();
            var seen = new HashSet<PickableItem>();
            var bag = resources.GetComponent<PlayerBag>();
            var repo = resources.GetComponent<PlayerRepo>();
            void Add(PickableItem item, PickableItem.ItemLocation location, Vector2Int origin, int slot)
            {
                if (item == null || !seen.Add(item)) return;
                if (string.IsNullOrEmpty(item.SaveDefinitionId) || SaveItemCatalog.Load()?.Find(item.SaveDefinitionId) == null)
                    throw new InvalidOperationException($"物品 {item.name} 没有存档模板，请重建存档物品目录。");
                data.items.Add(new ItemSaveEntry {
                    definitionId = item.SaveDefinitionId, location = location, origin = origin, slot = slot,
                    rotation = item.shape?.RotationSteps ?? 0,
                    remainingNutrition = item is FoodItem food ? (food.NutritionInitialized ? food.remainingNutrition : food.totalNutrition) : 0
                });
            }
            if (bag != null)
            {
                foreach (var placement in bag.Placements)
                    Add(placement.item, PickableItem.ItemLocation.InBag, placement.origin, -1);
                for (int i = 0; i < bag.EquippedItems.Count; i++)
                    Add(bag.EquippedItems[i], PickableItem.ItemLocation.Equipped, default, i);
                // A dragged owned item may temporarily have no grid placement.
                foreach (var item in resources.GetComponentsInChildren<PickableItem>(true))
                    if (item.Location == PickableItem.ItemLocation.InBag)
                        Add(item, PickableItem.ItemLocation.InBag, new Vector2Int(-999, -999), -1);
            }
            if (repo != null)
                foreach (var item in repo.Items)
                    if (repo.TryGetCellIndex(item, out _, out int index))
                        Add(item, PickableItem.ItemLocation.InRepo, default, index);
            data.nests = new List<NestSaveEntry>();
            var manager = CockroachManager.Instance;
            data.sharedNutrition = manager != null ? manager.sharedNutrition : 0;
            foreach (var nest in UnityEngine.Object.FindObjectsByType<CockNest>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                data.nests.Add(nest.CaptureState());
            data.hasFullCheckpoint = true;
        }

        public static void Restore(SaveData data, PlayerResources resources)
        {
            // Restore before the phase event, so a collected card cannot respawn on load.
            foreach (var spawner in UnityEngine.Object.FindObjectsByType<AccessCardSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                spawner.RestoreCollectedState(data.hasCollectedAccessCard);
            if (data.version < 2) return;
            var nests = new Dictionary<string, CockNest>();
            foreach (var nest in UnityEngine.Object.FindObjectsByType<CockNest>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                nests.Add(nest.SaveId, nest);
            foreach (var entry in data.nests)
                if (!nests.ContainsKey(entry.id))
                    throw new InvalidOperationException($"存档巢穴 {entry.id} 在当前场景中不存在。");
            var bag = resources.GetComponent<PlayerBag>();
            var repo = resources.GetComponent<PlayerRepo>();
            var catalog = SaveItemCatalog.Load();
            // Validate every template before clearing anything.
            foreach (var entry in data.items)
                if (catalog == null || catalog.Find(entry.definitionId) == null)
                    throw new InvalidOperationException($"无法恢复物品模板 {entry.definitionId}。");
            resources.GetComponent<PlayerTalentTree>()?.RestoreProgress(data.talents);
            bag?.ClearForRestore();
            repo?.ClearForRestore();
            foreach (var entry in data.items)
            {
                var item = UnityEngine.Object.Instantiate(catalog.Find(entry.definitionId));
                item.gameObject.SetActive(false);
                if (item.shape != null) item.shape.RotationSteps = entry.rotation;
                if (item is FoodItem food) food.RestoreNutrition(entry.remainingNutrition);
                bool placed = false;
                if (entry.location == PickableItem.ItemLocation.Equipped && bag != null)
                    placed = bag.RestoreEquipped(item, (EquipSlot)entry.slot);
                else if (entry.location == PickableItem.ItemLocation.InRepo && repo != null)
                    placed = repo.InBounds(entry.slot) && !repo.IsCellOccupied(item.Category, entry.slot)
                        && repo.PutItem(item.Category, entry.slot, item);
                else if (entry.location == PickableItem.ItemLocation.InBag && bag != null)
                    placed = bag.TryPlace(item, entry.origin) || bag.TryPlace(item, out _);
                // Changed bag dimensions/definitions must not silently delete possessions.
                if (!placed && repo != null) placed = repo.PutItem(item, out _, out _);
                if (!placed && bag != null) placed = bag.TryPlace(item, out _);
                if (!placed)
                {
                    if (bag != null) bag.ReturnToWorld(item, resources.transform.position);
                    else { item.transform.position = resources.transform.position; item.gameObject.SetActive(true); }
                    Debug.LogWarning($"物品 {item.name} 无法放回原槽位，已放在玩家脚下。");
                }
            }
            foreach (var entry in data.nests)
            {
                if (!nests.TryGetValue(entry.id, out var nest))
                    throw new InvalidOperationException($"存档巢穴 {entry.id} 在当前场景中不存在。");
                nest.RestoreState(entry);
            }
            if (CockroachManager.Instance != null)
            {
                CockroachManager.Instance.RestoreSharedNutrition(data.sharedNutrition);
            }
        }

        public static bool Validate(SaveData data)
        {
            if (data.version == 1) return true;
            if (!data.hasFullCheckpoint || data.talents == null || data.items == null || data.nests == null || !FiniteNonnegative(data.sharedNutrition)) return false;
            var ids = new HashSet<string>();
            foreach (var talent in data.talents)
                if (talent == null || string.IsNullOrEmpty(talent.id) || talent.level < 1 || !ids.Add(talent.id)) return false;
            foreach (var item in data.items)
                if (item == null || string.IsNullOrEmpty(item.definitionId) || item.rotation < 0 || item.rotation > 3 ||
                    !FiniteNonnegative(item.remainingNutrition) ||
                    (item.location != PickableItem.ItemLocation.InBag && item.location != PickableItem.ItemLocation.InRepo && item.location != PickableItem.ItemLocation.Equipped) ||
                    (item.location == PickableItem.ItemLocation.Equipped && (item.slot < 0 || item.slot > 2)) ||
                    (item.location == PickableItem.ItemLocation.InRepo && item.slot < 0)) return false;
            ids.Clear();
            foreach (var nest in data.nests)
                if (nest == null || string.IsNullOrEmpty(nest.id) || !ids.Add(nest.id) || nest.population < 0 || nest.population > 100000 ||
                    !Enum.IsDefined(typeof(CockNest.NestLockState), nest.state) || !Enum.IsDefined(typeof(CockroachCommand), nest.command) ||
                    !FiniteNonnegative(nest.spawnProgress) || !FiniteNonnegative(nest.firstHatchProgress) || !FiniteNonnegative(nest.hatchNutrition) ||
                    !Finite(nest.position.x) || !Finite(nest.position.y) || !Finite(nest.position.z) ||
                    !Finite(nest.rotation.x) || !Finite(nest.rotation.y) || !Finite(nest.rotation.z) || !Finite(nest.rotation.w)) return false;
            return true;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool FiniteNonnegative(float value) => Finite(value) && value >= 0;
    }
}
