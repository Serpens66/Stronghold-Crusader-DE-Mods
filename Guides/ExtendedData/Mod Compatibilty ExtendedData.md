# ExtendedData compatibility for mod authors / ExtendedData-Kompatibilität für Modentwickler

[English](#english) | [Deutsch](#deutsch)

## English

ExtendedData can store a compatible mod's persistent host settings in Maps, Custom Trails, and Coop Trail packages. Target mods depend only on APIShared; they must not reference `ExtendedData.dll` or vendor Shared source files.

For normal shareable presets, including JSON and asset-mod packaging, see [Extensible ModSettings Presets](ModSettings%20Presets.md#english). Map and Trail creators should also read [Mod settings in Maps and Custom Trails](Custom%20Trail%20Mod%20Settings.md#english).

### Requirements

1. Add a hard BepInEx dependency on `APIShared_Serp` version `0.4.0` or newer.
2. Reference the installed `APIShared.dll` with `Private=false`.
3. Derive the registered lobby-settings ViewModel from `Shared.PresetLobbyModSettingsViewModel`.
4. Every persistent setter calls `CanMutateSetting()` before changing state and `OnPropertyChanged()` afterwards.
5. Register exactly one lobby-settings ViewModel for the plugin GUID through `LobbyModSettingsPresetRegistration.Register`.
6. Expose at least one public readable/writable persistent `[SyncHostOnly]` property without `[DoNotPersist]`. Its current value must be non-null and MessagePack-serializable.

ExtendedData also checks that APIShared's disabled mission snapshot contains a deserializable value for every eligible property. If `EnableMod` is among those properties, it must be Boolean; APIShared sets it to `false` in that snapshot. Deriving from `PresetLobbyModSettingsViewModel` supplies the typed endpoint and snapshot implementation.

No Shared source links, compile symbols, or ExtendedData reference are required.

```xml
<PropertyGroup Condition="'$(ApiSharedDir)' == ''">
  <ApiSharedDir>$(GameDir)\BepInEx\plugins\APIShared_Serp</ApiSharedDir>
</PropertyGroup>
<ItemGroup>
  <Reference Include="APIShared">
    <HintPath>$(ApiSharedDir)\APIShared.dll</HintPath>
    <Private>false</Private>
  </Reference>
</ItemGroup>
```

Create the ViewModel once in the plugin, then register it from the plugin's existing `CrusaderLibrary.LibraryLoaded` callback after the library is ready. This is the registration call, not a complete plugin class:

```csharp
LobbyModSettingsPresetRegistration.Register(
    this, Logger, Info.Metadata.GUID, Settings,
    "ScriptExtenderUI/ExampleSettings.xaml");
```

The plugin declares `[BepInDependency("APIShared_Serp", "0.4.0")]`. Its `Settings` instance can use this minimal ViewModel shape:

```csharp
using SHCDESE.API.Components.Network;
using Shared;

public sealed class ExampleSettings : PresetLobbyModSettingsViewModel
{
    private bool enableMod = true;
    private int strength = 100;

    [SyncHostOnly]
    public bool EnableMod
    {
        get => enableMod;
        set
        {
            if (!CanMutateSetting() || enableMod == value) return;
            enableMod = value;
            OnPropertyChanged(nameof(EnableMod));
        }
    }

    [SyncHostOnly]
    public int Strength
    {
        get => strength;
        set
        {
            if (!CanMutateSetting() || strength == value) return;
            strength = value;
            OnPropertyChanged(nameof(Strength));
        }
    }
}
```

Use this namespace in the settings XAML because the shared controls now live in APIShared:

```xml
xmlns:shared="clr-namespace:Shared;assembly=APIShared"
```

The minimal preset row binds `System_PresetLoadText`, `System_OpenPresetLoadCommand`, `System_PresetSaveText`, and `System_OpenPresetSaveCommand`. Copy the complete standard Load/Save and settings-source block from [Extensible ModSettings Presets](ModSettings%20Presets.md#target-mod-integration). APIShared supplies **Mod defaults** by itself; an optional typed provider such as ExtendedData adds Trail and Map sources.

### ExtendedData contract

`PresetLobbyModSettingsViewModel` implements the public typed `IModSettingsPresetEndpoint` and its working-copy extension `IModSettingsWorkingCopyEndpoint`. ExtendedData accepts the typed contract; the former reflection-by-member-name contract is no longer supported. Mods deriving from the public base class need no additional implementation.

Only public readable/writable `[SyncHostOnly]` properties without `[DoNotPersist]` enter Map/Trail documents. `[SyncPerPlayer]`, `[PresetLocal]`, `[PersistLocal]`, and transient values remain player-owned. A Boolean host property named `EnableMod` is set to `false` in the safe disabled mission snapshot.

Map and Trail settings remain in schema-3 documents, including optional settings sidecars in Coop packages. Selecting a Custom Trail first opens a read-only preview; mods absent from its sidecar retain their normal values during selection. Starting it directly applies the Trail rules in a read-only mission context, including the safe baseline for unmentioned mods and settings. Customize and Trail Maker use an editable temporary working copy; normal presets and available **Mod defaults**, **Trail settings**, and **Map settings** sources can be loaded without changing source files, and the previous normal working state is restored on exit. A Trail initially uses only its Trail document even when its Map also contains settings.

### Explicit opt-out

To hide a registered plugin from ExtendedData, declare this exact public constant on its BepInEx plugin class:

```csharp
public const bool ExtendedDataModSettingsOptOut = true;
```

### Verification

- The mod appears under ExtendedData's compatible mods. If rejected, the log entry beginning `Map/Trail mod settings [` names the mod and gives the incompatibility reason.
- **Mod default**, **Player/host**, and **Fixed value** behave as documented.
- Personal/local/transient properties never enter Map/Trail JSON.
- Map and Trail activation does not replace normal preset storage.
- Host authority and client read-only behavior work in multiplayer.
- The consumer project compiles using APIShared only.

Search `BepInEx/LogOutput.log` for `Map/Trail mod settings` when compatibility is rejected.

---

## Deutsch

ExtendedData kann die persistenten Host-Einstellungen eines kompatiblen Mods in Maps, Custom Trails und Koop-Trail-Paketen speichern. Ziel-Mods hängen nur von APIShared ab; sie dürfen weder `ExtendedData.dll` referenzieren noch Shared-Quelldateien einbinden.

Für normale teilbare Presets einschließlich JSON- und Asset-Mod-Struktur siehe [Erweiterbare ModSettings-Presets](ModSettings%20Presets.md#deutsch). Map- und Trail-Ersteller sollten zusätzlich [Mod-Einstellungen in Maps und Custom Trails](Custom%20Trail%20Mod%20Settings.md#deutsch) lesen.

### Voraussetzungen

1. Füge eine harte BepInEx-Abhängigkeit auf `APIShared_Serp` ab Version `0.4.0` hinzu.
2. Referenziere die installierte `APIShared.dll` mit `Private=false`.
3. Leite das registrierte Lobby-Settings-ViewModel von `Shared.PresetLobbyModSettingsViewModel` ab.
4. Jeder persistente Setter ruft vor der Änderung `CanMutateSetting()` und danach `OnPropertyChanged()` auf.
5. Registriere über `LobbyModSettingsPresetRegistration.Register` genau ein Lobby-Settings-ViewModel für die Plugin-GUID.
6. Stelle mindestens eine öffentliche les- und schreibbare persistente `[SyncHostOnly]`-Property ohne `[DoNotPersist]` bereit. Ihr aktueller Wert darf nicht null sein und muss sich mit MessagePack serialisieren lassen.

ExtendedData prüft außerdem, ob APIShareds deaktivierter Missionssnapshot für jede geeignete Property einen deserialisierbaren Wert enthält. Falls `EnableMod` zu diesen Properties gehört, muss die Property boolesch sein; APIShared setzt sie in diesem Snapshot auf `false`. Die Ableitung von `PresetLobbyModSettingsViewModel` stellt den typisierten Endpunkt und die Snapshot-Implementierung bereit.

Shared-Quelllinks, Compile-Symbole und eine ExtendedData-Referenz sind nicht erforderlich. Das Projektbeispiel im englischen Abschnitt gilt unverändert. Lege das ViewModel im Plugin einmalig an und registriere es im bestehenden `CrusaderLibrary.LibraryLoaded`-Callback nach dem Laden der Bibliothek. Der englische C#-Ausschnitt zeigt den Registrierungsaufruf und die minimale ViewModel-Form; er ist keine vollständige Plugin-Klasse.

Im XAML muss der gemeinsame Namespace auf APIShared zeigen:

```xml
xmlns:shared="clr-namespace:Shared;assembly=APIShared"
```

Die minimale Preset-Zeile bindet `System_PresetLoadText`, `System_OpenPresetLoadCommand`, `System_PresetSaveText` und `System_OpenPresetSaveCommand`. Den vollständigen Standardblock für Laden/Speichern sowie **Einstellungen zurücksetzen auf** findest du in [Erweiterbare ModSettings-Presets](ModSettings%20Presets.md#integration-des-ziel-mods). APIShared stellt **Mod-Standards** selbst bereit; ein optionaler typisierter Provider wie ExtendedData ergänzt Trail- und Map-Quellen.

### ExtendedData-Vertrag

`PresetLobbyModSettingsViewModel` implementiert die öffentliche typisierte Schnittstelle `IModSettingsPresetEndpoint` sowie deren Arbeitskopie-Erweiterung `IModSettingsWorkingCopyEndpoint`. ExtendedData akzeptiert den typisierten Vertrag; der frühere Reflection-Vertrag über Membernamen wird nicht mehr unterstützt. Von der öffentlichen Basisklasse abgeleitete Mods benötigen keine zusätzliche Implementierung.

Nur öffentliche les- und schreibbare `[SyncHostOnly]`-Properties ohne `[DoNotPersist]` gelangen in Map-/Trail-Dokumente. `[SyncPerPlayer]`, `[PresetLocal]`, `[PersistLocal]` und transiente Werte bleiben im Besitz des Spielers. Eine boolesche Host-Property namens `EnableMod` wird im sicheren deaktivierten Missionssnapshot auf `false` gesetzt.

Map- und Trail-Einstellungen bleiben in Schema-3-Dokumenten gespeichert, einschließlich optionaler Settings-Sidecars in Koop-Paketen. Die Auswahl eines Custom Trails öffnet zunächst eine schreibgeschützte Vorschau; Mods ohne Eintrag in dessen Sidecar behalten während der Auswahl ihre normalen Werte. Beim direkten Start gelten die Trail-Regeln in einem schreibgeschützten Missionskontext, einschließlich der sicheren Ausgangslage für nicht genannte Mods und Einstellungen. Customize und Trail Maker verwenden eine bearbeitbare temporäre Arbeitskopie; normale Presets sowie verfügbare Quellen **Mod-Standards**, **Trail-Einstellungen** und **Map-Einstellungen** können geladen werden, ohne ihre Quelldateien zu verändern. Beim Verlassen wird der vorherige normale Arbeitsstand wiederhergestellt. Ein Trail verwendet anfangs ausschließlich sein Trail-Dokument, auch wenn seine Map ebenfalls Einstellungen enthält.

### Explizites Opt-out

Um ein registriertes Plugin in ExtendedData auszublenden, deklariere diese exakte öffentliche Konstante in seiner BepInEx-Plugin-Klasse:

```csharp
public const bool ExtendedDataModSettingsOptOut = true;
```

### Prüfung

- Der Mod erscheint unter den kompatiblen ExtendedData-Mods. Bei einer Ablehnung nennt der mit `Map/Trail mod settings [` beginnende Logeintrag den Mod und den Grund.
- **Mod-Standard**, **Spieler/Host** und **Fester Wert** funktionieren wie dokumentiert.
- Persönliche, lokale und transiente Properties gelangen nie in Map-/Trail-JSON.
- Die Aktivierung einer Map oder eines Trails ersetzt nicht den normalen Presetspeicher.
- Hostautorität und Client-Schreibschutz funktionieren im Multiplayer.
- Das Consumer-Projekt kompiliert ausschließlich gegen APIShared.

Suche bei einer Ablehnung in `BepInEx/LogOutput.log` nach `Map/Trail mod settings`.
