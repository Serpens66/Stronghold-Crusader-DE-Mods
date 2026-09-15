# AIAttackTest – mögliche Folgearbeit

## UCP-Zielrotation vor dem Durchbruch

UCPs `ai_attackwave` enthält neben dem hier umgesetzten Lord-Angriff einen separaten Eingriff in die Zielverteilung vor einem Durchbruch. Mit dem UCP-Standardwert 7 werden nacheinander vier Angriffsgruppen auf Mauerziele, zwei auf Befestigungs- beziehungsweise Turmziele und eine auf normale Gebäude- beziehungsweise Wirtschaftsziele angesetzt. Danach beginnt die 4/2/1-Folge erneut. UCP überspringt dabei außerdem Prüfungen für bereits belegte oder bereits gebrochene Mauerteile.

Diese Rotation ist in AIAttackTest 0.1.0 absichtlich nicht enthalten. Stronghold Crusader DE besitzt mit „Improved Siege“ und „Aggressive Siege“ bereits eigene Zielroutinen und AIC-Schalter. Ein direkter HD-Patch würde deren Verhalten möglicherweise ersetzen oder widersprüchlich überlagern. Vor einem DE-Port muss deshalb der vollständige State-5-Daten- und Kontrollfluss einschließlich normaler und verbesserter Befestigungszielsuche, Abbruchpfade, Zielzustände und Mehrspieler-Synchronität erneut featurebezogen auditiert werden.

## Abgrenzung der vorhandenen Änderungen

- AIAttackTest 0.1.0 ändert nach einem Durchbruch nur die Vanilla-Begrenzung, die ansonsten lediglich dem ersten geeigneten Tribe einen Lord-Angriff erlaubt.
- `BugfixesAndQoL/src/AiWallTargetingFix.cs` entfernt unabhängig davon die Reservierungssperre, damit mehrere einzelne Angreifer dasselbe gültige Mauersegment wählen können.
- Die spätere 4/2/1-Rotation wäre ein drittes, strategisches Feature und darf weder mit dem Lord-Patch noch mit dem Wall-Targeting-Fix gleichgesetzt oder ungeprüft an deren RVAs angehängt werden.
