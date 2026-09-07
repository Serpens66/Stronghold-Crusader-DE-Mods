# Offene UCP-Fixkandidaten: Platzierung und Animation

## `u_fix_lord_animation_stuck_movement`

UCP setzt nach Bewegung beziehungsweise Gebäudeangriff mehrere Lord-Animations-/Zustandsfelder zurück, damit der Lord nicht in einer Pose hängen bleibt. **DE-Status: möglicher Altfehler, niedrige Priorität; keine Abdeckung gefunden.** `OnUnitAIStateChange`, `OnUnitMovement` und die Unity-Visual-Spawn/Interpolate-Events können bestimmen, ob nur der Chimp-Animator oder auch der native Lordzustand hängt. Bei rein visueller Abweichung im Chimp-/Animator-Pfad korrigieren (`NetworkMode=0`); nur bei steckender Simulation den engen nativen Zustandsabschluss ändern (`NetworkMode=1`).

Die übergreifenden Baseline- und Eventregeln stehen in [UCP-native-integration-audit.md](UCP-native-integration-audit.md).

