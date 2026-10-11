"""Compare frozen plain and production-transport replays, without altering originals."""
import sys, importlib.util
from pathlib import Path
sys.dont_write_bytecode = True
root=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('bridge_analysis',root/'_inspect/EnemyGateBuildingContextAudit/bridge-log-analysis.py')
analysis=importlib.util.module_from_spec(spec)
spec.loader.exec_module(analysis)
path=Path(sys.argv[1])
plain=analysis.analyze(path.read_bytes())
packed=analysis.analyze(Path(str(path)+'.packed.log').read_bytes())
for key in ('records','captureComplete','deliveryComplete','bridgeFileComplete','bridgeComplete','coverage','definitions','missing','reconstructionErrors','routeChains','routeChainGroups','movementSummary','uniqueBridgeCommands','uniqueBridgeUnits','virtualSummary','controlSummary','artifactSummary'):
    assert plain[key]==packed[key],key
print('PASS frozen transport: every semantic record, definition, command/path/decision chain, movement, coverage and delivery unchanged')
print('Bridge bytes including prefixes:',sum(plain['volumes'].values()),'->',sum(packed['volumes'].values()))
