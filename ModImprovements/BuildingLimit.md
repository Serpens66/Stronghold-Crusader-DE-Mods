# Building Limit

Keine offenen Befunde. Die auskommentierte lineare Scanimplementierung und ihre
Reste wurden entfernt; allein der aktive `ActiveBuildingCache` bleibt als
Zählpfad bestehen. Die Script-Extender-Initialisierung ist nun gegen den
Sofortaufruf später Abonnenten und das reguläre Event-Raise threadsicher auf
genau einen Aufruf begrenzt; Cache- und UI-Hooks bleiben bis Prozessende aktiv.
