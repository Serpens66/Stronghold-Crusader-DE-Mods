# Sichere und erweiterbare Save-State-Arraygrenzen

## Zweck

MessagePack-Arrayheader enthalten die behauptete Elementzahl. Ein beschädigter oder manipulierter Save kann darin beispielsweise `int.MaxValue` deklarieren, obwohl fast keine Nutzdaten folgen. Wird zuerst `new T[length]` ausgeführt und erst danach validiert, kann bereits die Allokation den Prozess stark belasten oder beenden.

Für Mod-Save-Daten gilt deshalb zwingend:

1. Arrayheader lesen.
2. Länge gegen den aktuellen Limit-Snapshot prüfen.
3. Erst danach das Array anlegen und seine Elemente lesen.
4. Fachliche Werte und Beziehungen zusätzlich fail-closed validieren.

Eine Begrenzung der gesamten Payloadgröße ersetzt diese Prüfungen nicht.

## Aktuelle Limit-Policies

### Gatehouse Automation

Implementierung:

- `ExtraFeatures/src/GatehouseAutomationSaveState.cs`
- `GatehouseAutomationSaveLimitPolicy`
- `GatehouseAutomationSaveLimits`

Standardgrenze:

- `MaximumSavedGatehouses`: 10.000

Die Grenze schützt sowohl `ManualOnlyGateGlobalIds` als auch `ManualOnlyGateLocators`.

### Plague Popularity

Implementierung:

- `BugfixesAndQoL/src/PlaguePopularitySaveState.cs`
- `PlaguePopularitySaveLimitPolicy`
- `PlaguePopularitySaveLimits`

Standardgrenzen:

| Eigenschaft | Standard | Bedeutung |
|---|---:|---|
| `MaximumManagedPlayers` | 8 | Maximale Anzahl und höchste unterstützte ID verwalteter Spieler |
| `MaximumHerds` | 4.096 | Maximale Zahl gespeicherter Seuchenherden |
| `MaximumProjectilesPerHerd` | 10 | Maximale Projektile in einer einzelnen Herde |
| `MaximumTotalProjectiles` | 10.000 | Maximale Projektile über alle Herden hinweg |
| `MaximumProjectileSlotId` | 10.000 | Höchste zulässige native Projectile-Slot-ID |

Die Gesamtgrenze wird während der Deserialisierung fortlaufend berücksichtigt. Schon der Slot-Arrayheader einer weiteren Herde wird gegen die noch verfügbare Gesamtkapazität geprüft, bevor das Array angelegt wird.

## Grenze für ein neues Feature erhöhen

Limit-Provider werden unter einer stabilen, weltweit eindeutigen Feature-ID registriert. Der Rückgabewert beschreibt immer die insgesamt benötigte Kapazität, nicht nur den Zuschlag des Features.

### Gatehouse-Beispiel

```csharp
private static IDisposable gatehouseSaveLimitRegistration;

private static void RegisterGatehouseSaveLimits()
{
    gatehouseSaveLimitRegistration =
        GatehouseAutomationSaveLimitPolicy.Register(
            "author.mod-id.expanded-gatehouses",
            () => new GatehouseAutomationSaveLimits(
                maximumSavedGatehouses: 12500));
}
```

Benötigt das Feature insgesamt 12.500 Einträge, muss es `12500` melden. `2500` würde wegen der Standardgrenze von 10.000 keine Erhöhung bewirken.

### Plague-Beispiel

```csharp
private static IDisposable plagueSaveLimitRegistration;

private static void RegisterPlagueSaveLimits()
{
    plagueSaveLimitRegistration =
        PlaguePopularitySaveLimitPolicy.Register(
            "author.mod-id.expanded-plague",
            () => new PlaguePopularitySaveLimits(
                maximumManagedPlayers: 8,
                maximumHerds: 6000,
                maximumProjectilesPerHerd: 20,
                maximumTotalProjectiles: 20000,
                maximumProjectileSlotId: 20000));
}
```

Nur fachlich tatsächlich erweiterte Werte müssen größer gewählt werden. Die Policy behält für jede Eigenschaft mindestens den Standardwert.

