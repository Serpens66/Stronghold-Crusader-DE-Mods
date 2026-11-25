# Verifizierte Zusatzbefunde des Rettungsaudits

Stand: 25.11.2026. Ausschließlich statischer Review und isolierte Managed-Ausführung. Keine Installation, kein Spielstart, kein nativer Aufruf und keine Hook-Veröffentlichung. Keine Runtimekorrektur im Rahmen dieses Auditauftrags.

## F1: Fehler beim Formatieren eines Beobachterfehlers durchbricht die AIV-Isolation

**Status:** bestätigte Vertragsverletzung im seltenen Fehlerpfad, durch tatsächlichen Produktions-Dispatch offline reproduziert. Kein Nachweis eines bereits aufgetretenen Spielschadens und keine Aussage über die Häufigkeit gewöhnlicher Spielerfälle.

**Ort:** `APIShared/src/Diagnostics/AivBuildStepCapability.cs:264–266,285–286,292–301`, insbesondere die Stringinterpolation `{ex}` beim Aufruf von `NativeApiLog.Error`.

**Konkreter Fehlerfall:** Ein registrierter öffentlicher AIV-Beobachter wirft aus `TryBegin` beziehungsweise `Complete` eine Exception, deren virtuelles `ToString()` selbst wirft. `Dispatch` fängt zunächst den Beobachterfehler. Danach ruft sein Catch `LogCallbackFailure` auf. Dieser formatiert die fremde Exception vor dem Eintritt in den geschützten zentralen Logger. Die neue Formatierungsexception kann deshalb aus dem Fehlerpfad entkommen.

Folgen, einzeln reproduziert:

| Fall | Originalaufrufe | Gesunder Begin/Complete | Ergebnis |
|---|---:|---:|---|
| Begin wirft Exception mit werfendem `ToString` | 0 | 0 / 0 | Die Formatierungsexception verhindert Vanilla und weitere Beobachter. |
| Complete wirft so eine Exception nach erfolgreichem Original | 1 | 1 / 0 | Der native Ergebniswert wird durch die Formatierungsexception ersetzt; später abzuarbeitende gesunde Receipts erhalten kein Complete. |
| Complete wirft so eine Exception während das Original bereits eine eigene Exception propagiert | 1 | 1 / 0 | Die Formatierungsexception maskiert die Originalexception; gesunde Receipts werden nicht mehr abgeschlossen. |
| Kontrolle: gewöhnliche `InvalidOperationException` aus Complete | 1 | 1 / 1 | Unveränderter Rückgabewert 12, gesunder Beobachter abgeschlossen. Die vorhandene normale Isolation funktioniert. |

Der Reihenfolgebeleg nutzt die echte deterministische Registrierung: Im Beginfall kommt der defekte Beobachter zuerst. In den Completefällen wird der defekte Beobachter vor dem gesunden abgewickelt, weil Completion in umgekehrter Reihenfolge läuft. Der Harness ersetzt nicht den Dispatch oder dessen Fehlerbehandlung; er ruft `AivBuildStepService.DispatchForTest` der unten identifizierten echten Workspace-Assembly per Reflection auf. Die übergebene Originalaktion ist eine reine Managed-Zähler-/Exceptionaktion. Der native Detour wird weder vorbereitet noch aktiviert.

**Widerlegungsversuch:** Der gemeinsame `RuntimeHealthLedger.Report` enthält tatsächlich einen umfassenden eigenen Catch, und normale Beobachterexceptions werden durch die vorhandenen Tests bereits isoliert. Dieser Schutz beginnt jedoch erst nach Auswertung aller Methodenargumente. Er kann das vorherige virtuelle `Exception.ToString()` in `LogCallbackFailure` nicht schützen. Das konkrete Gegenbeispiel propagiert `FormatException("audit-format-failure")`, während die ursprüngliche `InvalidOperationException("audit-original-failure")` nicht mehr dieselbe propagierte Instanz ist. Der gewöhnliche Exception-Kontrollfall besteht.

### Reproduktionsstand und Nachweise

- Ausgeführte Assembly: **Workspace-Testabhängigkeit**, nicht installierte APIShared-Assembly:
  `APIShared/tests/APISharedTests/bin/Release/net481/APIShared.dll`.
- SHA-256: `B4261E8B5BC83375F58BC8D2C9459184490D07B93B02E757E4FE46B84997B8A5`.
- MVID: `fd095d42-1705-4791-a831-235b6ed0c83e`.
- Gegenübergestellte Quelldatei: `APIShared/src/Diagnostics/AivBuildStepCapability.cs`, SHA-256 `5299317D31D07243693AA75BBC1EC3C3A17A6D83D9C727ECDB031596B07CA347`.
- Reproduzierbarer isolierter Harness: `.work/a/rescue-audit/001/check-aiv-reporting.ps1`.
- Kompaktes Ergebnis mit absolutem Assemblypfad, SHA/MVID und allen vier Ausgängen: `.work/a/rescue-audit/001/aiv-reporting-counterexample.log`.
- Ausführung: frisches `powershell.exe -NoProfile -ExecutionPolicy Bypass -File <Harness>` aus dem Workspace-Root; Exitcode 0 bedeutet, dass alle drei Fehlergegenbeispiele und der Kontrollfall exakt den dokumentierten Ausgang haben, **nicht**, dass die Isolation fehlerfrei wäre.

Der entscheidende Beobachterfehler ist eine erlaubte Unterklasse von `System.Exception` mit:

    public override string ToString()
    {
        throw new FormatException("audit-format-failure");
    }

Die bestehende normale AIV-Offlinesuite bleibt ein gültiger Nachweis für ihre normalen Exceptionfälle. Sie prüft diesen speziellen zweiten Fehler während des Fehlermeldens nicht; ihr PASS widerspricht F1 nicht.

### Konsequenz für den allgemeinen Brokervertrag

Die geplante Forderung „Beobachterfehler verhindern/wiederholen Original nicht, Completionfehler maskieren Original nicht“ ist richtig, aber im heutigen AIV-Service nicht unter jeder fremden Fehlerdarstellung erfüllt. Für den neuen Broker müssen **auch Fehlerformatierung, Logging und weitere Cleanup-Schritte** innerhalb einer nicht werfenden Isolationsgrenze liegen. Vor einem Rückruf benötigte Verwaltungsdaten sollten so vorbereitet sein, dass der Cleanup nicht erst beim Abwickeln der Originalexception mit komplexer fremder Verarbeitung beginnen muss. Das ist eine Design-/Testanforderung; eine Runtimeänderung wurde hier ausdrücklich nicht vorgenommen.

F1 begründet keine globale Abschaltung funktionierender AIV-Beobachter und keinen neuen pauschalen Versions-/Hashguard. Ein normaler Callback oder eine gewöhnliche Exception wird dadurch nicht automatisch problematisch. Die spätere gezielte Korrektur und ihre Tests sind getrennt vom Audit zu beauftragen beziehungsweise umzusetzen.
