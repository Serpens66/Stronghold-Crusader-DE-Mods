# ReliableHunterProjectiles – archivierte, nicht mehr benötigte Funktion

## Status

`ReliableHunterProjectiles` wurde mit Improved Hunters `1.1.69` vollständig aus
dem produktiven Mod entfernt. Die Funktion wird nicht mehr benötigt und darf
nicht versehentlich zusammen mit der Wegfindung erneut aktiviert werden.

Ursprünglich sollte sie Jägerpfeile kompensieren, die an Gebäuden hängen
blieben, ihr Ziel zwar erreichten, aber keinen aufgelösten Treffer erzeugten,
oder ohne Treffer gelöscht wurden. Die verbesserte Weg- und Sichtbehandlung
soll den Jäger stattdessen zu einer gültigen Schussposition führen. Damit wäre
eine nachträgliche zweite Schadensanwendung sowohl redundant als auch riskant.

Die Funktion war zuletzt **nicht auf Hühner beschränkt**. Sie konnte für jede
durch ihren `Hunt...`-Schalter aktivierte Beuteart laufen: Reh, Ziege, Hase,
Kamel und Huhn. Die zusätzliche Hühnerbehandlung im Spawn-Handler war nur
Diagnose beziehungsweise Sichtkorrelation.

## Letzter produktiver Aufbau

Die entfernte Implementierung lag in
`src/HunterProjectileRecoveryFeature.cs` und war als Teilklasse von
`ImprovedHuntersRuntime` aufgebaut. Zugehörige Zustände und Konstanten lagen in
`src/ImprovedHuntersRuntime.cs`; der Host-Schalter lag in
`src/ImprovedHuntersViewModel.cs`.

Der Datenfluss war:

1. `ProjectileR3EventHooks.OnProjectileSpawn(Post)` erkannte lebende
   `ArcherArrow`-Projektile gegen eine aktivierte Tierart.
2. Der Jäger wurde über `SourceUnitId`, einen aktuell auf die Beute zielenden
   lebenden Jäger oder den Cache `activeHunterTargets` rekonstruiert.
3. Ein `PendingHunterShotIntent` speicherte Jäger- und Zielslot samt Global-ID,
   Tierart, Projektilslot und -Global-ID, letzte Position, Zeitgrenzen und
   Versuchsanzahl. `HunterShotIntentKey` band den Eintrag an Ziel- und
   Projektilidentität.
4. Der persistente 100-ms-Nativscan beobachtete die Projektilbewegung. Frühestens
   nach 250 ms wurde kompensiert, wenn der Pfeil höchstens 32 interne
   Distanzeinheiten vom Ziel entfernt oder seit mindestens 300 ms unbewegt war.
5. `ProjectileR3EventHooks.OnProjectileDelete(Pre)` war ein letzter
   Kompensationsauslöser, falls der Treffer bis zur Löschung nicht aufgelöst war.
6. Vor jedem Eingriff wurden Jäger, Ziel und Projektil erneut über Slot,
   Global-ID, Typ, Alive-State, Besitzer, Ziel-ID und Reservation validiert.
7. Die Kompensation rief
   `GameUnitManagerAPI.DamageUnitRanged(targetUnitId, projectileId)` auf. Sie
   verwendete damit Vanillas Fernkampfschadenspfad und erzeugte keinen
   synthetischen Kadaver per `KillUnit`.
8. Solange Ziel und Projektil gültig blieben, waren höchstens drei Versuche im
   Abstand von 100 ms erlaubt. Ein Intent lief nach fünf Sekunden aus.

Wichtige letzte Parameter:

- minimale Flugzeit: `Stopwatch.Frequency / 4` (250 ms)
- Stillstandsgrenze: `Stopwatch.Frequency * 3 / 10` (300 ms)
- Wiederholungsintervall: `Stopwatch.Frequency / 10` (100 ms)
- Intent-Lebensdauer: `Stopwatch.Frequency * 5` (5 s)
- Zielnähe: `32`
- maximale Schadensversuche: `3`

## Sicherheitsprüfungen der entfernten Version

Die Schadenswiederholung wurde verworfen, wenn unter anderem:

- Mod oder Tierart deaktiviert waren;
- Ziel, Jäger oder Projektil nicht mehr dieselbe Global-ID besaßen;
- das Ziel bereits tot oder kein erlaubtes lebendes Beutetier mehr war;
- der Jäger nicht lebte oder kein `CHIMP_TYPE_HUNTER` war;
- Projektiltyp, Quelljäger oder Ziel-ID nicht übereinstimmten;
- die Projektil-Spieler-ID nicht zum Jäger passte;
- Reservation nicht `0` oder `2` war.

Vor dem nativen Schadensaufruf wurde der Intent entfernt, damit eine synchron
ausgelöste Projektil-Löschung den gleichen Intent nicht erneut betreten konnte.
Fehler in Kompensation und Bereinigung waren getrennt gekapselt; Vanilla und der
übrige native Scan liefen weiter.

## Bewusst erhaltener Beobachtungsteil

Die Hooks für Projektil-Spawn und -Löschung bleiben in
`src/HunterProjectileObservationFeature.cs` ausschließlich beobachtend erhalten.
Die noch unfertige Nach-Schuss-Wegfindung benötigt diese Ereignisse zur
Korrelation ihrer eigenen Zustandsübergänge. Dieser Rest enthält:

- keine Pending-Shot-Intents;
- keine Stillstands- oder Zielnäheprüfung;
- keinen Schadensversuch;
- keinen Aufruf von `DamageUnitRanged` oder `KillUnit`;
- kein dazugehöriges Modsetting.

## Wiederherstellung, falls künftig doch erforderlich

Die vollständige letzte Implementierung bleibt über die Git-Historie vor
Version `1.1.69` rekonstruierbar. Maßgeblich sind die entfernte Datei
`src/HunterProjectileRecoveryFeature.cs`, die Strukturen
`HunterShotIntentKey`/`PendingHunterShotIntent` und die oben dokumentierten
Runtime-Konstanten. Vor einer Wiedereinführung müssen zuerst neue Laufzeittests
belegen, dass verbesserte Wegfindung und Sichtübergabe das Problem nicht lösen.
Eine Wiedereinführung muss wieder separat schaltbar, standardmäßig deaktiviert
und für jede Beuteart sowie Mehrjägersituationen abgenommen werden.
