from pathlib import Path
def write(p,s):
    expected=s.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8')
    Path(p).write_bytes(expected)
    assert Path(p).read_bytes()==expected

for p in ['Testmods/EnemyGatePathfindingTest/src/SamePclGateRouteRuntime.cs',
          'Testmods/EnemyGatePathfindingTest/src/EnemyGatePathfindingRuntime.cs']:
    s=Path(p).read_text(encoding='utf-8-sig').replace('cursorThrottleDeferrals','cursorValidationDeferrals').replace('CursorThrottleDeferrals','CursorValidationDeferrals').replace('cursorRequestsPublished','cursorValidationRequests').replace('CursorRequestsPublished','CursorValidationRequests')
    if p.endswith('SamePclGateRouteRuntime.cs'):
        s=s.replace('            if (!Installed || Interlocked.CompareExchange(ref cursorValidationActive, 1, 0) != 0)\n                return false;', '''            if (!Installed) return false;
            if (Interlocked.CompareExchange(ref cursorValidationActive, 1, 0) != 0)
            {
                Interlocked.Increment(ref cursorValidationDeferrals);
                return false;
            }''')
        s=s.replace('AiTacticalTargetAdapterEmitter.SlotDepthOffset)) != 0)\n                    return false;', '''AiTacticalTargetAdapterEmitter.SlotDepthOffset)) != 0)
                {
                    Interlocked.Increment(ref cursorValidationDeferrals);
                    return false;
                }''')
    write(p,s)

p='BugfixesAndQoL/tests/ExtendedShiftCommandQueue.Tests/Program.cs'
s=Path(p).read_text(encoding='utf-8-sig')
needle='        Check(string.Equals(actualSha, expectedSha, StringComparison.Ordinal), "canonical native SHA-256");'
s=s.replace(needle,needle+'''
        // Validate the runtime's exact feedback contracts against installed PE bytes.
        byte[] mappedFeedbackCode = new byte[0x90740];
        foreach (var block in new[] { (0x8F3DA,10), (0x8F3EA,10), (0x8F3FE,10),
            (0x9002D,13), (0x90729,20), (0x8D323,11) })
            image.AsSpan(RvaToRawOffset(image, block.Item1), block.Item2)
                .CopyTo(mappedFeedbackCode.AsSpan(block.Item1));
        NativeGroundMoveFeedbackReader.ValidateContract(mappedFeedbackCode);
        Check(true, "installed native cursor rejection/terminal dispatch/mode read contracts");
        mappedFeedbackCode[0x9002D] ^= 1;
        bool rejectedFeedbackCode = false;
        try { NativeGroundMoveFeedbackReader.ValidateContract(mappedFeedbackCode); }
        catch (InvalidOperationException) { rejectedFeedbackCode = true; }
        Check(rejectedFeedbackCode, "changed native feedback contract rejects reader installation");
''')
write(p,s)

