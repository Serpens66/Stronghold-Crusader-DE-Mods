# Befunde am Loader von SHCDE-SE 1.39

Gemessen am Dekompilat von SHCDE 2.80 und SHCDE-SE 1.39.0. Deckt die sieben
Punkte aus Issue #162 ab und ergänzt drei, die dort noch nicht stehen.

## 1. Vanilla baut drei Materialformen, und alle drei stehen im Klartext

`spriteLoader.cs:473-508` entscheidet beim Laden anhand eines
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

Drei Unterschiede, die der Atlas-Loader heute einebnet:

1. Foliage baut **ein** Material und legt sieben Verweise darauf ab, nicht
   sieben Materialien.
2. Foliage setzt **kein** `_SpriteCutoff`. Nur der TeamColour-Zweig staffelt
   den Fußabschnitt über j.
3. Plain hat gar keine Maske.

## 2. Welche Gruppen Foliage sind, steht ebenfalls im Spiel

`SpriteMapping.cs:301` nimmt vom Farb-Remap genau aus:

```csharp
(uint)(file - 29) > 2u && (uint)(file - 70) > 2u && file != 97
```

Das sind GM 29/30/31, 70/71/72 und 97, also `GM_TREE_BIRCH`, `_PINE`,
`_CHESTNUT`, `_OAK`, `_SHRUB1`, `_SHRUB2`, `_APPLE`. Genau diese sieben
rendert Vanilla über den Foliage-Zweig, mit Zehnfarben-Paletten je Baumart.
`GM_TREE_CACTII` (200) gehört ausdrücklich **nicht** dazu: plain shader, weiß,
Maske verboten (`spriteLoader.cs:369` sowie 639-667).

Der richtige Default ließe sich also aus der Spielwahrheit ableiten, statt ihn
zu raten. Die Datei `atlas_contract/loader_groups.json` in diesem Paket führt
die Zuordnung für alle 195 Gruppen und leitet die Foliage-Menge aus genau
dieser Bedingung ab, statt sie von Hand zu pflegen.

## 3. Einzelsprites laufen in denselben Fehler

`GameSpriteManagerAPI.cs:56-83` baut je ersetztem Sprite ein neues
`Unlit/TeamColour` mit der gelieferten `_m.png` als `_TeamMask`;
`ManagedHookManager.cs:1254-1290` setzt es nach jedem
`SpriteMapping.SetBodySprite` auf den Renderer. Bäume laufen durch
`SetBodySprite`, sind also immer betroffen.

Drei Nebenbefunde auf demselben Pfad:

- Der Hook setzt dort auch `_SpriteCutoff` nach `chopFeet`. Vanilla kennt für
  Foliage-Gruppen keinen Fußabschnitt, siehe der Code unter Punkt 1.
- Fehlt zu einem Einzelsprite die passende `<name>_m.png`, behält der Loader
  nicht die Vanilla-Maske, sondern legt einen transparenten 4x4-Ersatz an.
- `CreateReplacementSprite` (Zeilen 113 bis 119) übernimmt den
  **normalisierten** Pivot des Crusader-Slots, siehe [ANKER.md](ANKER.md).

## 4. Der Teilatlas kürzt das Array

`GameAtlasManagerAPI.ApplySingle` legt das neue Sprite-Array mit
`höchster Index in der atlas.json + 1` Slots an und kopiert nur die
Vanilla-Einträge, die in diese neue Länge passen. Ein Atlas mit den Frames 0
bis 72 macht aus einem 148 Slots breiten Array eines mit 73 Slots. Der Rest
ist weg, und das Spiel meldet ein fehlendes Sprite.

Unsere Umgehung: nie einen Teilatlas ausliefern. Der Bauer akzeptiert nur einen
vollständigen Zielnamensatz, festgenagelt über Framezahl, Haupt- und Alt-Zahl,
Maximalindex und einen SHA-256 über die loader-sortierten Namen. Slots ohne
eigene Vorlage gehen als Byte-Kopie des Crusader-Sprites mit.

Der Befehl `python -m atlas_contract fuellen` in diesem Paket schreibt genau
diese Kopien in den Vorlagenordner, benannt nach der Konvention des
AtlasBuilders.

Die Bilder kommen aus `Sprite.image`. Das ist richtig, weil der Loader jedes
ersetzte Sprite als `SpriteMeshType.FullRect` neu anlegt: was Vanilla wegen
seines engen Netzes nie gezeichnet hat, darf im Ersatz auch nicht auftauchen.

