Wir sind im Europa-Projekt auf alle sieben Punkte gestoßen und haben sie im
August gegen das Dekompilat festgenagelt. Hier unsere Belege und die Umgehung,
die wir uns gebaut haben.

## Vanilla baut drei Materialformen, und alle drei stehen im Klartext

`spriteLoader.cs:473-508` (SHCDE 2.8) entscheidet beim Laden anhand eines
`foliage`-Schalters:

```csharp
Material material = null;
if (foliage)
{
    material = new Material(Shader.Find("Unlit/Foliage"));
    material.SetTexture("_TeamMask", texture);
}
Material[] array2 = new Material[7];
for (int j = 0; j < 7; j++)
{
    if (!foliage)
    {
        Material material2 = new Material(Shader.Find("Unlit/TeamColour"));
        material2.SetTexture("_TeamMask", texture);
        if (j == 0)
            material2.SetFloat("_SpriteCutoff", (float)j / 2f / 10f);
        else
            material2.SetFloat("_SpriteCutoff", ((float)j + 4f) / 2f / 10f);
        array2[j] = material2;
    }
    else
    {
        array2[j] = material;          // sieben Verweise auf EIN Material
    }
}
```

Die dritte Form steht direkt darunter in `initPlainMaterial`: ein einziges
`Universal Render Pipeline/2D/Sprite-Unlit-Default`, ebenfalls siebenmal
verwiesen.

Drei Unterschiede, die der Atlas-Loader heute alle einebnet:

1. Foliage baut **ein** Material und legt sieben Verweise darauf ab, nicht
   sieben Materialien.
2. Foliage setzt **kein** `_SpriteCutoff`. Nur der TeamColour-Zweig staffelt
   den Fußabschnitt über j.
3. Plain hat gar keine Maske.

## Welche Gruppen Foliage sind, steht ebenfalls im Spiel

`SpriteMapping.cs:301` nimmt vom Farb-Remap genau aus:

```csharp
(uint)(file - 29) > 2u && (uint)(file - 70) > 2u && file != 97
```

Das sind GM 29/30/31, 70/71/72 und 97, also GM_TREE_BIRCH, _PINE, _CHESTNUT,
_OAK, _SHRUB1, _SHRUB2, _APPLE. Genau diese sieben rendert Vanilla über den
Foliage-Zweig oben, mit Zehnfarben-Paletten je Baumart. GM_TREE_CACTII (200)
gehört ausdrücklich NICHT dazu: plain shader, weiß, Maske verboten
(`spriteLoader.cs:369` sowie 639-667). Der richtige Default ließe sich also
aus der Spielwahrheit ableiten, statt ihn zu raten.

## Einzelsprites laufen in denselben Fehler

`GameSpriteManagerAPI.cs:56-83` baut je ersetztem Sprite ein neues
`Unlit/TeamColour` mit der gelieferten `_m.png` als `_TeamMask`;
`ManagedHookManager.cs:1254-1290` setzt es nach jedem
`SpriteMapping.SetBodySprite` auf den Renderer. Bäume laufen durch
`SetBodySprite`, sind also immer betroffen.

Drei Nebenbefunde auf demselben Pfad:

- Der Hook setzt dort auch `_SpriteCutoff` nach `chopFeet`. Vanilla kennt für
  Foliage-Gruppen keinen Fußabschnitt, siehe der Code oben.
- Fehlt zu einem Einzelsprite die passende `<name>_m.png`, behält der Loader
  nicht die Vanilla-Maske, sondern legt einen transparenten 4x4-Ersatz an.
- `CreateReplacementSprite` (Zeilen 113-119) übernimmt den *normalisierten*
  Pivot des Crusader-Slots. Ein Frame auf einer anderen Leinwand landet damit
  bei (pivot_x * eigene Breite, pivot_y * eigene Höhe) und verrutscht lautlos
  auf seiner Kachel. Eine Größenprüfung gibt es auf diesem Weg gar nicht.
  `LoadTexture` premultipliziert außerdem Alpha, geliefert werden muss also
  straight alpha.

## Unsere Umgehung

**Gegen den Teilatlas: nie einen ausliefern.** Der Loader legt
`höchster Index + 1` Slots an und kopiert nur die Vanilla-Einträge, die in die
neue Länge passen. Unser Builder akzeptiert deshalb ausschließlich einen
vollständigen Zielnamensatz, festgenagelt über Framezahl, Haupt- und
Alt-Zahl, Maximalindex und einen SHA-256 über die loader-sortierten Namen;
doppelte (index, isAlt) schlagen fehl. Slots ohne europäischen Spender gehen
als Byte-Kopie des Crusader-Sprites mit, auf Zielleinwand, Zielpivot und
Ziel-PPU, mit eigenem Herkunftsnachweis statt eines Konvertierungsbeweises.

