from pathlib import Path
import re, subprocess, xml.etree.ElementTree as ET
repo=Path.cwd().parent/'SHCDE-APIShared'
def git(*args):
    return subprocess.check_output(['git','-C',str(repo),*args],text=True).strip()
tracked=git('ls-files').splitlines()
files={p for p in tracked if (repo/p).is_file()}
files.update(p.relative_to(repo).as_posix() for p in repo.rglob('*') if p.is_file() and
             not any(part in ['.git','bin','obj','BepInEx','.local','.vs'] for part in p.relative_to(repo).parts))
failures=[]
for relative in sorted(files):
    p=repo/relative
    if relative.startswith('_inspect/') or p.name in ['AGENTS.md','MIGRATION_PLAN.md','UpdateToNewDLL.md']:
        failures.append('Historical/instruction artifact: '+relative)
    if p.suffix in ['.cs','.csproj','.props','.targets','.ps1','.bat','.md','.json','.xaml','.yml','.sln']:
        text=p.read_bytes().decode('utf-8-sig')
        if '\x00' in text or re.search(r'(?<!\r)\n',text): failures.append('Invalid text/line endings: '+relative)
        if re.search(r'(?i)[ED]:[\\/]',text): failures.append('Personal absolute path: '+relative)
    if p.suffix in ['.csproj','.props','.targets','.xaml']: ET.parse(p)
    if p.suffix=='.md':
        text=p.read_text(encoding='utf-8-sig')
        for link in re.findall(r'\[[^\]]*\]\(([^)]+)\)',text):
            link=link.strip('<>').split('#',1)[0]
            if not link or ':' in link: continue
            if not (p.parent/link).exists(): failures.append(f'Broken link: {relative} -> {link}')
if git('diff','77e0c62','--','src','Properties','info.json'):
    failures.append('Production runtime or manifest changed')
if failures: raise SystemExit('\n'.join(failures))
print(f'PASS: {len(files)} source/config/doc files; XML, Markdown links, CRLF, no personal paths/historical artifacts; runtime and versions unchanged.')
