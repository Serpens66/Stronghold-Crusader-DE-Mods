# Spätere gemeinsame Grafikzuständigkeit

APIShared wird in diesem Auftrag weder verändert noch gebaut. Der lokale Hook ist eine ausdrücklich erlaubte Testmod-Ausnahme.

Für eine spätere Integration eignet sich ein zentraler, prozessweit einmaliger Dienst für Zinnengrafiken: APIShared besitzt den nativen Gerade-Prädikat-Hook bei RVA 0x5C2D0 und veröffentlicht einen typisierten Vertrag, über den Verbraucher gewünschte niedrige gerade Endstücke registrieren. Die Baupolitik, Doppelreihensperre, Eigentümerauswahl und Versionsspeicherung bleiben beim jeweiligen Verbraucher.

Der gemeinsame Dienst sollte immutable, sitzungsgebundene Zuordnungen annehmen, konkurrierende Ansprüche pro Tile ausdrücklich erkennen und ohne Eigentümer-/Kartenübereinstimmung Vanilla verwenden. Er darf keine logischen Höhen im Renderpfad wählen oder schreiben. Positive Vanilla-Ergebnisse bleiben autoritativ; fremde, beschädigte oder strukturell geänderte Felder werden nicht überschrieben. Normale Lebenszykluswechsel leeren ausschließlich Daten, niemals den veröffentlichten Hook.

Der hier geprüfte Vertrag ist ausschließlich NativeX64 Absolute/15 am vollständigen 237-Byte-Prädikat, mit den zwei Grafik-Callern 0x65DD6 und 0x65E4F. Er ist vor einer Übernahme gegen dann aktuelle Native-/Extender-/Backend-Versionen und andere Grafikverbraucher neu zu prüfen. Bestehende Mod-Hooks dürfen nicht stillschweigend verkettet oder parallel installiert werden. Ein APIShared-Dienst braucht eine dokumentierte Ownership-/Migrationsstrategie, bevor der Testmod seinen lokalen Hook abgibt.

Es wird jetzt keine neue API veröffentlicht. Der Testmod bleibt bis zu einer ausdrücklich beauftragten gemeinsamen Integration eigener Hook-Eigentümer und verweigert eine bereits modifizierte Zielroutine.

Seit der Vorschaukorrektur besitzt der Testmod zusätzlich den Validator-Einstieg 0x7A3B0 (Absolute/18), angemeldet über das unveränderte öffentliche NativeSiteRegistry. Eine spätere gemeinsame Bauprüfung müsste Vanilla zuerst ausführen, zusätzliche Vetos ausschließlich verknüpfen und Vorschau von synchronisierter Ausführung ausdrücklich unterscheiden. Baupolitik und Autorisierung bleiben verbrauchereigen; lokale Vorschaucaches dürfen keine Multiplayer-Befehle freigeben. Dieser Dienst wäre unabhängig vom Grafikdienst und wird hier nur als spätere Integrationsmöglichkeit dokumentiert.