## 5. Zwei Gruppen dürfen als Atlas gar nicht überschrieben werden

```csharp
addGMFile(2167, "tile_sea_new_01", GM_NEW_SEA, ..., ID_Offset: 0,    additionalStorage: 816);
addGMFile( 816, "tile_sea_shore",  GM_NEW_SEA, ..., ID_Offset: 2167, additionalStorage: -1);
```

`addGMFile` legt `höchster Index + 1 + additionalStorage` Slots an, aber nur bei
`additionalStorage >= 0`, und schreibt jeden Frame nach `index + ID_Offset`.
`tile_sea_new_01` reserviert 816 Slots für einen Untermieter,
`tile_sea_shore` legt selbst nichts an und schreibt ab Index 2167 in diese
Slots hinein.

`ApplySingle` kennt beide Mechanismen nicht. Ein Atlas auf `tile_sea_shore`
misst Maximalindex 816, der Loader legt 817 Slots für ein 2.984 Felder breites
Array an, verwirft alles darüber und malt die Küste über die eigenen Frames von
`tile_sea_new_01`. Wir hatten das als zweitgrößten sicheren Gewinn eingeplant,
bis wir es nachgerechnet haben.

Die Regel ist kurz: eine Gruppe ist als Atlas nur dann sicher, wenn
`ID_Offset == 0 und additionalStorage == 0`. Genau zwei der 195 fallen durch.

## 6. 69 Gruppen werden ohne Maske geladen

Setzt `addGMFile` den Schalter `plainShader`, bekommt die Gruppe
`plainMaterials` und damit gar keine Maske. Wer denen eine `atlas_m.png`
mitgibt, bekommt das falsche Material. `tree_cactii` ist so ein Fall und sieht
auf den ersten Blick aus wie ein Baum.

## 7. Was wir uns im Extender wünschen würden

1. **Arraylänge nie schrumpfen:** `Math.Max(vanillaLength, maxIndex + 1)` statt
   `maxIndex + 1`. Das allein macht die dokumentierte Rückfallebene wahr und
   erledigt die Punkte 4, 5 und 6 aus Issue #162 auf einen Schlag.
2. **Ein `material`-Feld** mit "foliage", "teamcolour", "plain" in der
   `atlas.json`, und derselbe Schalter für Einzelsprite-Ersetzungen. Die drei
   Zweige aus `spriteLoader.cs:473-508` und `initPlainMaterial` lassen sich eins
   zu eins übernehmen, samt der Eigenheit, dass Foliage und Plain je ein
   Material siebenmal verweisen und kein `_SpriteCutoff` setzen.
3. **Als Default die Spielwahrheit:** GM 29-31, 70-72 und 97 bekommen
   `Unlit/Foliage`, GM 200 den plain shader.
4. **Eine Warnung**, wenn ein Atlas mit Maske auf einer dieser sieben GM-IDs
   registriert wird und kein Material gewählt wurde.
5. **Einzelsprite ohne `_m.png`:** die Vanilla-Maske behalten statt 4x4
   transparent.
6. Optional den Fußabschnitt für Foliage-Gruppen nicht setzen.

## Was wir uns gebaut haben, solange das so ist

Ein privates BepInEx-Begleitplugin hängt einen Harmony-Postfix an die
nichtöffentliche `SHCDESE.API.GameSpriteManagerAPI.ApplyRuntimeOverrides`,
läuft also nach der Atlas-Installation. Es liest per Reflexion nur
`spriteLoader.gmMaterials` und prüft vorher: erwartete Materialzahl im Slot,
alle nicht-null und `Unlit/TeamColour`, genau eine gemeinsame `_TeamMask`, alle
Sprites des Slots aus einer Textur, Haupttextur heißt `atlas`, Maske `atlas_m`,
beide noch lesbar. Erst dann entsteht ein eigenes `Unlit/Foliage`-Material mit
derselben Maske, das in alle sieben Slots geschrieben wird, wie Vanilla es tut.
Jede Abweichung führt zu gar keiner Änderung. Die Farbtabelle `gmColors` wird
nie angefasst. Beim Entladen wird nur zurückgesetzt, was noch unser eigenes
Material ist.

Weil diese Reparatur die gemeinsame Atlastextur braucht, kann sie Einzelsprites
grundsätzlich nicht erfassen. Deshalb liefern wir Foliage ausschließlich als
Atlas aus, nie als Einzelsprites.
