# SerpNativeAPI V1 – implementierter Übergabestand

## Ergebnis

`SerpNativeAPI` (`SerpNativeAPI_Serp`, Version `0.1.0`) stellt drei katalogisierte Capabilities bereit. `APITest` (`APITest_Serp`, Version `0.1.0`) bleibt in diesem Arbeitsschritt unverändert. `BugfixesAndQoL` und `ExtraFeatures` wurden nicht migriert; ihre spätere Zuordnung steht in `MIGRATION_PLAN.md`.

Die kanonische Native-Basis ist `_inspect/CrusaderDE-Native-Baseline/CURRENT.json` mit DLL-SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.

## Gatehouse

- Bestätigte native Funktion: RVA `0xB73D0`, Ende exklusiv `0xB7CE5`, Größe 2325 Bytes.
- Reproduzierter Funktions-SHA-256: `F73E9FF6F69D9EC1ECD59D528BC6D4861739F54E0A9C59C6E6BAD91369FA57C8`.
- Bestätigte Immediates:
  - AI-Schließdistanz RVA `0xB7BC3`, Vanilla 200 native Einheiten = 25 Felder.
  - AI-Wiederöffnungswartezeit RVA `0xB7BCA`, Vanilla 1200 Ticks = 30 Sekunden.
  - Human-Schließdistanz RVA `0xB7BD3`, Vanilla 140 native Einheiten = 17,5 Felder.
  - Human-Wiederöffnungswartezeit RVA `0xB7C35`, Vanilla 100 Ticks = 2,5 Sekunden.
- Der Resolver akzeptiert ausschließlich den festen Katalog des vollständigen bekannten DLL-Hashes. Es gibt keinen AOB-Fallback.
- Funktionshash, ausführbare Section, Funktionsgrenzen, Instruktionsbytes, Immediate-Grenzen und Vanilla-Werte werden vor Freigabe geprüft. Vor jeder späteren Mutation werden die unveränderlichen Opcodebytes und der erwartete aktuelle Zustand erneut gelesen.
- `gatehouse-distance-origin` besitzt ausschließlich `[0xB7B70, 0xB7BBB)` und schaltet explizit zwischen `VanillaBuildingBegin` und `BuildingBoundsCenter`. Vorgesehener späterer Consumer ist `BugfixesAndQoL`.
- `gatehouse-timing` besitzt ausschließlich die vier Immediates. `Enabled=false` stellt nur deren Vanilla-Werte wieder her und berührt den Distanzursprung nicht. Vorgesehener späterer Consumer ist `ExtraFeatures`.
- Original- und Mittelpunktblock sind jeweils 75 Bytes lang und werden unabhängig von den Immediates reserviert, live verifiziert und transaktional zurückgerollt. Die Byte- und Disassembly-Evidenz steht in `_inspect/gatehouse-center-patch.md`.
- Beide Capability-Transaktionen verwenden wegen ihrer gemeinsamen 4-KiB-Page denselben Mutations-Lock. Ihre Intervalle, Besitzer, erwarteten Zustände und Diagnosen bleiben getrennt; eine capabilityspezifische Byteabweichung sperrt nicht automatisch die andere.
- Der erste Ersatzblock crashte beim ersten relevanten Unit-Zugriff, weil sein X-`cdq` den für den nachfolgenden Y-Read noch lebenden Unit-Offset in `RDX` zerstörte. Der korrigierte Block lädt X und Y vor dem ersten `cdq`; die Tests pinnen alle 75 Bytes und diese Registerreihenfolge.

## Selected unit command

- Die API besitzt hierfür keinen RVA, Scanner, nativen Detour, unmanaged Delegate oder Trampoline mehr.
- Quelle ist ausschließlich das öffentliche Script-Extender-Event `TribeR3EventHooks.OnTribeIssueOrderWithTarget`.
- Genau eine dauerhafte Subscription wird bei der ersten Verbraucherregistrierung erzeugt. Nur `EventHookPhase.Pre` wird vermittelt.
- Der öffentliche Kontext ist unveränderlich und enthält `TribeAICommand`, `TribeId`, `TargetValue1`, `TargetValue2` und `Argument6`.
- API-Callbacks laufen ordinal nach Besitzer-GUID, sind einzeln fehlerisoliert und können über ihr Handle aktiviert, deaktiviert oder entfernt werden.
- Die API verändert weder die Script-Extender-EventArgs noch `SkipOriginalFunction` oder Rückgabewerte. Fremde direkte Abonnenten des Extender-Events liegen außerhalb dieser Garantie.
- Weil der Extender die native Versionsanpassung übernimmt, bleibt diese Capability auch bei einem unbekannten CrusaderDE.dll-Hash verfügbar.

## Readiness und Diagnosen

- `Pending` gilt bis zum einmaligen `LibraryLoaded`-Abschluss.
- Distance Origin, Gatehouse Timing und Selected Unit Command werden unabhängig veröffentlicht, soweit die gemeinsame Build-/Funktionsprovenienz gültig ist.
- Unbekannte Hashes ergeben für beide Gatehouse-Capabilities `UnsupportedBuild` und für Selected Unit Command `Available`, ohne native Operation der API.
- `Unavailable` ist ausschließlich einem globalen Publikationsfehler vorbehalten.
- Erfolgreiche Gatehouse-Anwendungen loggen Human-/AI-Werte sowohl semantisch in Feldern/Sekunden als auch nativ in Einheiten/Ticks.

## Tests und Laufzeitabnahme

`_inspect/SerpNativeAPITests` verwendet Fake-PE-, Speicher-, Seitenschutz- und Eventadapter. Abgedeckt sind feste Hash-/RVA-/Opcodevalidierung ohne Decoy-Fallback, Mittelpunktarithmetik einschließlich Halbfeldern und umgekehrten Bounds, Symmetrie und Chebyshev-Diagonalen, getrennte Intervalle und Besitzer, explizite Rückkehr zu Vanilla, capabilityspezifische Fremdmutation, Code- und Werte-Rollback, Rundung und native Grenzen, Ein-/Mehrseiten-Transaktionen, kombinierte Cleanupfehler sowie Eventphase, Reihenfolge, Fehlerisolierung, Reentranz, Idempotenz und genau eine Subscription.

APITest aktiviert unverändert nur Gatehouse Timing mit 0 Sekunden und 5 Feldern für Human und AI; nach der Trennung aktiviert dieser Aufruf keinen Mittelpunkt mehr. Die Anpassung des Testverbrauchers und die erneute Laufzeitabnahme sind gemäß `MIGRATION_PLAN.md` erst in der späteren Pilotphase vorgesehen.
