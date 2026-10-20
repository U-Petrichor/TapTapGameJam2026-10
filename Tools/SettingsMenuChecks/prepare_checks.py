"""Prepare isolated Unity checks without opening or saving the user's editor.
Usage: python Tools/SettingsMenuChecks/prepare_checks.py [scratch-project-path]
Then run Unity -batchmode -projectPath <scratch> -executeMethod SettingsVerify.Run -quit
and Unity -batchmode -nographics -projectPath <scratch> -executeMethod SettingsSceneVerify.Run -quit.
SettingsVerify renders PNGs and uses an audio stub; compile production sources separately.
"""
from pathlib import Path
import json
import shutil
import sys

ROOT = Path(__file__).resolve().parents[2]
TARGET = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else ROOT.parent / '.codex-checks' / 'SettingsArtwork'
if TARGET == ROOT or ROOT in TARGET.parents:
    raise ValueError('Use a scratch project outside the production project.')
for name in ['Assets/Editor', 'Assets/Integration', 'Packages', 'ProjectSettings']:
    (TARGET / name).mkdir(parents=True, exist_ok=True)
for relative in ['Assets/Model/UI/菜单组件/系统设置', 'Assets/TextMesh Pro/Fonts',
                 'Assets/TextMesh Pro/Shaders', 'Assets/TextMesh Pro/Resources', 'Assets/Resources/UI']:
    shutil.copytree(ROOT / relative, TARGET / relative, dirs_exist_ok=True)
font = 'Assets/TextMesh Pro/Examples & Extras/Fonts/fusion-pixel-12px-monospaced-zh_hans 2 SDF.asset'
(TARGET / font).parent.mkdir(parents=True, exist_ok=True)
for relative in [font, font + '.meta']:
    shutil.copy2(ROOT / relative, TARGET / relative)
for relative in ['Assets/Scripts/UI/SettingsMenu.cs', 'Assets/Scripts/UI/SettingsGuideArrow.cs',
                 'Assets/Editor/SettingsArtworkBuilder.cs']:
    destination = TARGET / relative
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(ROOT / relative, destination)
    shutil.copy2(ROOT / (relative + '.meta'), TARGET / (relative + '.meta'))
for source, destination in [('SettingsVerify.cs', 'Assets/Editor/SettingsVerify.cs'),
                            ('SettingsSceneVerify.cs', 'Assets/Editor/SettingsSceneVerify.cs'),
                            ('SettingsReferenceProbe.cs', 'Assets/SettingsReferenceProbe.cs'),
                            ('MainMenuReferenceStub.cs', 'Assets/MainMenu.cs'),
                            ('AudioStub.cs', 'Assets/AudioStub.cs')]:
    shutil.copy2(Path(__file__).parent / source, TARGET / destination)
shutil.copy2(ROOT / 'Assets/Scripts/UI/MainMenu.cs.meta', TARGET / 'Assets/MainMenu.cs.meta')
shutil.copy2(ROOT / 'Assets/Scenes/MainMenu.unity', TARGET / 'Assets/Integration/MainMenu.unity')
shutil.copy2(ROOT / 'ProjectSettings/ProjectVersion.txt', TARGET / 'ProjectSettings/ProjectVersion.txt')
(TARGET / 'Packages/manifest.json').write_text(json.dumps({'dependencies': {
    'com.unity.ugui': '2.0.0', 'com.unity.modules.ui': '1.0.0', 'com.unity.modules.imgui': '1.0.0'
}}), encoding='utf-8')
print(TARGET)
