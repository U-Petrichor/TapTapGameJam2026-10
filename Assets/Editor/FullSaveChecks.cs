using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TalentTree;
using TapTapGameJam.Core;
using TapTapGameJam.Cockroach;
using TapTapGameJam.Items;
using TapTapGameJam.Player;
using TapTapGameJam.Types;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Real scene reload checks, isolated from persistentDataPath. Results live in Temp/FullSaveChecks.txt.</summary>
[InitializeOnLoad]
public static class FullSaveChecks
{
    private const string RunningKey = "FullSaveChecks.Running";
    private const string RequestPath = "Temp/RunFullSaveChecks";
    private const string ResultPath = "Temp/FullSaveChecks.txt";
    private static IEnumerator _routine;
    private static readonly List<string> Results = new List<string>();
    private static double _deadline;

    static FullSaveChecks()
    {
        if (SessionState.GetBool(RunningKey, false))
            SaveManager.TestSaveDirectory = Path.GetFullPath("Temp/FullSaveTestData");
        EditorApplication.update += Update;
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(RunningKey, false))
            {
                _routine = Run();
                _deadline = EditorApplication.timeSinceStartup + 180;
            }
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(RunningKey, false))
            {
                SessionState.SetBool(RunningKey, false);
                SaveManager.TestSaveDirectory = null;
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString("FullSaveChecks.PreviousStart", ""));
            }
        };
    }

    [MenuItem("Tools/Save/Run Full Save Checks")]
    public static void Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("先退出运行模式再进行存档检查。");
        SaveItemCatalogBuilder.Rebuild();
        SessionState.SetString("FullSaveChecks.PreviousStart", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(RunningKey, true);
        SaveManager.TestSaveDirectory = Path.GetFullPath("Temp/FullSaveTestData");
        Directory.CreateDirectory(SaveManager.TestSaveDirectory);
        foreach (string suffix in new[] { "", ".bak", ".tmp" }) File.Delete(SaveManager.SavePath + suffix);
        File.WriteAllText(ResultPath, "RUNNING\n");
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/GameScene.unity");
        EditorApplication.isPlaying = true;
    }

    private static void Update()
    {
        if (File.Exists(RequestPath) && !EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode)
        { File.Delete(RequestPath); Begin(); return; }
        if (_routine == null) return;
        try
        {
            if (EditorApplication.timeSinceStartup > _deadline) throw new TimeoutException("Full save checks timed out.");
            if (_routine.MoveNext()) return;
            Finish(null);
        }
        catch (Exception exception) { Finish(exception); }
    }

    private static void Finish(Exception exception)
    {
        _routine = null;
        Results.Add(exception == null ? "ALL CHECKS PASSED" : "FAILED: " + exception);
        File.WriteAllLines(ResultPath, Results);
        if (exception != null) Debug.LogException(exception);
        else Debug.Log("[SaveChecks] ALL CHECKS PASSED");
        Time.timeScale = 1;
        EditorApplication.isPlaying = false;
    }

    private static void Check(bool value, string description)
    {
        if (!value) throw new Exception(description);
        Results.Add("PASS: " + description);
        File.WriteAllLines(ResultPath, Results);
    }

    private static string Normalize(SaveData data)
    {
        data.talents.Sort((a,b) => string.CompareOrdinal(a.id,b.id));
        data.nests.Sort((a,b) => string.CompareOrdinal(a.id,b.id));
        data.items = data.items.OrderBy(e => e.definitionId).ThenBy(e => e.location).ThenBy(e => e.slot).ThenBy(e => e.origin.x).ThenBy(e => e.origin.y).ToList();
        return JsonUtility.ToJson(data);
    }

    private static IEnumerator Run()
    {
        Results.Clear();
        while (UnityEngine.Object.FindFirstObjectByType<GamePhaseManager>() == null ||
            !UnityEngine.Object.FindFirstObjectByType<GamePhaseManager>().IsInitialized) yield return null;
        Time.timeScale = 0;
        var catalog = SaveItemCatalog.Load();
        Check(catalog != null && catalog.entries.Count > 0, "Catalog included in runtime Resources");
        Check(catalog.entries.All(e => e.prefab != null && e.id == e.prefab.SaveDefinitionId), "All prefab identities resolve");
        var phases = UnityEngine.Object.FindFirstObjectByType<GamePhaseManager>();
        var resources = GameManager.Player.PlayerResources;
        var bag = resources.GetComponent<PlayerBag>();
        var repo = resources.GetComponent<PlayerRepo>();
        var tree = resources.GetComponent<PlayerTalentTree>();
        Check(phases.DayIndex == 1 && bag != null && repo != null && tree != null, "GameScene initializes save participants");
        var talent = tree.Asset.Nodes.First(n => n.Data != null && n.Data.MaxLevel > 0).Data;
        tree.RestoreProgress(new List<TalentSaveEntry> { new TalentSaveEntry { id = talent.Id, level = talent.MaxLevel } });
        resources.RestoreResources(1234, 321, 42);
        bag.ClearForRestore(); repo.ClearForRestore();
        foreach (var category in new[] { ItemCategory.Food, ItemCategory.Tool, ItemCategory.Dress })
        {
            var template = catalog.entries.First(e => e.prefab.Category == category).prefab;
            var item = UnityEngine.Object.Instantiate(template);
            if (item.shape != null) item.shape.RotationSteps = 1;
            if (item is FoodItem food) food.RestoreNutrition(food.totalNutrition * 0.35f);
            Check(bag.TryPlace(item, out _), "Place " + category + " in bag");
            item = UnityEngine.Object.Instantiate(template);
            if (item is FoodItem storedFood) storedFood.RestoreNutrition(storedFood.totalNutrition * 0.65f);
            Check(repo.PutItem(category, 3, item), "Place " + category + " in repository slot 3");
        }
        foreach (EquipSlot slot in Enum.GetValues(typeof(EquipSlot)))
        {
            var dress = catalog.entries.First(e => e.prefab is DressItem item && item.ToSlot() == slot).prefab;
            var equipment = UnityEngine.Object.Instantiate(dress);
            Check(bag.RestoreEquipped(equipment, slot), "Restore equipment slot " + slot);
        }
        var nests = UnityEngine.Object.FindObjectsByType<CockNest>(FindObjectsSortMode.None);
        Check(nests.Length > 0, "Scene nests present");
        var first = nests[0];
        var nestState = first.CaptureState();
        nestState.position += new Vector3(0.2f, 0, 0.2f);
        nestState.rotation = Quaternion.Euler(0, 37, 0);
        nestState.state = CockNest.NestLockState.Unlocked;
        nestState.population = 5;
        nestState.hatchNutrition = 0.25f;
        first.RestoreState(nestState);
        if (nests.Length > 1)
        {
            nestState = nests[1].CaptureState();
            nestState.state = CockNest.NestLockState.FirstHatching;
            nestState.firstHatchProgress = 7.5f;
            nests[1].RestoreState(nestState);
        }
        CockroachManager.Instance.sharedNutrition = 456.75f;
        for (int i = 0; i < 3; i++) yield return null;
        Check(first.InsideCount == 5 && first.CockroachList.Count == 5, "Nest restores real population exactly");
        foreach (var phase in new[] { GamePhase.Day, GamePhase.Shopping, GamePhase.Night })
        {
            phases = UnityEngine.Object.FindFirstObjectByType<GamePhaseManager>();
            resources = GameManager.Player.PlayerResources;
            if (phase != phases.CurrentPhase) phases.EnterPhase(phase);
            Check(SaveManager.SaveCheckpoint(phases, resources), "Capture complete " + phase + " checkpoint");
            Check(SaveManager.TryRead(out var expected), "Read " + phase + " JSON");
            string expectedJson = Normalize(expected);
            var expectedBuffs = GameManager.Player.Buffs.CockroachSnapshot;
            for (int repeat = 0; repeat < 2; repeat++)
            {
                resources.RestoreResources(0, 0, 0);
                resources.GetComponent<PlayerTalentTree>().RestoreProgress(new List<TalentSaveEntry>());
                resources.GetComponent<PlayerBag>().ClearForRestore();
                resources.GetComponent<PlayerRepo>().ClearForRestore();
                Check(SaveManager.SaveLatestCheckpoint(), "Exit rewrites captured snapshot, not mutated state");
                Check(SaveManager.PrepareContinue(), "Prepare continue " + phase);
                var load = SceneManager.LoadSceneAsync("GameScene");
                while (!load.isDone) yield return null;
                while (UnityEngine.Object.FindFirstObjectByType<GamePhaseManager>() == null ||
                    !UnityEngine.Object.FindFirstObjectByType<GamePhaseManager>().IsInitialized) yield return null;
                Time.timeScale = 0;
                phases = UnityEngine.Object.FindFirstObjectByType<GamePhaseManager>();
                resources = GameManager.Player.PlayerResources;
                Check(SaveManager.SaveCheckpoint(phases, resources), "Recapture restored scene");
                Check(SaveManager.TryRead(out var actual) && Normalize(actual) == expectedJson,
                    phase + " reload " + (repeat + 1) + ": resources, talents, slots, rotations, food, nests and day match");
                Check(resources.GetComponent<PlayerTalentTree>().GetLevel(talent.Id) == talent.MaxLevel, "Talent levels restored without spending points");
                Check(GameManager.Player.Buffs.CockroachSnapshot.Equals(expectedBuffs), "Talent/equipment buff effects rebuilt");
            }
        }
        // Card history belongs to the same stage snapshot as the rest of the world.
        phases.DayIndex = 2;
        phases.EnterPhase(GamePhase.Night);
        var spawner = UnityEngine.Object.FindFirstObjectByType<AccessCardSpawner>();
        var cardDoor = UnityEngine.Object.FindObjectsByType<SecurityDoor>(FindObjectsSortMode.None).First(d => d.RequiresAccessCard);
        var card = spawner.GetComponentInChildren<AccessCard>();
        Check(card != null && !spawner.HasCollectedAccessCard && !cardDoor.IsAvailable, "Second night offers an uncollected card and keeps card door locked");
        card.Interact(GameManager.Player, TapTapGameJam.Interactables.InteractKey.E);
        Check(spawner.HasCollectedAccessCard && cardDoor.IsAvailable, "Real card pickup records history and grants door access");
        Check(SaveManager.SaveLatestCheckpoint() && SaveManager.PrepareContinue(), "Mid-stage pickup rolls back with its checkpoint");
        var cardLoad = SceneManager.LoadSceneAsync("GameScene");
        while (!cardLoad.isDone) yield return null;
        while (UnityEngine.Object.FindFirstObjectByType<GamePhaseManager>() == null ||
            !UnityEngine.Object.FindFirstObjectByType<GamePhaseManager>().IsInitialized) yield return null;
        phases = UnityEngine.Object.FindFirstObjectByType<GamePhaseManager>();
        resources = GameManager.Player.PlayerResources;
        spawner = UnityEngine.Object.FindFirstObjectByType<AccessCardSpawner>();
        cardDoor = UnityEngine.Object.FindObjectsByType<SecurityDoor>(FindObjectsSortMode.None).First(d => d.RequiresAccessCard);
        card = spawner.GetComponentInChildren<AccessCard>();
        Check(!spawner.HasCollectedAccessCard && !cardDoor.IsAvailable && card != null, "Uncollected checkpoint respawns the card after reload");
        card.Interact(GameManager.Player, TapTapGameJam.Interactables.InteractKey.E);
        phases.EnterPhase(GamePhase.Day);
        Check(SaveManager.TryRead(out var cardSave) && cardSave.hasCollectedAccessCard, "Next phase commits card history to disk");
        Check(SaveManager.PrepareContinue(), "Prepare collected card continue");
        cardLoad = SceneManager.LoadSceneAsync("GameScene");
        while (!cardLoad.isDone) yield return null;
        while (UnityEngine.Object.FindFirstObjectByType<GamePhaseManager>() == null ||
            !UnityEngine.Object.FindFirstObjectByType<GamePhaseManager>().IsInitialized) yield return null;
        phases = UnityEngine.Object.FindFirstObjectByType<GamePhaseManager>();
        spawner = UnityEngine.Object.FindFirstObjectByType<AccessCardSpawner>();
        cardDoor = UnityEngine.Object.FindObjectsByType<SecurityDoor>(FindObjectsSortMode.None).First(d => d.RequiresAccessCard);
        Check(spawner.HasCollectedAccessCard && cardDoor.IsAvailable, "Collected history restores card door permission after scene reload");
        phases.DayIndex = 4;
        phases.EnterPhase(GamePhase.Night);
        Check(spawner.GetComponentInChildren<AccessCard>() == null && cardDoor.IsAvailable, "Collected card never respawns on fourth night");
        Check(SaveManager.TryRead(out var valid), "Read checkpoint for validation checks");
        var invalid = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(valid));
        invalid.nests[0].population = -1;
        Check(!CheckpointState.Validate(invalid), "Negative nest population rejected");
        invalid = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(valid));
        invalid.nests.Add(invalid.nests[0]);
        Check(!CheckpointState.Validate(invalid), "Duplicate nest identities rejected");
        invalid = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(valid));
        invalid.items[0].location = PickableItem.ItemLocation.InStore;
        Check(!CheckpointState.Validate(invalid), "Non-owned item location rejected");
        File.WriteAllText(SaveManager.SavePath, "{broken json");
        Check(SaveManager.TryRead(out _), "Corrupt main file falls back to backup");
        File.WriteAllText(SaveManager.SavePath + ".bak", "{broken json");
        Check(!SaveManager.TryRead(out _), "Both corrupt files rejected");
        File.WriteAllText(SaveManager.SavePath, "{\"version\":1,\"dayIndex\":3,\"phase\":0,\"coins\":10,\"nutrition\":20,\"talentPoint\":30}");
        Check(SaveManager.TryRead(out var legacy) && legacy.version == 1 && legacy.dayIndex == 3, "Version 1 resource saves remain readable");
        legacy.version = 2;
        Check(!CheckpointState.Validate(legacy), "Incomplete version 2 checkpoint rejected");
        Check(SaveManager.PrepareContinue(), "Prepare legacy continue");
        var legacyLoad = SceneManager.LoadSceneAsync("GameScene");
        while (!legacyLoad.isDone) yield return null;
        while (UnityEngine.Object.FindFirstObjectByType<GamePhaseManager>() == null ||
            !UnityEngine.Object.FindFirstObjectByType<GamePhaseManager>().IsInitialized) yield return null;
        phases = UnityEngine.Object.FindFirstObjectByType<GamePhaseManager>();
        resources = GameManager.Player.PlayerResources;
        Check(phases.DayIndex == 3 && resources.Coins == 10 && resources.Nutrition == 20 && resources.TalentPoint == 30,
            "Legacy scene restore keeps day and resources");
        spawner = UnityEngine.Object.FindFirstObjectByType<AccessCardSpawner>();
        cardDoor = UnityEngine.Object.FindObjectsByType<SecurityDoor>(FindObjectsSortMode.None).First(d => d.RequiresAccessCard);
        Check(!spawner.HasCollectedAccessCard && !cardDoor.IsAvailable, "Legacy saves without card history default to uncollected");
        Check(SaveManager.SaveCheckpoint(phases, resources) && SaveManager.TryRead(out var upgraded) && upgraded.version == 2,
            "Legacy checkpoint upgrades on next capture");
    }
}
