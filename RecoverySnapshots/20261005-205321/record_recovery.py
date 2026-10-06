from pathlib import Path
import json,re,hashlib
root=Path.cwd();out=root/'RecoverySnapshots/20261005-205321'
source=root/'Assets/Scenes/GameScene.unity'
candidate=out/'GameScene_ResourcesUI_IconsRemoved.unity'
s=source.read_text(encoding='utf-8-sig');c=candidate.read_text(encoding='utf-8-sig')
header=lambda t: set(re.findall(r'^--- !u!\d+ &(-?\d+)',t,re.M))
removed=header(s)-header(c)
assert len(removed)==12
assert header(c)-header(s)==set()
assert c.count('  m_Name: Icon\n')==s.count('  m_Name: Icon\n')-3
assert '  m_Name: NewInteractionSelectorUI\n' in c
record={
 'source_scene':str(source),
 'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),
 'reconstructed_copy':str(candidate),
 'reconstructed_sha256':hashlib.sha256(candidate.read_bytes()).hexdigest(),
 'exact_pre_reload_scene_found':False,
 'known_change_reconstructed':['HUDCanvas/ResourcesUI/Coins/Icon deleted','HUDCanvas/ResourcesUI/Nutrition/Icon deleted','HUDCanvas/ResourcesUI/CockroachNum/Icon deleted'],
 'other_ui_changes_reconstructed':False,
 'current_scene_overwritten':False,
 'evidence':['Temp/__Backupscenes is absent','Assets/_Recovery/0.unity timestamp 2026-10-05 03:28:26 +08:00; still includes all three Icons','Parent scene-recovery-20261005-0332 directory contains only morning copies','Two unreachable Git blobs are C# sources, not scenes','Windows shadow copy query returned no entries'],
 'validation':{'removed_serialized_blocks':len(removed),'new_interaction_ui_preserved':True,'remaining_references_to_deleted_ids':False}
}
(out/'recovery_manifest.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8')
(out/'README.md').write_text('''# Scene recovery inspection\n\nThe exact scene state immediately before the external-change reload has not been found.\n\nGameScene_ResourcesUI_IconsRemoved.unity is a reconstruction based on the currently saved scene. It removes the three Icon GameObjects under HUDCanvas/ResourcesUI/Coins, Nutrition and CockroachNum, matching the user-confirmed pre-reload edit. It is not a complete historical backup. Other unreported UI changes have not been reconstructed.\n\nThe active Assets/Scenes/GameScene.unity was not overwritten during recovery. All candidate snapshots and logs are preserved here.\n\nThe morning recovery scene still contains the three Icons, so replacing the active scene with it would not restore the requested UI state.\n''',encoding='utf-8')
print(json.dumps({'status':'Known UI deletion reconstructed in isolated copy','exactPreReloadSceneFound':False,'currentSceneOverwritten':False,'removedBlocks':len(removed)},ensure_ascii=False))