# Custom Custom Trail compatibility for mod authors

`CustomCustomTrail` can save and restore another mod's host-controlled lobby settings without a compile-time reference to that mod. Compatible installed mods are discovered automatically and appear as checkboxes in the `CustomCustomTrail` settings.

Your mod does **not** need to reference `CustomCustomTrail.dll` or contain mod-specific integration code.

## Compatibility requirements

A mod is detected as compatible when all of the following are true:

1. It registers a lobby-modsettings ViewModel through `GameXAMLManagerAPI`.
2. The ViewModel exposes at least one public readable and writable property marked `[SyncHostOnly]` and not marked `[DoNotPersist]`.
3. The ViewModel exposes the mission-preset API listed below.
4. Every captured `[SyncHostOnly]` value can be serialized by MessagePack.

`CustomCustomTrail` uses the owning BepInEx plugin GUID as the stable identity. The display name and the name passed to `RegisterLobbyModSettings` may be different.

Only host-controlled settings belong in a Trail. `[SyncPerPlayer]`, `[PresetLocal]`, and `[PersistLocal]` values remain owned by each player and are never captured.

## Recommended integration

The easiest and safest integration is to use the workspace's shared preset system. It already implements mission snapshots, Trail locking, restoration of the player's previous preset, host/client authority, and persistence isolation.

Link these canonical source files into the runtime project instead of copying their contents:

- [`Shared/PresetLobbyModSettingsViewModel.cs`](Shared/PresetLobbyModSettingsViewModel.cs)
- [`Shared/GameModeHelper.cs`](Shared/GameModeHelper.cs)
- [`Shared/DebugLogHelper.cs`](Shared/DebugLogHelper.cs)

Example project entries:

    <Compile Include="..\Shared\PresetLobbyModSettingsViewModel.cs">
      <Link>Shared\PresetLobbyModSettingsViewModel.cs</Link>
    </Compile>
    <Compile Include="..\Shared\GameModeHelper.cs">
      <Link>Shared\GameModeHelper.cs</Link>
    </Compile>
    <Compile Include="..\Shared\DebugLogHelper.cs">
      <Link>Shared\DebugLogHelper.cs</Link>
    </Compile>

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

`EnableMod` is optional for compatibility, but strongly recommended. If present as a Boolean `[SyncHostOnly]` property, a Trail can explicitly restore the mod's enabled or disabled state. Without it, the compatible host settings are still captured and restored.

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

- applying a Trail snapshot must not overwrite the player's normal local settings file;
- leaving the Trail must restore the exact previous local preset;
- read-only Trail host settings must reject local client edits;
- personal settings must remain unchanged;
- snapshot application and restoration must be atomic from the ViewModel's perspective.

Unless there is a strong reason to maintain a separate implementation, use the shared base class.

## Property rules

- Use `[SyncHostOnly]` for settings that define shared match rules and should be stored in a Trail.
- Use `[SyncPerPlayer]` for synchronized personal preferences. They are not stored in a Trail.
- Use `[PresetLocal]` for local settings participating in presets. They are not stored in a Trail.
- Use `[PersistLocal]` for local settings outside the preset system. They are not stored in a Trail.
- Add `[DoNotPersist]` to transient network/status properties. Such properties are deliberately excluded from Trail capture even when they are `[SyncHostOnly]`.
- Public `[SyncHostOnly]` properties must have both a getter and setter.
- Property values must be MessagePack-serializable. Primitive values and arrays are the simplest choices; explicitly attributed MessagePack models are suitable for complex values.

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

If the mod is listed as incompatible, first confirm that it uses `LobbyModSettingsPresetRegistration.Register(...)`, has at least one persistent `[SyncHostOnly]` property, and was built against the currently supported Script Extender API.
