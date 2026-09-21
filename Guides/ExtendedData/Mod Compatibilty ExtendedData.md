# ExtendedData compatibility for mod authors / ExtendedData-Kompatibilität für Modentwickler

[English](#english) | [Deutsch](#deutsch)

## English

This guide is for mod authors. Map and Trail creators should use [Mod settings in Maps and Custom Trails](Custom%20Trail%20Mod%20Settings.md#english).

`ExtendedData` can save and restore another mod's host-controlled lobby settings for Maps and Trails without a compile-time reference from that mod to `ExtendedData.dll`. Compatible installed mods are discovered automatically and appear as per-setting mode selectors in the `ExtendedData` settings.

### Compatibility requirements

A mod is compatible when all of these conditions are met:

1. It registers a lobby-modsettings ViewModel through `GameXAMLManagerAPI`.
2. The ViewModel exposes at least one public readable and writable `[SyncHostOnly]` property that is not `[DoNotPersist]`.
3. The ViewModel exposes the mission-preset API listed below.
4. Every captured value is non-null and MessagePack-serializable.
5. Its disabled/default mission snapshot contains a valid MessagePack value for every captured property.
6. The owning BepInEx plugin GUID has exactly one registered lobby-modsettings panel.

ExtendedData uses the owning BepInEx plugin GUID as the stable identity. The display name and the name passed to `RegisterLobbyModSettings` may differ.

Only host-controlled match rules belong in a Map or Trail. `[SyncPerPlayer]`, `[PresetLocal]`, `[PersistLocal]`, and transient `[DoNotPersist]` values are never captured.

### Recommended integration

Use the repository's current shared preset implementation. It already implements mission snapshots, Map/Trail locking, restoration of the previous local preset, host/client authority, persistence isolation, mission lifecycle handling, and process-wide lobby observation.

Vendor these source files together and update them as one unit:

- [`Shared/PresetLobbyModSettingsViewModel.cs`](../../Shared/PresetLobbyModSettingsViewModel.cs)
- [`Shared/GameModeHelper.cs`](../../Shared/GameModeHelper.cs)
- [`Shared/GameplaySessionLifecycle.cs`](../../Shared/GameplaySessionLifecycle.cs)
- [`Shared/DebugLogHelper.cs`](../../Shared/DebugLogHelper.cs)
- [`Shared/ModSettingsSearch.cs`](../../Shared/ModSettingsSearch.cs)

The runtime project must reference BepInEx, `Assembly-CSharp.dll`, `SHCDESE.dll`, `R3.dll`, `MessagePack.dll`, `Noesis.NoesisGUI.dll`, Steamworks.NET, and `APIShared.dll`. Reference `APIShared.dll` with `Private=false`, define `API_SHARED_LOBBY_OBSERVER`, and require the same APIShared version at runtime instead of copying a private DLL beside the consuming mod.

Example project fragments:

```xml
<PropertyGroup>
  <DefineConstants>$(DefineConstants);API_SHARED_LOBBY_OBSERVER</DefineConstants>
</PropertyGroup>

<ItemGroup>
  <Reference Include="APIShared">
    <HintPath>$(ApiSharedDir)\APIShared.dll</HintPath>
    <Private>false</Private>
  </Reference>
  <Compile Include="Compatibility\SerpShared\PresetLobbyModSettingsViewModel.cs">
    <Link>Shared\PresetLobbyModSettingsViewModel.cs</Link>
  </Compile>
  <Compile Include="Compatibility\SerpShared\GameModeHelper.cs">
    <Link>Shared\GameModeHelper.cs</Link>
  </Compile>
  <Compile Include="Compatibility\SerpShared\GameplaySessionLifecycle.cs">
    <Link>Shared\GameplaySessionLifecycle.cs</Link>
  </Compile>
  <Compile Include="Compatibility\SerpShared\DebugLogHelper.cs">
    <Link>Shared\DebugLogHelper.cs</Link>
  </Compile>
  <Compile Include="Compatibility\SerpShared\ModSettingsSearch.cs">
    <Link>Shared\ModSettingsSearch.cs</Link>
  </Compile>
</ItemGroup>
```

Derive the settings ViewModel from `PresetLobbyModSettingsViewModel`:

```csharp
using Shared;
using SHCDESE.API.Components.Network;

public sealed class ExampleSettingsViewModel : PresetLobbyModSettingsViewModel
{
    private bool enableMod = true;
    private int strength = 100;

    [SyncHostOnly]
    public bool EnableMod
    {
        get => enableMod;
        set
        {
            if (!CanMutateSetting() || enableMod == value)
                return;
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
            if (!CanMutateSetting() || strength == value)
                return;
            strength = value;
            OnPropertyChanged(nameof(Strength));
        }
    }
}
```

Register it after the Script Extender library is ready:

```csharp
LobbyModSettingsPresetRegistration.Register(
    this,
    Logger,
    PluginGuid,
    Settings,
    "ScriptExtenderUI/ExampleSettings.xaml");
```

Use the stable BepInEx `PluginGuid` as `modName`. Register exactly one lobby-modsettings ViewModel per plugin GUID.

`EnableMod` is optional but recommended. When it is a Boolean `[SyncHostOnly]` property, a mission can explicitly restore the mod's enabled or disabled state. Its disabled snapshot must contain `false`.

### Explicit opt-out

A mod whose settings must never be owned by a Map or Trail can opt out without referencing ExtendedData. Add this exact public constant to the BepInEx plugin class that owns the registered panel:

```csharp
public const bool ExtendedDataModSettingsOptOut = true;
```

The member name is case-sensitive and must be a public compile-time constant. A property, mutable field, or `false` value does not opt out. An opted-out plugin is omitted from discovery, compatibility warnings, and capture.

### Required mission-preset API

`PresetLobbyModSettingsViewModel` provides this API automatically. A custom implementation must expose these exact public instance members:

```csharp
public Dictionary<string, byte[]> System_CreateDisabledMissionPresetSnapshot();
public void System_EnterMissionPreset(
    Dictionary<string, byte[]> snapshot,
    string label,
    bool editable);
public void System_ExitMissionPreset();
public bool IsMissionPresetActive { get; }
```

The contract is discovered by member shape, so the consuming mod needs no `ExtendedData.dll` reference.

A custom implementation must provide the same guarantees:

- Snapshot creation is side-effect free, returns a non-null dictionary, and includes every persistent `[SyncHostOnly]` property.
- `[SyncHostOnly] bool EnableMod`, when present, is `false` in the disabled snapshot; other values use the mod's safe defaults.
- Entering a mission preset applies only the supplied host snapshot, records the exact previous local preset, and does not overwrite the normal local settings file.
- Exiting restores that exact preset; calling exit while inactive is a safe no-op.
- A read-only Map/Trail context rejects local changes to host settings while personal settings remain editable.
- Application and restoration are atomic from the ViewModel's perspective and do not throw during normal operation.

### Property rules

- `[SyncHostOnly]`: persistent shared match rules eligible for Map/Trail storage.
- `[SyncPerPlayer]`: synchronized personal settings; never captured.
- `[PresetLocal]`: local settings participating in normal presets; never captured.
- `[PersistLocal]`: local settings outside the preset system; never captured.
- `[DoNotPersist]`: transient values; excluded even when also `[SyncHostOnly]`.
- Captured properties require public getters and setters and non-null MessagePack-serializable values.
- Use property setters and commands to enforce authority; disabled UI alone is not a security boundary.

ExtendedData owns the schema-3 JSON document. Compatible mods serialize only their property values through MessagePack and must not implement a second Map/Trail document serializer.

### Verification checklist

- The mod appears with per-setting modes under compatible mods in ExtendedData.
- **Mod default**, **Player/host**, and **Fixed creator value** each produce the documented result.
- An all-default selection omits the mod from the active document.
- Personal, local, and transient values are absent.
- Map/Trail activation does not modify the normal local preset file.
- Map changes, lobby exit, mission end, restarts, and Trail Maker test returns restore or retain the correct context.
- Multiplayer clients cannot alter read-only host settings.
- The mod builds and runs with the current shared source set and required APIShared dependency.

If compatibility fails, search `BepInEx/LogOutput.log` for `Map/Trail mod settings`; ExtendedData records the plugin GUID and concrete rejection reason there.

---

## Deutsch

Dieser Guide richtet sich an Modentwickler. Map- und Trail-Ersteller verwenden [Mod-Einstellungen in Maps und Custom Trails](Custom%20Trail%20Mod%20Settings.md#deutsch).

`ExtendedData` kann hostverwaltete Lobby-Einstellungen eines anderen Mods für Maps und Trails speichern und wiederherstellen, ohne dass dieser Mod zur Kompilierzeit `ExtendedData.dll` referenziert. Installierte kompatible Mods werden automatisch erkannt und erscheinen mit einem Modus pro Einstellung in den `ExtendedData`-Einstellungen.

### Kompatibilitätsanforderungen

Ein Mod ist kompatibel, wenn alle folgenden Bedingungen erfüllt sind:

1. Er registriert über `GameXAMLManagerAPI` ein Lobby-Modsettings-ViewModel.
2. Das ViewModel stellt mindestens eine öffentliche les- und schreibbare `[SyncHostOnly]`-Eigenschaft bereit, die nicht mit `[DoNotPersist]` markiert ist.
3. Das ViewModel stellt die unten aufgeführte Missions-Preset-API bereit.
4. Jeder erfasste Wert ist nicht null und mit MessagePack serialisierbar.
5. Der deaktivierte Standard-Missionssnapshot enthält für jede erfasste Eigenschaft einen gültigen MessagePack-Wert.
6. Für die zugehörige BepInEx-Plugin-GUID ist genau ein Lobby-Modsettings-Panel registriert.

ExtendedData verwendet die BepInEx-Plugin-GUID des Besitzers als stabile Identität. Anzeigename und der an `RegisterLobbyModSettings` übergebene Name dürfen davon abweichen.

Nur hostverwaltete Spielregeln gehören in eine Map oder einen Trail. `[SyncPerPlayer]`, `[PresetLocal]`, `[PersistLocal]` und vorübergehende `[DoNotPersist]`-Werte werden niemals erfasst.

### Empfohlene Integration

Verwende die aktuelle gemeinsame Preset-Implementierung dieses Repositories. Sie implementiert bereits Missionssnapshots, Map-/Trail-Sperren, die Wiederherstellung des vorherigen lokalen Presets, Host-/Client-Autorität, isolierte Persistenz, den Missionslebenszyklus und die prozessweite Lobby-Beobachtung.

Übernimm diese Quelldateien gemeinsam und aktualisiere sie immer als Einheit:

- [`Shared/PresetLobbyModSettingsViewModel.cs`](../../Shared/PresetLobbyModSettingsViewModel.cs)
- [`Shared/GameModeHelper.cs`](../../Shared/GameModeHelper.cs)
- [`Shared/GameplaySessionLifecycle.cs`](../../Shared/GameplaySessionLifecycle.cs)
- [`Shared/DebugLogHelper.cs`](../../Shared/DebugLogHelper.cs)
- [`Shared/ModSettingsSearch.cs`](../../Shared/ModSettingsSearch.cs)

Das Runtime-Projekt muss BepInEx, `Assembly-CSharp.dll`, `SHCDESE.dll`, `R3.dll`, `MessagePack.dll`, `Noesis.NoesisGUI.dll`, Steamworks.NET und `APIShared.dll` referenzieren. Referenziere `APIShared.dll` mit `Private=false`, definiere `API_SHARED_LOBBY_OBSERVER` und verlange dieselbe APIShared-Version zur Laufzeit, statt eine private DLL neben den konsumierenden Mod zu kopieren.

Beispielhafte Projektelemente:

```xml
<PropertyGroup>
  <DefineConstants>$(DefineConstants);API_SHARED_LOBBY_OBSERVER</DefineConstants>
</PropertyGroup>

<ItemGroup>
  <Reference Include="APIShared">
    <HintPath>$(ApiSharedDir)\APIShared.dll</HintPath>
    <Private>false</Private>
  </Reference>
  <Compile Include="Compatibility\SerpShared\PresetLobbyModSettingsViewModel.cs">
    <Link>Shared\PresetLobbyModSettingsViewModel.cs</Link>
  </Compile>
  <Compile Include="Compatibility\SerpShared\GameModeHelper.cs">
    <Link>Shared\GameModeHelper.cs</Link>
  </Compile>
  <Compile Include="Compatibility\SerpShared\GameplaySessionLifecycle.cs">
    <Link>Shared\GameplaySessionLifecycle.cs</Link>
  </Compile>
  <Compile Include="Compatibility\SerpShared\DebugLogHelper.cs">
    <Link>Shared\DebugLogHelper.cs</Link>
  </Compile>
  <Compile Include="Compatibility\SerpShared\ModSettingsSearch.cs">
    <Link>Shared\ModSettingsSearch.cs</Link>
  </Compile>
</ItemGroup>
```

Leite das Einstellungs-ViewModel von `PresetLobbyModSettingsViewModel` ab:

```csharp
using Shared;
using SHCDESE.API.Components.Network;

public sealed class ExampleSettingsViewModel : PresetLobbyModSettingsViewModel
{
    private bool enableMod = true;
    private int strength = 100;

    [SyncHostOnly]
    public bool EnableMod
    {
        get => enableMod;
        set
        {
            if (!CanMutateSetting() || enableMod == value)
                return;
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
            if (!CanMutateSetting() || strength == value)
                return;
            strength = value;
            OnPropertyChanged(nameof(Strength));
        }
    }
}
```

Registriere es, nachdem die Script-Extender-Bibliothek bereit ist:

```csharp
LobbyModSettingsPresetRegistration.Register(
    this,
    Logger,
    PluginGuid,
    Settings,
    "ScriptExtenderUI/ExampleSettings.xaml");
```

Verwende die stabile BepInEx-`PluginGuid` als `modName`. Registriere genau ein Lobby-Modsettings-ViewModel pro Plugin-GUID.

`EnableMod` ist optional, aber empfohlen. Ist es eine boolesche `[SyncHostOnly]`-Eigenschaft, kann eine Mission den aktivierten oder deaktivierten Zustand des Mods ausdrücklich wiederherstellen. Der deaktivierte Snapshot muss `false` enthalten.

### Explizites Opt-out

Ein Mod, dessen Einstellungen niemals einer Map oder einem Trail gehören dürfen, kann ohne ExtendedData-Referenz aussteigen. Ergänze exakt diese öffentliche Konstante in der BepInEx-Plugin-Klasse, der das registrierte Panel gehört:

```csharp
public const bool ExtendedDataModSettingsOptOut = true;
```

Der Membername beachtet Groß-/Kleinschreibung und muss eine öffentliche Compilezeitkonstante sein. Eine Property, ein veränderliches Feld oder der Wert `false` bewirken kein Opt-out. Ein ausgestiegenes Plugin wird bei Erkennung, Kompatibilitätswarnungen und Erfassung ausgelassen.

### Erforderliche Missions-Preset-API

`PresetLobbyModSettingsViewModel` stellt diese API automatisch bereit. Eine eigene Implementierung muss exakt diese öffentlichen Instanzmember anbieten:

```csharp
public Dictionary<string, byte[]> System_CreateDisabledMissionPresetSnapshot();
public void System_EnterMissionPreset(
    Dictionary<string, byte[]> snapshot,
    string label,
    bool editable);
public void System_ExitMissionPreset();
public bool IsMissionPresetActive { get; }
```

Der Vertrag wird anhand der Memberform erkannt, daher benötigt der konsumierende Mod keine Referenz auf `ExtendedData.dll`.

Eine eigene Implementierung muss dieselben Garantien bieten:

- Die Snapshoterstellung ist nebenwirkungsfrei, gibt ein nicht-null Dictionary zurück und enthält jede dauerhafte `[SyncHostOnly]`-Eigenschaft.
- Eine vorhandene `[SyncHostOnly] bool EnableMod` ist im deaktivierten Snapshot `false`; andere Werte verwenden die sicheren Standardwerte des Mods.
- Beim Eintritt wird nur der übergebene Host-Snapshot angewendet und das genaue vorherige lokale Preset gespeichert, ohne die normale lokale Einstellungsdatei zu überschreiben.
- Beim Austritt wird genau dieses Preset wiederhergestellt; ein Austritt im inaktiven Zustand ist ein sicherer No-op.
- Ein schreibgeschützter Map-/Trail-Kontext weist lokale Änderungen an Host-Einstellungen ab, während persönliche Einstellungen bearbeitbar bleiben.
- Anwendung und Wiederherstellung sind aus Sicht des ViewModels atomar und werfen im Normalbetrieb keine Exceptions.

### Eigenschaftsregeln

- `[SyncHostOnly]`: dauerhafte gemeinsame Spielregeln, die für Map-/Trail-Speicherung infrage kommen.
- `[SyncPerPlayer]`: synchronisierte persönliche Einstellungen; werden nie erfasst.
- `[PresetLocal]`: lokale Einstellungen in normalen Presets; werden nie erfasst.
- `[PersistLocal]`: lokale Einstellungen außerhalb des Preset-Systems; werden nie erfasst.
- `[DoNotPersist]`: vorübergehende Werte; auch zusammen mit `[SyncHostOnly]` ausgeschlossen.
- Erfasste Properties benötigen öffentliche Getter und Setter sowie nicht-null, MessagePack-serialisierbare Werte.
- Autorität muss in Settern und Commands erzwungen werden; eine deaktivierte Oberfläche allein ist keine Sicherheitsgrenze.

ExtendedData besitzt das Schema-3-JSON-Dokument. Kompatible Mods serialisieren ausschließlich ihre Eigenschaftswerte mit MessagePack und implementieren keinen zweiten Map-/Trail-Dokumentserializer.

### Prüfliste

- Der Mod erscheint mit Modi pro Einstellung unter den kompatiblen Mods in ExtendedData.
- **Mod default**, **Player/host** und **Fixed creator value** erzeugen jeweils das dokumentierte Ergebnis.
- Eine reine Standardauswahl lässt den Mod im aktiven Dokument aus.
- Persönliche, lokale und vorübergehende Werte fehlen.
- Die Map-/Trail-Aktivierung verändert die normale lokale Preset-Datei nicht.
- Map-Wechsel, Lobby-Austritt, Missionsende, Neustarts und Trail-Maker-Test-Rückkehr behalten oder restaurieren den richtigen Kontext.
- Multiplayer-Clients können schreibgeschützte Host-Einstellungen nicht ändern.
- Der Mod baut und läuft mit dem aktuellen Shared-Quellsatz und der erforderlichen APIShared-Abhängigkeit.

Schlägt die Kompatibilität fehl, suche in `BepInEx/LogOutput.log` nach `Map/Trail mod settings`; ExtendedData protokolliert dort Plugin-GUID und konkreten Ablehnungsgrund.
