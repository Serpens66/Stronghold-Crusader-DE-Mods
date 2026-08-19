# Custom Custom Trail compatibility for mod authors

`CustomCustomTrail` can save and restore another mod's host-controlled lobby settings without a compile-time reference to that mod. Compatible installed mods are discovered automatically and appear as checkboxes in the `CustomCustomTrail` settings.

Your mod does **not** need to reference `CustomCustomTrail.dll` or contain mod-specific integration code.

## Compatibility requirements

A mod is detected as compatible when all of the following are true:

1. It registers a lobby-modsettings ViewModel through `GameXAMLManagerAPI`.
2. The ViewModel exposes at least one public readable and writable property marked `[SyncHostOnly]` and not marked `[DoNotPersist]`.
3. The ViewModel exposes the mission-preset API listed below.
4. Every captured `[SyncHostOnly]` value is non-null and can be serialized by MessagePack.
5. Its disabled/default mission snapshot contains a valid MessagePack value for every captured property.
6. The owning BepInEx plugin GUID has exactly one registered lobby-modsettings panel.

`CustomCustomTrail` uses the owning BepInEx plugin GUID as the stable identity. The display name and the name passed to `RegisterLobbyModSettings` may be different.

Only host-controlled settings belong in a Trail. `[SyncPerPlayer]`, `[PresetLocal]`, and `[PersistLocal]` values remain owned by each player and are never captured.

## Recommended integration

The easiest and safest integration is to use this repository's shared preset system. It already implements mission snapshots, Trail locking, restoration of the player's previous preset, host/client authority, and persistence isolation.

Use the current versions of these three source files together:

- [`Shared/PresetLobbyModSettingsViewModel.cs`](Shared/PresetLobbyModSettingsViewModel.cs)
- [`Shared/GameModeHelper.cs`](Shared/GameModeHelper.cs)
- [`Shared/DebugLogHelper.cs`](Shared/DebugLogHelper.cs)

For a separate repository, download or vendor the files into a directory such as `Compatibility/SerpShared` and update all three together when the shared contract changes. A Git submodule is also suitable. Do not manually reimplement fragments of the files.

Example project entries for vendored files:

    <Compile Include="Compatibility\SerpShared\PresetLobbyModSettingsViewModel.cs">
      <Link>Shared\PresetLobbyModSettingsViewModel.cs</Link>
    </Compile>
    <Compile Include="Compatibility\SerpShared\GameModeHelper.cs">
      <Link>Shared\GameModeHelper.cs</Link>
    </Compile>
    <Compile Include="Compatibility\SerpShared\DebugLogHelper.cs">
      <Link>Shared\DebugLogHelper.cs</Link>
    </Compile>

The runtime project must already reference BepInEx, `SHCDESE.dll`, `MessagePack.dll`, and `Noesis.NoesisGUI.dll`. Add these namespaces to the settings source:

    using Shared;
    using SHCDESE.API.Components.Network;

Derive the settings ViewModel from `PresetLobbyModSettingsViewModel`:

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

Register it with the shared registration helper after the Script Extender library is ready:

    LobbyModSettingsPresetRegistration.Register(
        this,
        Logger,
        PluginGuid,
        Settings,
        "ScriptExtenderUI/ExampleSettings.xaml");

Use the stable BepInEx `PluginGuid` as `modName`. This also keeps multiplayer synchronization and local preset storage stable.

Register exactly one lobby-modsettings ViewModel for each BepInEx plugin GUID. If a mod currently uses several panels, combine their settings behind one registered ViewModel before enabling Trail compatibility. This prevents two panels from competing for the same stable Trail identity.

`EnableMod` is optional for compatibility, but strongly recommended. If present as a Boolean `[SyncHostOnly]` property, a Trail can explicitly restore the mod's enabled or disabled state. Without it, the compatible host settings are still captured and restored.

## Explicit opt-out

A mod whose settings must never be owned by a Trail can opt out without referencing `CustomCustomTrail`. Add this exact public constant to the BepInEx plugin class that owns the registered modsettings panel:

    public const bool CustomCustomTrailModSettingsOptOut = true;

