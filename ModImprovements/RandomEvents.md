# Random Events

Keine offenen Befunde. Die vier dokumentierten Probleme wurden nach erneuter
Prüfung gegen den aktuellen Code, die lokale Script-Extender-Dokumentation und
den relevanten Script-Extender-Sourcecode bestätigt und im Workspace behoben:

- Die Netzwerkmodus-Kontrolle verwendet genau einen gemeinsamen Snapshot im
  ersten echten Spieltick nach `OnStartMap(Post)`. Zu diesem Zeitpunkt sind die
  native Karte und `GameData` initialisiert; Multiplayerstatus, Lobby und echte
  Game-Member stehen nachweislich bereits vor dem Kartenladen fest.
- Die Modsettings verwenden `Shared/SerpLocalization.cs`; die parallele
  `RandomEventsLocalization` wurde entfernt.
- Der bekannte Signpost-Slot-Offset wird beim Referenz-Hash ausdrücklich gegen
  den aus dem validierten Code abgeleiteten Wert geprüft.
- Der unerreichbare `OnApplicationQuit`-Cleanup der beim Start zerstörten
  Plugin-Komponente wurde entfernt. Die dauerhaften Script-Extender-Abonnements
  bleiben wie vorgesehen für die Prozesslaufzeit aktiv.
