import os,json,sys,uuid,threading
from pathlib import Path
# A daemon watchdog also covers blocking pipe discovery, connection and writes.
def expire():
 print('Unity bridge exceeded its 45 second total time limit.',file=sys.stderr,flush=True)
 os._exit(124)
watchdog=threading.Timer(45,expire)
watchdog.daemon=True
watchdog.start()
pipe=next(p for p in os.listdir('//./pipe/') if p.startswith('unity-mcp-') and p.endswith('-5332'))
print('Connecting',pipe,flush=True)
with open('//./pipe/'+pipe,'r+b',buffering=0) as f:
 print('Connected',flush=True)
 def read():
  b=bytearray()
  while True:
   c=f.read(1)
   if not c:raise RuntimeError('Unity disconnected')
   b.extend(c)
   if len(b)>4*1024*1024:raise RuntimeError('Unity bridge message exceeded 4 MB')
   if c == b'\n': return json.loads(b)
 msg=read();print('Handshake',msg.get('protocol'),flush=True)
 code=Path(sys.argv[1]).read_text(encoding='utf-8-sig')
 f.write((json.dumps({'type':'Unity_RunCommand','params':{'Code':code,'Title':Path(sys.argv[1]).stem},'requestId':str(uuid.uuid4())})+'\n').encode())
 print('Sent',flush=True)
 while True:
  msg=read()
  if msg.get('type') in ('approval_pending','approval_denied'): print(msg,flush=True);break
  if msg.get('type') in ('command_in_progress','handshake'):continue
  Path('Temp/LiquidUnityResponse.json').write_text(json.dumps(msg,ensure_ascii=False),encoding='utf-8')
  print(json.dumps(msg,ensure_ascii=False)[:6000],flush=True);break