The marker is intentionally a compile-time constant rather than a configurable setting. `CustomCustomTrail` checks it before inspecting the ViewModel. An opted-out plugin is omitted completely: it receives no checkbox, does not appear in the incompatible-mod list, is not mentioned by compatibility warnings, and its settings are never captured or applied. The member name is case-sensitive; a property, mutable field, or value of `false` does not opt out.

## Required mission-preset API

Mods using `PresetLobbyModSettingsViewModel` receive this API automatically. A custom implementation must expose these exact public members:

    public Dictionary<string, byte[]> System_CreateDisabledMissionPresetSnapshot();
    public void System_EnterMissionPreset(
        Dictionary<string, byte[]> snapshot,
        string label,
        bool editable);
    public void System_ExitMissionPreset();
    public bool IsMissionPresetActive { get; }

The contract is discovered by member shape, so no shared interface assembly and no `CustomCustomTrail` reference are required.

A custom implementation must provide the same safety guarantees as the shared base class:

- `System_CreateDisabledMissionPresetSnapshot()` is side-effect free, returns a non-null dictionary, and includes a MessagePack value for every persistent `[SyncHostOnly]` property;
- when `[SyncHostOnly] bool EnableMod` exists, that snapshot contains `false` for it; all other values are the mod's current defaults;
- `System_EnterMissionPreset(...)` applies only the supplied host snapshot, records the exact prior local preset state, and sets `IsMissionPresetActive` to `true` after success;
- applying a Trail snapshot must not overwrite the player's normal local settings file;
- leaving the Trail must restore the exact previous local preset;
- `System_ExitMissionPreset()` is a safe no-op while inactive and sets `IsMissionPresetActive` to `false` after restoration;
- read-only Trail host settings must reject local client edits;
- personal settings must remain unchanged;
- snapshot application and restoration must be atomic from the ViewModel's perspective;
- all four contract members must be public instance members with the exact signatures shown above and must not throw during normal operation.

Unless there is a strong reason to maintain a separate implementation, use the shared base class.

## Property rules

- Use `[SyncHostOnly]` for settings that define shared match rules and should be stored in a Trail.
- Use `[SyncPerPlayer]` for synchronized personal preferences. They are not stored in a Trail.
- Use `[PresetLocal]` for local settings participating in presets. They are not stored in a Trail.
- Use `[PersistLocal]` for local settings outside the preset system. They are not stored in a Trail.
- Add `[DoNotPersist]` to transient network/status properties. Such properties are deliberately excluded from Trail capture even when they are `[SyncHostOnly]`.
- Public `[SyncHostOnly]` properties must have both a getter and setter.
- Property values must be non-null and MessagePack-serializable while compatibility is checked and while a Trail is saved. Primitive values and arrays are the simplest choices; explicitly attributed MessagePack models are suitable for complex values.

Do not write a second JSON serializer for Trail integration. `CustomCustomTrail` owns the `.modjson` format and serializes complex compatible values through MessagePack.

## UI expectations

Bind host-controlled interactive elements to the shared access properties, especially `CanEditHostSettings`. During a read-only Trail mission, the shared base class then locks only the Trail-owned host values while client settings remain editable.

Commands and property setters must both enforce the same authority. UI disablement alone is not a security boundary.

## Verification checklist

Before publishing a compatible mod, verify that:

- the mod appears with a checkbox under compatible mods in `CustomCustomTrail`;
- disabling that checkbox causes new Trail files to omit the mod entirely;
- enabling it stores all intended persistent `[SyncHostOnly]` values;
- `[SyncPerPlayer]`, `[PresetLocal]`, `[PersistLocal]`, and `[DoNotPersist]` values are absent;
- playing a Trail applies its host settings without changing the local `.msgpack` file;
- leaving the Trail restores the previously selected local preset;
- a multiplayer client cannot alter read-only Trail host settings;
- disabling the mod itself is captured correctly when it exposes `[SyncHostOnly] bool EnableMod`.

If the mod is listed as incompatible, open `BepInEx/LogOutput.log` and search for `Trail mod-settings compatibility rejected`. `CustomCustomTrail` writes the plugin GUID and the concrete reason there, while the in-game settings intentionally show only the comma-separated mod names. Then confirm that the mod uses `LobbyModSettingsPresetRegistration.Register(...)`, has one registered panel and at least one persistent `[SyncHostOnly]` property, returns a complete disabled snapshot, and was built against the currently supported Script Extender API.
