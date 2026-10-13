from pathlib import Path
import runpy,hashlib,sys,re
helper=runpy.run_path(str(Path(__file__).resolve().parents[1]/'EnemyGateBuildingContextAudit/prepare-native-assassin-test.py'))
fields=helper['fields'];require=helper['require'];CURRENT=helper['CURRENT'];P=helper['P']
def pack(values):
 result=bytearray(b'\xde'+len(values).to_bytes(2,'big'))
 for key,value in values.items():
  name=key.encode('utf-8');result+=b'\xda'+len(name).to_bytes(2,'big')+name
  result+=b'\xc6'+len(value).to_bytes(4,'big')+value
 return bytes(result)
def prepare(data):
 outer={k:v[0] for k,v in fields(data).items()}
 require(outer[helper['SCHEMA']]==b'\x03','Unsupported schema')
 inner={k:v[0] for k,v in fields(outer[CURRENT]).items()}
 require(inner.get('EnableMod')==b'\xc3','Main mod must already be enabled')
 wanted={'EnableEnemyGatePathfindingFix':b'\xc3',helper['WEIGHTED']:b'\xc2',helper['CLIMB']:b'\xc3'}
 for key,value in wanted.items():
  for values in (outer,inner):
   require(key not in values or values[key] in (b'\xc2',b'\xc3'),'Non-boolean setting')
   values[key]=value
 outer[CURRENT]=pack(inner);after=pack(outer)
 old={k:v[0] for k,v in fields(data).items()};oldinner={k:v[0] for k,v in fields(old[CURRENT]).items()}
 require(all(outer[k]==v for k,v in old.items() if k not in wanted and k!=CURRENT),'Unrelated outer setting changed')
 require(all(inner[k]==v for k,v in oldinner.items() if k not in wanted),'Unrelated current setting changed')
 return after
# Pure schema / exact unrelated value / idempotence regression.
f=pack({helper['SCHEMA']:b'\x03',CURRENT:pack({'EnableMod':b'\xc3',helper['WEIGHTED']:b'\xc3',helper['CLIMB']:b'\xc3','other':b'\x01\xff'}),'other':b'\x01'})
a=prepare(f);require(prepare(a)==a,'Not idempotent')
print('PASS migration settings: defaults, current state, unchanged other values, idempotence')
if '--apply' not in sys.argv:sys.exit(0)
before=P.read_bytes();after=prepare(before)
backup=Path(__file__).resolve().parent/'settings-before.msgpack'
if not backup.exists():backup.write_bytes(before)
# Exclusive write helper also checks concurrent edits; truncate only needed when shrinking.
require(len(after)>=len(before),'Unexpected shrinking format')
helper['apply_exclusive'](P,before,after)
require(P.read_bytes()==after,'Settings readback')
config=P.parents[3]/'config'/'BugfixesAndQoL_Serp.cfg'
raw=config.read_bytes();text=raw.decode('utf-8-sig');section='[GateMigrationDiagnostics]'
if section in text:
 start=text.index(section);end=text.find('\n[',start+len(section));end=len(text) if end<0 else end
 block=text[start:end]
 for key,value in [('DetailedDiagnostics','true'),('MacemenDiagnostics','false')]:
  pattern=r'(?m)^'+key+r'\s*=.*$'
  if re.search(pattern,block):block=re.sub(pattern,key+' = '+value,block)
  else:block+='\n'+key+' = '+value+'\n'
 text=text[:start]+block+text[end:]
else:text+='\n'+section+'\nDetailedDiagnostics = true\nMacemenDiagnostics = false\n'
updated=text.replace('\r\n','\n').replace('\r','').replace('\n','\r\n').encode('utf-8')
require(config.read_bytes()==raw,'Concurrent config change')
config.write_bytes(updated);require(config.read_bytes()==updated,'Config readback')
print('VERIFIED effective hostFix=True, weighted=False, gatehouseClimb=True, Detailed=True, Macemen=False')
