from pathlib import Path
p=Path('Testmods/EnemyBridgePathTest/src/BridgeVirtualShadow.cs');t=p.read_text(encoding='utf-8-sig')
old='(group.PlanningRoot==planningBundleOp||group.DecisionRoot==planningBundleOp?"exact-root":"unresolved-own-command-only")'
new='(planningBundleOp!=0&&(group.PlanningRoot==planningBundleOp||group.DecisionRoot==planningBundleOp)?"exact-root":"unresolved-own-command-only")'
assert old in t;t=t.replace(old,new)
old='(request.PlanningRoot==bundleRoot?"same-parent-root":request.DecisionRoot==bundleRoot?"retained-decision-root":"unresolved")'
new='(bundleRoot!=0&&request.PlanningRoot==bundleRoot?"same-parent-root":bundleRoot!=0&&request.DecisionRoot==bundleRoot?"retained-decision-root":"unresolved")'
assert old in t;t=t.replace(old,new);p.write_bytes(t.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8'))