## Zusammenführung mehrerer Provider

Alle registrierten Provider werden bei jedem Speichern und Laden neu ausgewertet. Je Eigenschaft gewinnt der höchste gemeldete Gesamtwert. Kein Provider kann die Standardgrenze oder die höhere Meldung eines anderen Providers absenken.

Die Werte werden nicht addiert. Wenn mehrere Features gemeinsam additiv mehr Einträge erzeugen können, muss mindestens ein koordinierender Provider die mögliche Gesamtzahl melden. Beispiel:

- Standard: 10.000
- Feature A kann 1.000 zusätzliche Einträge erzeugen.
- Feature B kann 2.000 zusätzliche Einträge erzeugen.
- Sind beide gleichzeitig möglich, muss die gemeldete Gesamtgrenze 13.000 und nicht 11.000 beziehungsweise 12.000 betragen.

Bei einer dauerhaften Erweiterung des eigentlichen Mods kann alternativ der sichere Standardwert in der zugehörigen Policy bewusst angepasst werden. Ein Provider ist besonders sinnvoll für optionale, konfigurationsabhängige oder von einem anderen Mod bereitgestellte Erweiterungen.

## Dynamische Provider

Ein Provider darf seine Grenze zur Laufzeit berechnen:

```csharp
() => new GatehouseAutomationSaveLimits(
    maximumSavedGatehouses: expandedFeatureEnabled ? 12500 : 10000)
```

Dabei gelten folgende Regeln:

- Der Wert darf nicht aus den gerade deserialisierten, untrusted Save-Daten abgeleitet werden.
- Die Berechnung muss deterministisch, schnell und nebenwirkungsfrei sein.
- Eine Grenze darf nicht abgesenkt werden, solange vorhandene Saves noch größere gültige Zustände enthalten können.
- Jeder einzelne Serialisierungs- oder Deserialisierungsvorgang verwendet einen konsistenten Snapshot.
- Unrealistisch große Werte wie `int.MaxValue` umgehen den eigentlichen Speicherschutz und sind unzulässig.

## Registrierung und Lifecycle

Die Registrierung muss vor dem ersten möglichen Speichern oder Laden abgeschlossen sein. Das zurückgegebene `IDisposable` muss für die gesamte benötigte Prozesslaufzeit stark referenziert bleiben, vorzugsweise in einem statischen Feld.

Bei SHCDE werden `OnDisable()` und `OnDestroy()` der `BaseUnityPlugin`-Instanz bereits während des Spielstarts aufgerufen. Eine weiterhin benötigte Limit-Registrierung darf dort nicht entsorgt werden.

`Dispose()` ist nur angebracht, wenn das Feature tatsächlich dauerhaft deaktiviert oder entladen wird. Danach können Saves oberhalb der verbleibenden Grenzen nicht mehr geladen werden.

## Abhängigkeiten zwischen Mods

Ein direkter Aufruf der öffentlichen Policy-Typen erzeugt eine Assemblyabhängigkeit zum jeweiligen Zielmod.

- Ist die Erweiterung ohne den Zielmod bedeutungslos, die Abhängigkeit ausdrücklich als harte BepInEx-Abhängigkeit deklarieren.
- Ist die Zusammenarbeit optional, eine Soft-Dependency verwenden und die Registrierung nur nach sicherer Erkennung des Zielmods durchführen. Keine andere Workspace-Mod stillschweigend voraussetzen.
- Die Feature-ID muss stabil und eindeutig sein, beispielsweise `author.mod-id.feature-name`.
- Eine doppelte Registrierung derselben ID wird mit einer aussagekräftigen Exception abgelehnt.

## Schemaänderungen sind keine Limiterhöhung

Die Anzahl der Felder eines MessagePack-Records ist versionsgebunden und wird exakt geprüft. Ein Limit-Provider erlaubt keine zusätzlichen Felder.

Wenn ein Save-State neue Felder erhält:

1. Schema-Version erhöhen.
2. Formatter ausdrücklich für die neue Feldzahl erweitern.
3. Bereits veröffentlichte alte Versionen weiterhin gezielt lesen.
4. Unbekannte Kombinationen aus Version und Feldzahl fail-closed ablehnen.
5. Für jedes neue Array eine eigene fachliche und gegebenenfalls registrierbare Grenze definieren.

