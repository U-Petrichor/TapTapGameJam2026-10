"""Stage actual audio sources into an isolated Unity project under Temp.
Gameplay dependencies are stubs; full game compilation is checked separately.
"""
from pathlib import Path
import json
import re
import shutil

ROOT = Path(__file__).resolve().parents[2]
TARGET = ROOT/'Temp/AudioSmokeProject'
result = TARGET/'smoke-result.txt'
if result.exists(): result.unlink()
for name in ['Assets/Audio', 'Assets/Editor', 'Assets/Resources/Audio', 'Assets/Clips', 'Packages', 'ProjectSettings']:
    (TARGET/name).mkdir(parents=True, exist_ok=True)
for file in (ROOT/'Assets/Scripts/Audio').glob('*'):
    if file.is_file(): shutil.copy2(file, TARGET/'Assets/Audio'/file.name)
for name in ['SoundLibrary.asset', 'SoundLibrary.asset.meta']:
    shutil.copy2(ROOT/'Assets/Resources/Audio'/name,TARGET/'Assets/Resources/Audio'/name)
shutil.copy2(ROOT/'Assets/Scripts/Core/EventPipeline.cs', TARGET/'Assets/EventPipeline.cs')
shutil.copy2(ROOT/'Tools/Audio/SmokeEditor.cs', TARGET/'Assets/Editor/AudioSmokeEditor.cs')
shutil.copy2(ROOT/'Tools/Audio/SmokeRunner.cs', TARGET/'Assets/AudioSmokeRunner.cs')
shutil.copy2(ROOT/'Tools/Audio/DoorRepeatRegression.cs', TARGET/'Assets/DoorRepeatRegression.cs')
for name in ['InteractableObjects/SecurityDoor.cs', 'InteractableObjects/Interactable.cs',
             'InteractableObjects/InteractKey.cs', 'MiniGame/ITaskSpawnCondition.cs']:
    shutil.copy2(ROOT/'Assets/Scripts'/name, TARGET/'Assets'/Path(name).name)
library=(ROOT/'Assets/Resources/Audio/SoundLibrary.asset').read_text(encoding='utf-8')
needed=set(re.findall(r'fileID: 8300000, guid: (\w+)',library))
for folder in [ROOT/'Assets/Model/Music/音效', ROOT/'Assets/Model/Music/V2', ROOT/'Assets/Resources/Audio/Derived']:
    for file in folder.rglob('*.meta'):
        match=re.search(r'^guid: (\w+)',file.read_text(encoding='utf-8'),re.M)
        if match and match[1] in needed:
            shutil.copy2(file, TARGET/'Assets/Clips'/file.name)
            shutil.copy2(Path(str(file)[:-5]), TARGET/'Assets/Clips'/file.name[:-5])
            needed.remove(match[1])
assert not needed, needed
(TARGET/'Assets/Stubs.cs').write_text('''using UnityEngine;
namespace TapTapGameJam.Events {
 public interface IEvent {}
 public readonly struct EventNewDay : IEvent { public int DayCount { get; } public EventNewDay(int day) => DayCount = day; }
 public readonly struct EventSecurityDoorAvailable : IEvent {
  public SecurityDoor Door { get; } public EventSecurityDoorAvailable(SecurityDoor door) => Door = door;
 }
 public readonly struct GamePhaseChangedEvent : IEvent {
  public TapTapGameJam.Types.GamePhase NewPhase { get; }
  public GamePhaseChangedEvent(TapTapGameJam.Types.GamePhase phase) => NewPhase = phase;
 }
}
namespace TapTapGameJam.Types { public enum GamePhase { Day, Shopping, Night } }
namespace TapTapGameJam.Player {
 public class PlayerBasic : MonoBehaviour { public bool IsHuman = true; }
 public class PlayerInteractionManager : MonoBehaviour {
  public void Register(TapTapGameJam.Interactables.Interactable item) {}
  public void Unregister(TapTapGameJam.Interactables.Interactable item) {}
 }
}
namespace TapTapGameJam.NPC { public class NPC : MonoBehaviour {} }
namespace TapTapGameJam.Cockroach { public class Guard : MonoBehaviour {} }
''',encoding='utf-8')
original=json.loads((ROOT/'Packages/manifest.json').read_text(encoding='utf-8'))['dependencies']
deps={k:v for k,v in original.items() if k.startswith('com.unity.modules.')}
deps['com.unity.ugui']='file:'+str(next((ROOT/'Library/PackageCache').glob('com.unity.ugui@*'))).replace('\\','/')
(TARGET/'Packages/manifest.json').write_text(json.dumps({'dependencies':deps},indent=2),encoding='utf-8')
shutil.copy2(ROOT/'ProjectSettings/ProjectVersion.txt',TARGET/'ProjectSettings/ProjectVersion.txt')
print(TARGET)
