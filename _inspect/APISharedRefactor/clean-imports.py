from pathlib import Path
import re,subprocess
ROOT=Path(__file__).resolve().parents[2]
def read(p):return p.read_text(encoding='utf-8-sig')
def write(p,s):p.write_bytes(s.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8'))
changed=subprocess.check_output(['git','diff','--name-only'],cwd=ROOT,text=True).splitlines()
for rel in changed:
    p=ROOT/rel
    if not p.exists():continue
    if '/before/' in rel or rel.startswith('_inspect/CrusaderDE-Native-Baseline/'):
        p.write_bytes(subprocess.check_output(['git','show','HEAD:'+rel],cwd=ROOT));continue
    if p.suffix!='.cs':continue
    s=read(p)
    # An import inferred from an assertion string is not a real dependency.
    without=re.sub(r'//[^\n]*|/\*.*?\*/|@?"(?:""|\\.|[^"\\])*"','',s,flags=re.S)
    for domain in ['ModSettings','GameModes','SerpsMods']:
        symbols=[]
        for source in (ROOT/'APIShared/src'/domain).glob('*.cs'):
            symbols+=re.findall(r'\b(?:class|struct|enum|interface)\s+(\w+)',read(source))
        if domain=='ModSettings':symbols+=['PresetLocal']
        if not any(re.search(r'\b'+x+r'\b',without) for x in symbols):
            s=s.replace('using APIShared.'+domain+';\n','')
    write(p,s)
p=ROOT/'_inspect/ExtraFeaturesSessionTests/GeneratedHarness.cs';s=read(p)
# The existing harness's feature stubs move together with their consumers.
start=s.index('namespace Shared {');end=s.index('\nnamespace ',start+1)
block=s[start:end]
if 'GameplayFeature' in block:
    # Split Serps stubs from the local lifecycle helper in this tiny fixture.
    block=block.replace('internal enum GameplayFeatureId','internal enum GameplayFeatureId')
    feature=re.search(r'\s*(?:internal|public) enum GameplayFeatureId.*?\n}',block,re.S)
    # handled explicitly below, based on the single-line declarations
    lines=block.splitlines();shared=[];serps=[]
    for line in lines[1:-1]:
        (serps if 'GameplayFeature' in line else shared).append(line)
    s=s[:start]+'namespace Shared {\n'+'\n'.join(shared)+'\n}\nnamespace APIShared.SerpsMods {\n'+'\n'.join(serps)+'\n}\n'+s[end:]
    write(p,s)
p=ROOT/'_inspect/APISharedRefactor/projects.txt'
write(p,'\n'.join(x for x in read(p).splitlines() if '/before/' not in x and 'AssassinSharedTests' not in x)+'\n')
print('Unused assertion-only imports and archived snapshots cleaned.')
