"""Extract individual takes from recordings containing multiple takes.

Run with --apply to update only clip references in the existing library. Original
recordings and all Inspector tuning values are preserved. Custom clip overrides
are skipped rather than replaced. Ranges are seconds in the original recording.
"""
import argparse
import re
import subprocess
from pathlib import Path
from build_sound_library import ROOT, SOURCE, TARGET, meta

# Loop beds use continuous sections, rather than sequences of start/stop takes.
RECIPES = {
    1: [('切换.wav', [(0,.56),(1,1.73),(2,2.58),(3,3.37),(4,4.59),(5,5.35)])],
    2: [('清脆点击.wav', [(0,.20),(.74,.94),(1.45,1.68)])],
    3: [('翻页.wav', [(.18,1.45),(1.85,3.64),(4.18,5.34)])],
    6: [('脚步1.wav', [(.60,.85)]), ('脚步2.wav', [(.40,.65)]), ('脚步3.wav', [(.76,1.03)])],
    7: [('跑腿送件.wav', [(0,1.09),(1.95,2.91),(4.08,4.94),(5.65,6.40),(8.05,8.73)])],
    8: [('咖啡豆研磨.wav', [(10.5,13.3)])],
    9: [('纸袋.wav', [(0,1.29),(1.64,3.26),(3.72,4.77)])],
    10: [('倒水.wav', [(1,4)])],
    13: [('敲打塑料壳.wav', [(.12,.63),(1.20,2.01),(3.03,3.46),(4.05,4.50),(5.62,6.14),
                             (7.28,7.75),(8.50,8.95),(9.45,9.94),(10.71,11.24),(11.91,12.40)])],
    14: [('清洁布.m4a', [(1,2.5)])],
    16: [('撕胶带.wav', [(0,.66),(1.17,2.05)])],
    17: [('塑料.wav', [(.16,.66),(.80,1.30),(1.60,2.10)])],
    22: [('旋转卡位.wav', [(0,.57),(1.64,2.04),(3.07,3.53),(4.56,5.11),(5.99,6.79)])],
    23: [('铃声.wav', [(0,.95),(1.90,2.72),(3.69,4.46)])],
    25: [('气泡.wav', [(0,.17),(.51,.73),(1.10,1.30),(1.67,1.90)])],
    26: [('爬行.mp3', [(.20,.60),(3.55,3.95),(7.02,7.42)])],
    28: [('拾取.wav', [(0,.61),(2.40,2.90),(3.75,4.53)])],
    29: [('放置.wav', [(0,.48),(1.19,1.60),(2.46,2.99),(3.63,4.58)])],
    34: [('胖子脚步.wav', [(2.89,3.38)]), ('胖子脚步2.wav', [(0,.40)]), ('胖子脚步3.wav', [(0,.37)])],
    35: [('喷雾2.wav', [(0,.76)])],
    36: [('跺脚.mp3', [(1.05,1.49),(5.47,5.86),(6.13,6.45)])],
    40: [('解锁.wav', [(0,2.22),(6.82,9.31),(10.59,13.13)])],
    42: [('下班铃声.wav', [(0,1.12),(1.94,3.07)])],
    43: [('牛皮纸袋结算.wav', [(0,1.29),(1.64,3.26),(3.72,4.77)])],
}

def prepare():
    directory = TARGET/'Derived'/'Samples'
    directory.mkdir(parents=True, exist_ok=True)
    meta(directory, True)
    library = TARGET/'SoundLibrary.asset'
    text = library.read_text(encoding='utf-8')
    total = 0
    for number, sources in RECIPES.items():
        pattern = rf'(  - id: {number}\n.*?    clips:\n)(.*?)(?=    volume:)'
        match = re.search(pattern, text, re.S)
        if match is None: raise ValueError(f'Missing library entry S{number:02}')
        old_guids = set(re.findall(r'guid: (\w+)', match[2]))
        permitted = {meta(SOURCE/name) for name, _ in sources}
        if number == 14: permitted.add(meta(TARGET/'Derived'/'Wipe.wav'))
        # Previously generated samples are safe to regenerate; unrelated user overrides are not.
        permitted.update(meta(p) for p in directory.glob(f'S{number:02}_*.wav'))
        if not old_guids.issubset(permitted):
            print(f'Skipped custom clip override for S{number:02}')
            continue
        references = []
        index = 0
        for name, ranges in sources:
            for start, end in ranges:
                index += 1
                duration = end-start
                if duration <= .02: raise ValueError((number,start,end))
                output = directory/f'S{number:02}_{index:02}.wav'
                subprocess.run(['ffmpeg','-v','error','-y','-i',str(SOURCE/name),
                    '-ss',str(start),'-t',str(duration),'-ac','1','-ar','44100',
                    '-af',f'afade=t=in:d=0.004,afade=t=out:st={duration-.015:.6f}:d=0.015',
                    '-c:a','pcm_s16le',str(output)],check=True)
                guid = meta(output)
                references.append(f'    - {{fileID: 8300000, guid: {guid}, type: 3}}\n')
                total += 1
        text = text[:match.start(2)]+''.join(references)+text[match.end(2):]
    library.write_text(text,encoding='utf-8')
    print(f'Prepared {total} samples across {len(RECIPES)} definitions; only clip references changed.')

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--apply', action='store_true')
    if parser.parse_args().apply: prepare()
    else: print(f'{len(RECIPES)} definitions; use --apply to extract samples and update clip references.')

if __name__ == '__main__': main()
