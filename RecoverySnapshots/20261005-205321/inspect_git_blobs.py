from pathlib import Path
import subprocess,json
root=Path.cwd()
out=root/'RecoverySnapshots/20261005-205321'
for oid in ['84ab965febeda0ff1e0ab4e682b68fc31155d945','fdcd46e04a5eaeef5216a1281bff299934d4caf9']:
    data=subprocess.check_output(['git','cat-file','blob',oid])
    p=out/('git-unreachable-'+oid+'.blob')
    p.write_bytes(data)
    print(json.dumps({'oid':oid,'bytes':len(data),'head':data[:180].decode('utf-8',errors='replace'),'looksLikeScene':data.startswith(b'%YAML'),'hasResourcesUI':b'm_Name: ResourcesUI' in data},ensure_ascii=False))