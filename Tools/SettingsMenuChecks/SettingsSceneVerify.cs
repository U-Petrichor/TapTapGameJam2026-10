using System;using System.IO;using System.Linq;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using TapTapGameJam.UI;
public static class SettingsSceneVerify {public static void Run(){
var scene=EditorSceneManager.OpenScene("Assets/Integration/MainMenu.unity",OpenSceneMode.Single);
var menu=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MainMenuUIInteract>(true)).Single();
if(menu.settingsCanvas==null||menu.settingsBackBtn==null)throw new Exception("Broken main menu settings references");
var settings=menu.settingsCanvas.GetComponent<SettingsMenu>();
if(settings==null||menu.settingsBackBtn!=settings.BackButton)throw new Exception("Back button points to another settings instance");
if(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(settings.gameObject)!=SettingsArtworkBuilder.PrefabPath)throw new Exception("Settings is not the shared prefab");
File.WriteAllText("settings-scene-result.txt","PASS: production MainMenu scene imports, Settings prefab resolves, main menu and back button reference the same instance");
EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
}}