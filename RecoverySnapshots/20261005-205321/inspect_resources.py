from pathlib import Path
import re,json
root=Path.cwd()
files=[root/'Assets/Scenes/GameScene.unity',root/'Assets/_Recovery/0.unity',root/'Assets/Scenes/NoUseAnymore.unity']
reports=[]
for source in files:
    text=source.read_text(encoding='utf-8-sig')
    heads=list(re.finditer(r'^--- !u!(\d+) &(-?\d+)(?: stripped)?\n',text,re.M))
    blocks={}
    for i,h in enumerate(heads):
        block=text[h.start():heads[i+1].start() if i+1<len(heads) else len(text)]
        blocks[h[2]]=(int(h[1]),block)
    names={}; trans={}; gotrans={}
    for fid,(kind,block) in blocks.items():
        if kind==1:
            n=re.search(r'^  m_Name: (.*)$',block,re.M)
            if n:names[fid]=n[1]
        if kind in (4,224):
            g=re.search(r'^  m_GameObject: \{fileID: (-?\d+)\}',block,re.M)
            f=re.search(r'^  m_Father: \{fileID: (-?\d+)\}',block,re.M)
            if g and f:trans[fid]=(g[1],f[1]);gotrans[g[1]]=fid
    def path(tid):
        seq=[];seen=set()
        while tid in trans and tid not in seen:
            seen.add(tid);go,f=trans[tid];seq.append(names.get(go,'?'));tid=f
        return '/'.join(reversed(seq))
    nodes=[]
    for tid,(go,parent) in trans.items():
        p=path(tid)
        if 'HUDCanvas/ResourcesUI' in p:
            nodes.append({'path':p,'gameObjectID':go,'transformID':tid,'parentTransformID':parent})
    reports.append({'source':str(source),'nodes':sorted(nodes,key=lambda n:n['path'])})
    if source==files[0]:
        resources=[n for n in nodes if n['path']=='HUDCanvas/ResourcesUI']
        icons=[n for n in nodes if n['path'].endswith('/Icon')]
        print(json.dumps({'resourcesRoots':resources,'iconsToRemove':icons},ensure_ascii=False))
        remove_ids=set()
        removed_trans=set()
        for icon in icons:
            icon_path=icon['path']
            for n in nodes:
                if n['path']==icon_path or n['path'].startswith(icon_path+'/'):
                    removed_trans.add(n['transformID'])
                    remove_ids.add(n['gameObjectID'])
                    for comp in re.findall(r'^  - component: \{fileID: (-?\d+)\}',blocks[n['gameObjectID']][1],re.M):
                        remove_ids.add(comp)
        allparts=[text[:heads[0].start()]]
        for h in heads:
            fid=h[2]
            if fid in remove_ids:continue
            block=blocks[fid][1]
            if int(h[1]) in (4,224):
                for tid in removed_trans:
                    block=re.sub(r'^  - \{fileID: '+re.escape(tid)+r'\}\n','',block,flags=re.M)
            allparts.append(block)
        candidate=''.join(allparts)
        for fid in remove_ids:
            if re.search(r'fileID: '+re.escape(fid)+r'(?:[,}])',candidate):
                raise RuntimeError('Remaining reference to removed ID '+fid)
        out=root/'RecoverySnapshots/20261005-205321/GameScene_ResourcesUI_IconsRemoved.unity'
        out.write_text(candidate,encoding='utf-8',newline='\n')
        print(json.dumps({'reconstructedCopy':str(out),'removedBlockIDs':sorted(remove_ids),'removedIconsCount':len(icons)},ensure_ascii=False))
report=root/'RecoverySnapshots/20261005-205321/ResourcesUI_comparison.json'
report.write_text(json.dumps(reports,ensure_ascii=False,indent=2),encoding='utf-8')
for r in reports:
    print(json.dumps({'scene':r['source'],'ResourcesUIPaths':[n['path'] for n in r['nodes']]},ensure_ascii=False))