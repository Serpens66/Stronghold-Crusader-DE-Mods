from pathlib import Path
p=Path('Testmods/EnemyBridgePathTest/src/BridgeVirtualShadow.cs')
t=p.read_text(encoding='utf-8-sig')
for old,new in [('if(first!=null&&first.Input!=null&&first.Input.Session==bundle.Session&&','if(!groupArtifact&&first!=null&&first.Input!=null&&first.Input.Session==bundle.Session&&'),('if(keepArtifact&&!groupArtifact&&input.Player==planningPlayer&&group.Input!=null)','if(!groupArtifact&&group.Input!=null&&(!keepArtifact||input.Player==planningPlayer))'),('request.InputReason="own-command-pre-separate-topology-copy-exact-physical-grids-match"','request.InputReason=input.Origin+"-separate-topology-copy-exact-physical-grids-match"')]:
 assert old in t,old;t=t.replace(old,new)
p.write_bytes(t.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8'))