## Neues Save-State-Array hinzufügen

Bei einem neuen Array sind mindestens folgende Punkte umzusetzen:

1. Fachlich begründeten sicheren Standard bestimmen.
2. Wenn andere Features die Kapazität verändern können, die Grenze in einer zugänglichen Policy abbilden.
3. Beim Serialisieren dieselben Grenzen prüfen, damit der Mod keine später unlesbaren Saves erzeugt.
4. Beim Deserialisieren unmittelbar nach `ReadArrayHeader()` und vor `new[]` prüfen.
5. Bei verschachtelten Arrays zusätzlich eine Gesamtgrenze führen und die verbleibende Kapazität vor jeder Teilallokation prüfen.
6. Parallele Arrays bereits am zweiten Header auf gleiche Länge prüfen.
7. Nullwerte, Wertebereiche, IDs, Duplikate und Beziehungen nach dem strukturellen Lesen validieren.
8. Einen Limit-Snapshot pro Root-Vorgang verwenden; nicht für jeden Datensatz einen möglicherweise abweichenden Providerwert abrufen.

Sicheres Grundmuster:

```csharp
int length = reader.ReadArrayHeader();
if (length > limits.MaximumEntries)
{
    throw new MessagePackSerializationException(
        $"Feature entry-array length {length} exceeds {limits.MaximumEntries}.");
}

Entry[] values = new Entry[length];
```

Unsicheres Muster:

```csharp
int length = reader.ReadArrayHeader();
Entry[] values = new Entry[length];

if (values.Length > limits.MaximumEntries)
    throw new InvalidOperationException("Too many entries.");
```

## Fehlerverhalten und Logs

Limit-, Schema- und Inhaltsverletzungen werden mit konkreten `MessagePackSerializationException`-Meldungen abgelehnt, beispielsweise:

- `locator-array length 12501 exceeds 10000`
- `herd array length ... exceeds ...`
- `projectile-slot array length ... exceeds ...`
- `projectile identity arrays have different lengths`
- `contains a duplicate projectile identity`
- `unsupported version or field count`

Plague Popularity fängt den Fehler modseitig ab, leert seinen abgeleiteten Zustand und protokolliert mit Millisekunden-Zeitstempel, dass der Save für dieses Feature abgelehnt wurde und Vanilla-Verhalten erhalten bleibt.

Gatehouse Automation lässt die Exception an `ModSaveDataAPI` weiterlaufen. Der Script Extender fängt jeden Handler getrennt ab und protokolliert:

```text
Error loading data for mod [serp-extrafeatures-gatehouse-automation-v1]
```

Die innere Exception enthält den konkreten Grenzwert- oder Schemafehler. Andere registrierte Mod-Save-Handler werden anschließend weiter verarbeitet.

## Verbindliche Tests

Für jeden neuen oder geänderten Formatter mindestens prüfen:

- gültige Serialisierungs-/Deserialisierungs-Rundreise;
- bestehende veröffentlichte Schema-Versionen;
- Länge 0, Maximum und Maximum+1;
- `int.MaxValue` im Arrayheader ohne entsprechende Nutzdaten;
- falsche Root- und Record-Feldzahlen;
- Null-Records und Null-Arrays gemäß Schema;
- trunkierte Payloads;
- parallele Arrays unterschiedlicher Länge;
- ungültige IDs und Duplikate;
- Überschreitung einer verschachtelten Gesamtgrenze;
- Erhöhung und anschließende Entfernung eines registrierten Providers;
- Nachweis, dass vor Ablehnung keine große Arrayallokation erfolgt.

Die aktuellen Regressionstests befinden sich in `_inspect/HostClientPresetTests` unter `TestBoundedSaveStateDeserialization()`.

## README-Abgrenzung

Diese Policies sind interne Entwickler- und Sicherheitsmechanismen. Sie gehören nur dann in eine Spieler-README, wenn daraus eine unmittelbar sichtbare Funktion, Einstellung oder Verhaltensänderung für Spieler entsteht.
