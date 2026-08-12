# Building Costs

Keine offenen Befunde. Die ungenutzte Reflection-Methode und der faktisch tote
Quit-Cleanup wurden entfernt. Die Script-Extender-Initialisierung ist nun gegen
den Sofortaufruf später Abonnenten und das reguläre Event-Raise threadsicher auf
genau einen Aufruf begrenzt; die Runtime-Hooks bleiben bis zum Prozessende aktiv.