Konkret bei der Birke: 148 Frames insgesamt, 131 konvertiert, 17 als
Crusader-Kopie. tree_apple liefert 101 von 101. So bleibt die Arraylänge
erhalten und es gibt kein Missing Sprite.

**Gegen den Shader: ein Begleitplugin.** Ein privates BepInEx-5-Plugin hängt
einen Harmony-Postfix an die nichtöffentliche
`SHCDESE.API.GameSpriteManagerAPI.ApplyRuntimeOverrides(spriteLoader)`, läuft
also nach der Atlas-Installation. Es liest per Reflexion nur
`spriteLoader.gmMaterials` und prüft vorher: erwartete Materialzahl im Slot,
alle nicht-null und `Unlit/TeamColour`, genau eine gemeinsame `_TeamMask`,
alle Sprites des Slots aus einer Textur, Haupttextur heißt `atlas`, Maske
`atlas_m`, beide noch lesbar. Erst dann entsteht ein eigenes
`Unlit/Foliage`-Material mit derselben Maske, das in alle sieben Slots
geschrieben wird, wie Vanilla es tut. Jede Abweichung führt zu gar keiner
Änderung. Die Farbtabelle `gmColors` wird nie angefasst. Beim Entladen wird
nur zurückgesetzt, was noch unser eigenes Material ist.

Weil diese Reparatur die gemeinsame Atlastextur braucht, kann sie
Einzelsprites grundsätzlich nicht erfassen. Deshalb liefern wir Foliage
ausschließlich als Atlas aus, nie als Einzelsprites. Unser fertiger
Birken-Neuzeichner blieb genau daran hängen und ist bis heute Entwurf.

**Gegen die fehlende Warnung: ein eigenes Gatter.** Unser Profil muss für jede
Gruppe, die Vanilla mit `Unlit/Foliage` zeichnet, ausdrücklich
`acknowledge_loader_139_foliage_material_mismatch: true` setzen, sonst bricht
der Bau ab. Das ist ein Pflaster auf unserer Seite, gehört aber eigentlich in
den Extender.

## Zur Birke: ein Befund, der über den Shader hinausgeht

Zwei Dinge, die dich beim SH1-Übertrag noch treffen werden.

Erstens: Der Crusader-Inhalt des `tree_birch`-Slots sind Dattelpalmen. Die
Frames 73 bis 147 zeigen Palmen, keine Birken.

Zweitens, und schwerwiegender: Namensgleichheit ist nur ein Index. Zwei Spiele
können dieselbe Gruppe anders ordnen, dann passt jeder Frame formal und keiner
inhaltlich. Genau das ist hier der Fall. Aus unserem Mapping:

```text
Idx | Crusader (Ziel)  | Stronghold 1 (Quelle)
 55 | 150x180 stehend  | 217x150 liegend
 60 | 149x180 stehend  | 216x127 liegend
 66 | 147x178 stehend  | 162x108 liegend
 70 | 153x184 stehend  | 164x98  liegend
 72 | 148x184 stehend  |  58x31  liegend
```

Crusader führt dort eine stehende Schwing-Schleife um 150x180, Stronghold 1
eine Fällsequenz: der Baum kippt, liegt, wird zum Stumpf. 13 Frames sind
betroffen, 55-60, 65-70 und 72. Im Spiel liegt dort ein umgestürzter Baum, wo
einer stehen und schwanken sollte. Eine reine Längenkorrektur behebt das nicht.

## Was wir uns im Extender wünschen würden

1. Arraylänge nie schrumpfen: `Math.Max(vanillaLength, maxIndex + 1)` statt
   `maxIndex + 1`. Das allein macht die dokumentierte Rückfallebene wahr und
   erledigt die Punkte 4, 5 und 6 auf einen Schlag.
2. Ein `material`-Feld mit "foliage", "teamcolour", "plain" in der `atlas.json`,
   und derselbe Schalter für Einzelsprite-Ersetzungen. Die drei Zweige aus
   `spriteLoader.cs:473-508` und `initPlainMaterial` lassen sich eins zu eins
   übernehmen, samt der Eigenheit, dass Foliage und Plain je ein Material
   siebenmal verweisen und kein `_SpriteCutoff` setzen.
3. Als Default die Spielwahrheit: GM 29-31, 70-72 und 97 bekommen
   `Unlit/Foliage`, GM 200 den plain shader.
4. Eine Warnung, wenn ein Atlas mit Maske auf einer dieser sieben GM-IDs
   registriert wird und kein Material gewählt wurde.
5. Einzelsprite ohne `_m.png`: die Vanilla-Maske behalten statt 4x4 transparent.

Alle Zeilenangaben beziehen sich auf das Dekompilat von SHCDE-SE 1.39.0 gegen
SHCDE 2.7 beziehungsweise 2.8.
