"""Stage production door/card/phase scripts in a graphics-free Unity test project.
Only unrelated player/NPC/audio dependencies are replaced by test doubles.
Run Unity with -batchmode -nographics -executeMethod DoorChecksEditor.Run.
"""
from pathlib import Path
import json
import shutil

ROOT = Path(__file__).resolve().parents[2]
TARGET = ROOT.parent / '.codex-checks' / 'AccessDoorProject'
(TARGET / 'Assets/Editor').mkdir(parents=True, exist_ok=True)
(TARGET / 'Packages').mkdir(exist_ok=True)
(TARGET / 'ProjectSettings').mkdir(exist_ok=True)
result = TARGET / 'result.txt'
if result.exists():
    result.unlink()
for name in ['InteractableObjects/SecurityDoor.cs', 'InteractableObjects/AccessCard.cs',
             'InteractableObjects/AccessCardSpawner.cs', 'InteractableObjects/Interactable.cs',
             'InteractableObjects/InteractKey.cs', 'Core/EventPipeline.cs',
             'Core/GamePhaseManager.cs', 'MiniGame/ITaskSpawnCondition.cs']:
    shutil.copy2(ROOT / 'Assets/Scripts' / name, TARGET / 'Assets' / Path(name).name)
shutil.copy2(ROOT / 'Tools/Audio/DoorRepeatRegression.cs', TARGET / 'Assets/DoorRepeatRegression.cs')
for name in ['AccessDoorChecks.cs', 'Stubs.cs']:
    shutil.copy2(Path(__file__).parent / 'Headless' / name, TARGET / 'Assets' / name)
shutil.copy2(Path(__file__).parent / 'Headless/DoorChecksEditor.cs', TARGET / 'Assets/Editor/DoorChecksEditor.cs')
shutil.copy2(ROOT / 'ProjectSettings/ProjectVersion.txt', TARGET / 'ProjectSettings/ProjectVersion.txt')
(TARGET / 'Packages/manifest.json').write_text(json.dumps({'dependencies': {
    'com.unity.modules.physics': '1.0.0', 'com.unity.modules.animation': '1.0.0'
}}), encoding='utf-8')
print(TARGET)