notes='''

## 2026-10-02: unmittelbarer Cursor und native Formationsfreigabe

Logbefund vom 01.10.: Editor-Epoche 23:48:47.548â€“23:50:15.458: 115 bewiesene
Gateblockaden, 685 erzwungene Cursorablehnungen, 835 fehlende passende
Cachetreffer, 208 DoppelprÃ¼fungen, 21 erfolglose Referenzen; mittlere PrÃ¼fdauer
0,229 ms, Maximum 0,503 ms. Keine Scope-Konflikte oder Exceptions, IntegritÃ¤t PASS.
Der Nutzer bestÃ¤tigte durch deaktivierte Formation, dass die grÃ¼nen Zielpunkte
aus der Formationsvorschau stammen. KI-Epoche 23:50:24.292â€“23:51:41.890:
9.132 KI-Suchen, 0 NoRoute; Tor 819/global 2423576, Eroberer 6â†’7,
Generation 5â†’6 exakt aufgeholt. Ein GebÃ¤ude-Scope-Konflikt (Suchwert 1,
Tribe-Besitzer 5) bleibt offen; keine Exceptions oder Snapshotfehler.

Der bestehende Cursoradapter prÃ¼ft bei Cachefehler sofort eine reprÃ¤sentative
Vanilla-Einheit: leere Referenzmaske zuerst, Policysuche zuletzt, beide Scopes
mit finally. Nur Referenzerfolg + Policyfehler + verworfene Gatekante sperren.
32 exakte LRU-CacheplÃ¤tze mit maximal 200 ms Alter beschrÃ¤nken nur gespeicherte
Ergebnisse; verdrÃ¤ngte Ziele werden neu geprÃ¼ft. SchlÃ¼ssel: Spieler, Unit-/Global-ID,
Start/Ziel, beide PCLs, Karten-Epoche, Policygeneration und Maskenfingerprint.
Verschachtelung, ungÃ¼ltiger Kontext, Ausnahme oder Snapshotwechsel bleiben offen.
Der bisherige periodische Cursor-DoppelprÃ¼fpfad entfÃ¤llt. Aggregierte Suchzahl und
PrÃ¼fdauer bleiben erhalten; cursorValidationRequests/Deferrals ersetzen die
historischen Request-/Throttle-Bezeichnungen.

BugfixesAndQoL bindet nur zusÃ¤tzliche Formationsvorschauen an die fertige native
Bodenfreigabe. Der erste bestehende sichtbare Tile-Callback liest einmal pro
Renderdurchlauf; Draw-List-Reset ist dessen Ende. Keine neue Wegsuche, kein neuer
Hook und keine Testmod-/APIShared-AbhÃ¤ngigkeit. BestÃ¤tigung gilt fÃ¼r Spieler,
Tribe, AuswahlgrÃ¶ÃŸe, Modus und den festen Gestenpunkt. Nicht bestÃ¤tigte oder
abgelehnte Ausgabe verbirgt die Vorschau. Beim Ziehen bleibt der Befehlsanker fest;
Auswahl-/Karten-/Steuerungswechsel und Loslassen verwerfen die Geste. Native
Vanilla-Befehlsmarker und Overflow-VerÃ¶ffentlichung bleiben getrennt erhalten.

Runtime-Projekte verwenden die echte installierte Assembly-CSharp.dll. Keine neuen
Spielmemberzugriffe; bereits benutzte Ã¶ffentliche Unit-/Player-APIs bleiben gleich.
Statische Runtime-/Event-/Hookwurzeln tragen die Arbeit nach Startup-Cleanup;
keine neuen MonoBehaviour-Lifecyclepfade. Hookspannen, RedBird-Adapter und Fixes-/SE-
Besitzer bleiben unverÃ¤ndert. Versionen und README unverÃ¤ndert.

Spielabnahme dieser Ã„nderung noch offen: schnelle erlaubte/gesperrte Hoverwechsel,
kurzer Klick und gehaltene Formation unter beiden Maussteuerungen, eigener
Bergfried, echter Umweg, Kartenwechsel und dieselben Gesten ohne Testmod.
'''
p='Testmods/EnemyGatePathfindingTest/PROJECT_FINDINGS.md'; write(p,Path(p).read_text(encoding='utf-8-sig')+notes)
native='''

## 2026-10-02: final native ground feedback and formation preview

Revalidated installed native SHA-256 FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2
and real managed SHA-256 BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789.
Feature audit covers RunTick 86680 â†’ 8B7E0 â†’ 8C5F0; representative selection
18D460 / route mode 18DC40; E7C40, E2610, DB650 and the E2CA0/E9D90/E9FF0
fallbacks; terminal cursor dispatch; renderer 41D60/436DE/41D10; command staging
195E30 â†’ chore17/10AE0 â†’ 196100 â†’ 11B520 â†’ 196280/F4930.

Ground reject 8F3DA writes detail -10 at 60AD560, image 41 at 60AD548,
file AC at 60AD54C and command 11 at 60AD55C. Final cursor dispatch table
90088 entry 3 goes to 90028: 9002D/90033 publishes kind 3 at 34A9E4C.
Approved ordinary ground starts file 6B/image 0, command 1/detail 0; applicable
special ground output uses file 6B/image 20, command 9/detail 0. Type-specific
positive fallback results remain authoritative. Object/wall contexts are excluded.
Terminal kind 3, mode 1 and coherent player/group/count/anchor are required;
unknown or rejected output never authorizes extra green formation markers.

Read-only RVAs: player 88E3D70, active tribe 7CC6720, selection count 67E8420,
mode 67E8410, cursor X/Y 3A11E2C/3A11E30, hovered unit 3A11DF0,
hovered building 3A11DE4, wall 3A11E34, cursor kind 34A9E4C,
file/image/command/detail 60AD54C/548/55C/560. Cursor offsets corroborated by
installed-layout GameCursorManager (unit +30, building +24, wall +74).
The new reader validates unchanged rejection, terminal-dispatch and mode bytes
before use; no executable memory is changed.

IMPORTANT: 41D60 calls 41D10 at the END, after visible tiles and cursor drawing.
It clears 34A9E4C immediately before that reset. Therefore authorization runs in
the first existing 436DE callback of each render pass, not in the reset callback;
reset only invalidates per-pass memoization. A confirmed gesture keeps its fixed
anchor when hover moves during spacing adjustment. EngineInterface.run and
existing input/map paths invalidate changed selection/gesture context. No native
search is added to marker rendering. 195E30 queues before checking feedback;
no additional command veto or artificial replacement order is introduced.

Cursor adapter 8F1BF uses immediate reference-first/filtered-last DB650 on demand.
DB650 scratch mutation is confined to this already audited non-search cursor
callsite; active direct/tactical scopes and reentry defer fail-open. Both scopes
restore via finally. Snapshot identity, epoch and generation guard memoization.
No new hook spans/owners, SE/Fixes conflicts or public APIShared interfaces.
'''
p='_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/MOVE_COMMAND_RELEASE.md'; write(p,Path(p).read_text(encoding='utf-8-sig')+native)
write('_inspect/finalize-cursor-marker-checks.py',Path('_inspect/finalize-cursor-marker-checks.py').read_text())
