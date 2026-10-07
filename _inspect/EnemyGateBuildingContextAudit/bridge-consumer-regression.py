"""Actual producer -> bounded file delivery -> productive local handoff replay."""
from pathlib import Path
import importlib.util,subprocess,re,struct
base=Path(__file__).resolve().parent;workspace=base.parents[1]
spec=importlib.util.spec_from_file_location('binary',base/'bridge-artifact-analysis.py');binary=importlib.util.module_from_spec(spec);spec.loader.exec_module(binary)
exe=workspace/'Testmods/EnemyBridgePathTest/tests/bin/EnemyBridgePathTest.PolicyTests.exe';cwd=exe.parents[2]
old=base/'real-plan-20261007-151016/planning.bin'
c=subprocess.run([str(exe),'--consumer-replay',str(old)],cwd=cwd,capture_output=True,text=True)
assert c.returncode==2 and 'missing-historical-consumer-inputs' in c.stdout
text=(workspace/'_inspect/BridgePlanningTests/real-consumer-native-tests.log').read_text()
files=re.findall(r'artifact=(.*?); captureMs=',text);assert len(files)==2
for file in files:
 path=Path(file).parent/'bridge-7-700.bin';meta,sections,digest=binary.decode(path)
 assert meta['planningConsumerCaptureVersion']=='2' and float(meta['planningConsumerCopyMs'])>0
 assert len(sections['plan/consumer/pre/unitTiles'])==641600 and len(sections['plan/consumer/pre/tribes'])==0x2a+4500*0x688
 assert struct.unpack_from('<i',sections['plan/military/entryPlayer'],0x379D974-0x379AE00)[0]==4
 assert struct.unpack_from('<i',sections['plan/military/exitPlayer'],0x379D974-0x379AE00)[0]==6
 c=subprocess.run([str(exe),'--consumer-replay',str(path)],cwd=cwd,capture_output=True,text=True)
 assert c.returncode==0 and 'captured-local-handoffs-match' in c.stdout and 'fullMilitaryConsumer=Unknown' in c.stdout,(c.stdout,c.stderr)
 c2=subprocess.run([str(exe),'--consumer-closure',str(path)],cwd=cwd,capture_output=True,text=True);assert c2.returncode==0 and 'fullCandidateBuilderAndMilitaryReplay=Unknown' in c2.stdout
 assert 'fullWeightReplay=Matched-actual-input-limit-' in c.stdout
 print('PASS synthetic capture/write/import/local consumers',meta['planningDirty'],digest,'consumerCopyMs',meta['planningConsumerCopyMs'])
print('PASS original real consumer coverage remains Unknown; synthetic evidence is not historical evidence')
