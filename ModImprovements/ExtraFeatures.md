# Extra Features

Keine offenen Befunde. Die zuvor dokumentierten Netzwerkprobleme wurden erneut
gegen den aktuellen Modcode und die lokale Script-Extender-Quelle geprüft und
bestätigt. Die eigene Multiplayer-Paketlogik für Rittertransformationen und die
Steinbruchhalden-Versetzung wurde vollständig entfernt, einschließlich
MessagePack-Formatter, Paketregistrierung, Versand, Empfang und
Request-Deduplizierung. Beide Funktionen arbeiten vorerst ausschließlich lokal.

Gezielte TODO-Kommentare markieren die spätere Synchronisierung über den
geordneten Chore-Transport ab Script Extender 1.50.0. Der faktisch nicht
erreichbare Quit-Cleanup auf der beim Start zerstörten BepInEx-Komponente wurde
ebenfalls entfernt; die Runtime-Hooks bleiben absichtlich bis zum Prozessende
aktiv.
