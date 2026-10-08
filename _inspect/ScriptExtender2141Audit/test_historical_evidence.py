"""Deletion-commit regression: historic content must still match its exact hash."""
from pathlib import Path
import hashlib,importlib.util,subprocess,tempfile,sys
sys.dont_write_bytecode=True
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('curated',ROOT/'_inspect/CrusaderDE-Native-Baseline/tools/semantic/curated_knowledge.py')
module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
repo=Path(tempfile.mkdtemp(prefix='se2141-evidence-'))
def git(*args):
    return subprocess.check_output(['git','-C',str(repo),*args],stderr=subprocess.STDOUT)
git('init');git('config','user.name','Offline Evidence Test');git('config','user.email','offline@example.invalid')
file=repo/'evidence.cs';file.write_bytes(b'original evidence\n');git('add','evidence.cs');git('commit','-m','Evidence')
expected=hashlib.sha256(b'original evidence\r\n').hexdigest().upper()
file.unlink();git('add','-u');git('commit','-m','Delete evidence')
assert module.evidence_source_matches(repo,'evidence.cs',expected)
assert not module.evidence_source_matches(repo,'evidence.cs','0'*64)
print(f'PASS: deletion commit skipped; earlier CRLF-normalized exact hash accepted; wrong hash rejected. Fixture retained: {repo}')
