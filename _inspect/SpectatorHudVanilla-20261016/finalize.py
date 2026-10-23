from pathlib import Path
root=Path('Testmods/SpectatorEditorBuildTest')
for p in (root/'Locales').glob('*.txt'):
 s=p.read_text(encoding='utf-8-sig')
 if 'Tools.PowerCancelled=' not in s:
  s+='Tools.PowerCancelled='+('Kraftaktion abgebrochen.' if p.name=='de-DE.txt' else 'Power action cancelled.')+'\n'
 p.write_text(s,encoding='utf-8')
p=root/'UpdateToNewDLL.md';s=p.read_text(encoding='utf-8-sig')
a=s.index('The test mod therefore intercepts only');b=s.index('## Existing native contracts',a)
s=s[:a]+'''The test mod intercepts the managed ExtremePower action through APIShared main 0.9.2
GameActionEvents and schedules selection outside the publisher. Vanilla's target
mode, cursor, previews, configured SH1 RTS controls and cancellation stay native.
There is no mod-owned power mouse/keyboard handler or duplicate target algorithm.

## Vanilla power context and gate HUD repair (2026-10-16)

The canonical native hash above remains mandatory. NativePowerAssembly/NativePowerContext
own four permanent, non-overlapping inline spans. RedBird X64InlineHook rounds to
at least 14 bytes; the installed backend is probed before publication. The complete
parent-function baseline xrefs (including jump-table references) have no interior
entry into these spans. The callback wrapper preserves volatile GPR/XMM state;
RAX and flags are separately saved. Callback decisions test fresh flags after the
wrapper. Inactive paths execute all displaced instructions exactly once.

| RVA / span | Function / contract |
|---|---|
| `0x105593` / 14 | Selection: replace the local actor load into RDX only inside our selection call; retain `RCX=RDX*0x583C`. |
| `0x8CAF8` / 14 | Native power-target branch only: retain R10 tile manager; replace R15 actor for native keep/Lord/team/pause checks. |
| `0x8CE20` / 18 | Native accepted target: capture already written tile `0x86C1330`, enqueue the captured AI action once, suppress duplicate Chore, preserve `R8D=2` continuation. |
| `0x105627` / 14 | Native immediate Gold confirmation: own selection captures one action; other selections preserve native Chore 119. |

A process-lived gateway has the exact Vanilla selection prologue (`push rbx`,
32-byte shadow frame, EBX=power), receives actor as its second Int32 argument,
sets RAX=actor and enters `0x105548`. It bypasses only the local-human availability
prefix and first local-identity load; the original cost, pause, selection, audio,
switch table and epilogue remain authoritative. Its initial write is before
publication; no subsequent executable mutation or release occurs.
Selection function `0x105510`, 326 bytes, SHA-256
`003599E68B7384B75515EAE73EA17EBA7D8B58BD34A748EADDE447FE28E5D88D`.
Cancellation is original `0x897F0`, 119 bytes, SHA-256
`5A244DF882A8C45FE8E7E8C15560986570BF2EADD2C898A23B49D54470B75ADA`.
Target mode `0x60AD56C` (4/5), selected power `0x60AD574`, and the native
submission argument at `0x86C1330` are hash-bound Int32 data contracts. Global
player identity `0x88E3D70` is never written. Cancellation calls Vanilla only
while the same session still owns the matching pending power.

All live selection-body bytes `105541..105656`, power-target bytes `8CAD2..8CF15`
and cancellation bytes are checked against the canonical executable PE sections
before installation. Hook-site bytes have independent exact signatures. These
blocks depend on surrounding branch/register/data contracts, so finding identical
bytes at another address is not a valid relocation fallback. Unknown bytes or
image hash disable only the AI-power adapter. ExtremePowers may own the original
selection/dispatcher entry: the gateway avoids its entry hook, metadata supplies
replacement target kind and costs, and the existing effect dispatcher/API remains
authoritative. No competing dispatcher hook or backend initialization is added.
Inactive ExtremePowers readiness retains Vanilla metadata and behavior.

Gate selection records input intent under the engine monitor and waits for a
completed managed PlayState revision. Main-thread rendering acquires one lease
bound to the session, owner, building/global IDs and concrete HUD objects. Only
the lease's own `InBuildingGameAction` scope overrides panel parameters. Full
Vanilla HUD transitions release without restoring obsolete flags; a direct panel
setup releases the lease before rebuilding. Visibility, sprites and optional
DataContext bindings are restored. A HUD replacement never restores flags into
its new instance. The test toolbar follows global UI visibility, not a particular
bottom HUD tab. Native building selection and global player identity stay intact.

ExtraFeatures 1.0.114 supplies the optional existing gate/bridge button host and
viewmodel. The encapsulated internal adapter verifies version/member contracts,
uses `IsManualOnlyBuilding` and `ApplyManualState(player,global,automatic,source)`,
and preserves the provider's native-ready, map-ready, loading, enabled and mode
gates. Only the test viewmodel command changes; the provider retains its sole
resolver and persistence. Unknown versions hide the integration, not direct
gate commands. Re-audit this internal adapter when ExtraFeatures changes.

Validation: production-generator tests execute real installed RedBird hooks on
private native buffers, both inactive/active branches, AI IDs 2/8, all eight
original selection paths, register/flag preservation and duplicate-Chore
suppression. Pure lifecycle and production optional-adapter tests cover delayed
HUD readiness, repeated transitions, rejection and provider state. Real-game
target effects, visual HUD recovery and combined-mod acceptance remain separate.

''' + s[b:]
s=s.replace('Alle neuen Adapter sind Call-only; sie installieren keinen nativen Hook und aendern\nweder globale Spieleridentitaet noch Diplomatie oder native Auswahl-IDs.', 'Die folgenden Ausfuehrungsadapter sind Call-only; die vier oben dokumentierten\nKontexthooks ergaenzen die native Kraftbedienung. Globale Spieleridentitaet und\nDiplomatie bleiben unveraendert.')
p.write_text(s,encoding='utf-8')
# Only this mod's source/config/test/locale files; generated binaries and unrelated trees stay untouched.
files=[p for p in root.rglob('*') if p.is_file() and not any(x in ('BepInEx','obj','bin') for x in p.relative_to(root).parts) and p.suffix in ('.cs','.csproj','.ps1','.py','.txt','.md','.json','.bat','.xaml')]
for p in files:
 data=p.read_bytes();bom=data.startswith(b'\xef\xbb\xbf'); text=data.decode('utf-8-sig').replace('\r\n','\n'); expected=text.replace('\n','\r\n').encode('utf-8'); expected=(b'\xef\xbb\xbf'+expected) if bom else expected
 if data!=expected:p.write_bytes(expected)
 assert p.read_bytes()==expected and b'\n' not in expected.replace(b'\r\n',b'')
print('CRLF verified:',len(files),'mod text files')
