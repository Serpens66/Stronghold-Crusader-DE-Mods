# SerpNativeAPI V1 – implementierter Übergabestand

## Ergebnis

`SerpNativeAPI` (`SerpNativeAPI_Serp`, Version `0.1.0`) stellt zwei katalogisierte Capabilities bereit. `APITest` (`APITest_Serp`, Version `0.1.0`) ist der eigenständige Verbraucher. `BugfixesAndQoL` und `ExtraFeatures` wurden nicht migriert und bleiben absichtlich inkompatibel mit APITest.

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
- Die vier Immediates werden exklusiv und transaktional geschrieben. Die aktuellen RVAs liegen gemeinsam auf der 4-KiB-Page ab RVA `0xB7000`; der Schutzadapter arbeitet dennoch allgemein pro Page und restauriert bei zukünftigen Mehrseitenkatalogen jeden eigenen Schutzwert. Teilfehler rollen alle Werte zurück und Cleanupfehler werden gemeinsam gemeldet.
- Der Distanzblock `[0xB7B70, 0xB7BBB)` wird beim ersten Anwenden derselben Capability auf den Mittelpunkt der vollständigen Gebäude-Bounds umgestellt. Er bleibt auch bei `Enabled=false` aktiv, während die vier Timingwerte auf Vanilla zurückkehren.
- Original- und Ersatzblock sind jeweils 75 Bytes lang und werden gemeinsam mit den Immediates reserviert, live verifiziert und transaktional zurückgerollt. Die Byte- und Disassembly-Evidenz steht in `_inspect/gatehouse-center-patch.md`.
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
- Gatehouse und Selected Unit Command werden unabhängig initialisiert.
- Unbekannte Hashes ergeben Gatehouse `UnsupportedBuild` und Selected Unit Command `Available`, ohne native Operation der API.
- `Unavailable` ist ausschließlich einem globalen Publikationsfehler vorbehalten.
- Erfolgreiche Gatehouse-Anwendungen loggen Human-/AI-Werte sowohl semantisch in Feldern/Sekunden als auch nativ in Einheiten/Ticks.

## Tests und Laufzeitabnahme

`_inspect/SerpNativeAPITests` verwendet Fake-PE-, Speicher-, Seitenschutz- und Eventadapter. Abgedeckt sind feste Hash-/RVA-/Opcodevalidierung ohne Decoy-Fallback, Mittelpunktarithmetik einschließlich Halbfeldern und umgekehrten Bounds, Symmetrie und Chebyshev-Diagonalen, Codeblock-Rollback, unabhängige Capabilities, Besitzkonflikte, Rundung und native Grenzen, die reale Einseitenlage sowie ein künstlicher Mehrseitenkatalog, Rollback und kombinierte Cleanupfehler sowie Eventphase, Reihenfolge, Fehlerisolierung, Reentranz, Idempotenz und genau eine Subscription.

APITest aktiviert Gatehouse fest mit 0 Sekunden und 5 Feldern für Human und AI. Die Assassin-Fachlogik reagiert ausschließlich auf den typisierten `TribeAICommand.UnitStop`. Der Nutzer hat beide Funktionen im Spiel bereits grundsätzlich bestätigt; nach dieser Härtung ist ein erneuter Laufzeittest vorgesehen.
