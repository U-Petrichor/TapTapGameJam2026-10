"""Create the initial sound library once. Does not overwrite Inspector edits.
Run from the Unity project directory; ffmpeg must be on PATH.
"""
from pathlib import Path
import re
import subprocess
import uuid

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'Assets/Model/Music/音效'
TARGET = ROOT / 'Assets/Resources/Audio'

def meta(path, folder=False):
    target = Path(str(path) + '.meta')
    if not target.exists():
        guid = uuid.uuid4().hex
        text = f'fileFormatVersion: 2\nguid: {guid}\n'
        if folder:
            text += 'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
        elif path.suffix == '.cs':
            text += 'MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
        elif path.suffix == '.wav':
            text += ('AudioImporter:\n  externalObjects: {}\n  serializedVersion: 7\n  defaultSettings:\n'
                     '    serializedVersion: 2\n    loadType: 0\n    sampleRateSetting: 0\n    sampleRateOverride: 44100\n'
                     '    compressionFormat: 1\n    quality: 1\n    conversionMode: 0\n    preloadAudioData: 1\n'
                     '  platformSettingOverrides: {}\n  forceToMono: 1\n  normalize: 1\n  loadInBackground: 0\n'
                     '  ambisonic: 0\n  3D: 1\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
        else:
            text += 'NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
        target.write_text(text, encoding='utf-8')
    return re.search(r'^guid: (\w+)', target.read_text(encoding='utf-8'), re.M)[1]

def main():
    TARGET.mkdir(parents=True, exist_ok=True)
    derived = TARGET / 'Derived'
    derived.mkdir(exist_ok=True)
    for folder in (ROOT/'Assets/Scripts/Audio', ROOT/'Assets/Resources/Audio', derived):
        meta(folder, True)
    for script in (ROOT/'Assets/Scripts/Audio').rglob('*.cs'):
        meta(script)
    conversions = [('清洁布.m4a', 'Wipe.wav', None), ('咀嚼.mp3', 'GnawShort.wav', 0.5)]
    for original, name, duration in conversions:
        output = derived/name
        if not output.exists():
            args = ['ffmpeg', '-v', 'error', '-i', str(SOURCE/original), '-ac', '1', '-ar', '44100']
            if duration:
                args += ['-t', str(duration), '-af', 'afade=t=out:st=0.42:d=0.08']
            subprocess.run(args + [str(output)], check=True)
        meta(output)
    names = {
        1:['切换.wav'], 2:['清脆点击.wav'], 3:['翻页.wav'], 4:['错误.wav'], 5:['金币.wav'],
        6:['脚步1.wav','脚步2.wav','脚步3.wav'], 7:['跑腿送件.wav'], 8:['咖啡豆研磨.wav'], 9:['纸袋.wav'],
        10:['倒水.wav'], 11:['机器.wav'], 12:['打印机.wav'], 13:['敲打塑料壳.wav'], 14:[derived/'Wipe.wav'],
        15:['敲击塑料壳.wav'], 16:['撕胶带.wav'], 17:['塑料.wav'], 18:['小狗粮.wav'], 19:['警报.wav'],
        20:['纽扣6.mp3'], 21:['纸箱移动_12(Carton_move_12)_爱给网_aigei_com.mp3'], 22:['旋转卡位.wav'],
        23:['铃声.wav'],24:['退回.wav'],25:['气泡.wav'],26:['爬行.mp3'],27:['昆虫冲刺.mp3'],28:['拾取.wav'],
        29:['放置.wav'],30:[derived/'GnawShort.wav'],31:['恢复音效.mp3'],32:['翻找.wav'],33:['成就.wav'],
        34:['胖子脚步.wav','胖子脚步2.wav','胖子脚步3.wav'],35:['喷雾2.wav'],36:['跺脚.mp3'],37:['警觉.wav'],
        38:['击中.mp3'],39:['失败2.wav'],40:['解锁.wav'],41:[ROOT/'Assets/Model/Music/V2/开门.wav'],42:['下班铃声.wav'],43:['牛皮纸袋结算.wav'],
        44:[ROOT/'Assets/Model/Music/V2/关门.wav']}
    asset = TARGET/'SoundLibrary.asset'
    if asset.exists():
        print('Library already exists; preserving Inspector configuration.')
        return
    script_guid = meta(ROOT/'Assets/Scripts/Audio/SoundLibrary.cs')
    lines = ['%YAML 1.1','%TAG !u! tag:unity3d.com,2011:','--- !u!114 &11400000','MonoBehaviour:',
        '  m_ObjectHideFlags: 0','  m_CorrespondingSourceObject: {fileID: 0}','  m_PrefabInstance: {fileID: 0}',
        '  m_PrefabAsset: {fileID: 0}','  m_GameObject: {fileID: 0}','  m_Enabled: 1','  m_EditorHideFlags: 0',
        f'  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}','  m_Name: SoundLibrary',
        '  m_EditorClassIdentifier: ','  masterVolume: 1','  musicVolume: 0.65','  effectsVolume: 0.8',
        '  defeatDuckVolume: 0.25','  voiceLimit: 32','  sounds:']
    for number, files in names.items():
        lines += [f'  - id: {number}', f'    enabled: {0 if number in (11,15) else 1}', '    clips:']
        for file in files:
            path = file if isinstance(file, Path) else SOURCE/file
            if not path.exists(): raise FileNotFoundError(path)
            guid = meta(path)
            lines += [f'    - {{fileID: 8300000, guid: {guid}, type: 3}}']
        step = number in (6,26,34)
        pitch = '{x: 0.96, y: 1.04}' if step or number == 30 else '{x: 1, y: 1}'
        cooldown = 0.2 if number in (4,38) else 0.12 if number in (21,30) else 0 if number in (8,10,12,14,18,32,35) else 0.06
        lines += ['    volume: 0.75',f'    pitch: {pitch}',f'    loop: {int(number in (8,10,12,14,18,32,35))}',
            f'    cooldown: {cooldown}',f'    cooldownPerOwner: {int(step or number == 38)}',f'    maxInstances: {1 if number in (19,39,42,43) else 3 if number == 30 else 6 if step else 4}',
            f'    priority: {100 if number == 39 else 80 if number in (4,5,19,31,33,38,42,43) else 20 if number in (26,30,34) else 40}',
            '    nearDistance: 2',f'    farDistance: {24 if number == 34 else 18}',
            '    maxDuration: 0','    fadeOut: 0.04',f'    volumePerLevel: {0.06 if number == 23 else 0}',
            f'    pitchPerLevel: {0.04 if number == 23 else 0}']
    asset.write_text('\n'.join(lines)+'\n', encoding='utf-8')
    meta(asset)
    print('Created 44 definitions; S11/S15 disabled; original audio preserved.')
    from prepare_sound_samples import prepare
    prepare()

if __name__ == '__main__': main()
