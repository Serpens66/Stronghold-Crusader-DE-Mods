from pathlib import Path
import json

OUT=Path(__file__).resolve().parent
ROOT=OUT.parents[1]
BASE=ROOT/'_inspect/CrusaderDE-Native-Baseline'
pointer=json.loads((BASE/'CURRENT.json').read_text())
validation=BASE/pointer['semanticDirectory']/'validation/validation-report.json'
assert validation.stat().st_mtime>(OUT/'preflight.json').stat().st_mtime, 'Validation report is stale'
result=json.loads(validation.read_text())
assert result['status']=='ok' and result['rawBaselineUnchanged'] and result['scriptExtenderUnchanged']
review=ROOT/'Shared/Tools/ScriptExtenderUpdate/2.14.1-2.14.2.review.md'
body=review.read_text()
body=body.replace('Status: prepared; baseline update and final acceptance pending.', '''Status: **baseline refresh and static acceptance passed**.

- UpdateForScriptExtender completed successfully, including the integrated fast validation.
- Source commit/tree agree between the checkout, IDENTITY and DATABASE_INFO.
- Ghidra inputs are unchanged; no Ghidra run occurred.
- Database schema 6, the selected analysis generation and all analysis table counts are preserved.
- Delegate, pattern, type, field and vtable records are unchanged apart from source provenance. The changed asset source hash matches the checkout.
- All 1,284 protected files remain hash-identical, including installed plugins, local extender build DLLs, raw baseline and reference exports/projects.
- Fresh Windows PowerShell stats and sound-search queries succeeded. GameAssetManagerAPI is recorded in the updated source-file inventory; the native search interface does not index it as a native function.
- JSON/JSONL, CRLF and scoped git diff checks passed. The Ghidra guard passed its enabled/disabled and changed/unchanged-input cases in fresh Windows PowerShell.
- The known startup crash-handler issue and runtime acceptance remain open; no runtime test was performed.''')
review.write_bytes((body.replace('\r\n','\n').rstrip()+'\n').replace('\n','\r\n').encode())
for p in [OUT/'prepare.py',OUT/'document.py',OUT/'final_checks.py',OUT/'finish.py',OUT/'Test-GhidraGuard.ps1']:
    body=p.read_text(encoding='utf-8-sig')
    p.write_bytes((body.replace('\r\n','\n').rstrip()+'\n').replace('\n','\r\n').encode())
print('Review finalized; fresh successful baseline validation confirmed.')
