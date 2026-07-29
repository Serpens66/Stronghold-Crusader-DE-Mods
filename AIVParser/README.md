# Stronghold Crusader DE AIV Parser

`AIVParser` reads the Definitive Edition `.aivjson` castle blueprint format. It preserves
the original build order while resolving mapper names, the keep anchor, 100x100 grid
coordinates, unit slots, pauses, rotations, and keep-relative placement deltas.

The tool does not modify AIV files or game state. HD `.aiv` containers and DE `.baiv`
raw data are intentionally outside V1; the local Script Extender already provides the
`SHCDESE.AIVDecoder` converter for `.baiv`.

## Build and test

    dotnet build AIVParser.sln -c Release
    dotnet run --project AIVParser.Tests/AIVParser.Tests.csproj -c Release

All projects are package-free. `AIVParser.Core` targets `netstandard2.0` so a future
net481 BepInEx plugin can reference it. Its DTOs use public fields with the game's
original spellings and can be populated by Unity `JsonUtility`.

## Validate files

    dotnet run --project AIVParser.Cli/AIVParser.Cli.csproj -c Release -- validate "C:\path\to\ExtendedLords"

Directories are searched recursively for `*.aivjson`. Validation writes no files.
Exit code `0` means all files are structurally valid, `1` means at least one invalid
file, and `2` indicates usage or I/O failure.

## Inspect one castle

    dotnet run --project AIVParser.Cli/AIVParser.Cli.csproj -c Release -- inspect "C:\path\castle.aivjson" --rotation 90

Inspection writes `<name>.parsed.json` and `<name>.svg` to `AIVParser-output` in the
current directory. Use `-o <directory>` to choose another output folder. The SVG is
self-contained and every placement has a tooltip with its frame, type, source offset,
grid coordinates, and pause flag.

## Coordinate contract

The parser does not guess how the AIV axes map to the game's world-tile X/Y axes:

- `Row = encodedOffset / 100`
- `Column = encodedOffset % 100`
- 90-degree rotation maps `(row, column)` to `(column, 99-row)`
- placement deltas are calculated relative to the equally rotated keep

This retains all information needed by a later spawner while leaving the final
Row/Column-to-world-axis verification explicit.
