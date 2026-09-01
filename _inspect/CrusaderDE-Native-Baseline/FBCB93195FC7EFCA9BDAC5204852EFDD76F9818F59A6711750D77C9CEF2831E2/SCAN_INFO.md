# CrusaderDE Native Analysis Baseline

## Source identity

- Created: 2026-09-01, Europe/Berlin
- Source: `E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll`
- File size: 3,451,392 bytes
- Last modified: 2026-08-24 17:06:48 local time
- SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
- PE architecture: Windows x86-64, image base `0x180000000`
- PE debug GUID: `3C04FE43919A4DE78B2E8981A9B79F592`
- Embedded but unavailable PDB path: `D:\Jenkins\.jenkins\workspace\CrusaderDE\CDE-DLL-STABLE\CrusaderDEDLL\Source\ff_gfx_manager\Release\Crusader.pdb`

This baseline is valid only for the exact SHA-256 above. Do not reuse native addresses after a game update without checking the installed DLL again.

## Toolchain

- Cutter 2.4.1 with bundled Rizin 0.8.1, commit `c009cb739ca4fd289e634c2bea432d1e7bcd3676`
- Ghidra 12.1.3 official public release
- Eclipse Temurin JDK 21.0.12.1+1 LTS, portable
- Ghidra archive SHA-256: `93A5D11A9AD510622ACAAF908C556A7B9B764D338E78A7567F3689BF5081FD54`
- Temurin archive SHA-256: `F9D6E191AB098C0D416E7D588A24420A8621CD2F4720DAB2459B8B7B2D2D8B4E`

Installed tools remain versioned under `.tools/ghidra-12.1.3` and `.tools/temurin-jdk-21.0.12.1+1`. The verified download archives remain under `.tools/downloads`.

## Analysis configuration

### Rizin

The bundled Rizin was started with the existing Ghidra Sleigh configuration. The stable `aaa` analysis was run once, followed by `Ps` only after analysis completion. Neither experimental `aaaa` nor a separate aggressive prelude scan was used.

Equivalent command from the workspace root:

    & '.native-analysis\Run-Rizin-With-Ghidra.cmd' -q -e scr.color=false -c aaa -c "Ps _inspect/CrusaderDE-Native-Baseline/FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2/rizin/CrusaderDE-rizin-0.8.1-aaa.rzdb" -c aflc 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'

- Analysis and save duration: approximately 2,656 seconds (44 minutes 16 seconds)
- Saved project size: 75,235,971 bytes
- Saved project SHA-256: `EE27AD51C7119F234AAD7ED8C0B33220319D11379ECC16B3FD5C604234923A95`
- The project was reopened successfully from its final `_inspect` path.

### Ghidra

Ghidra imported the DLL using `Portable Executable (PE)` and `x86:LE:64:default:windows`. Its stable default analyzers ran, including reference, stack, call-convention, switch/decompiler, Function ID, PE exception handling and Microsoft RTTI analysis. The optional aggressive instruction finder was not enabled. PDB processing was attempted by the default analyzer and skipped because no matching PDB was available.

- Ghidra auto-analysis reported 67 seconds.
- The project was saved as `ghidra/CrusaderDE-Ghidra-12.1.3.gpr` plus its matching `.rep` directory.
- A separate read-only headless run generated exports.
- A second fresh read-only headless run validated the saved project.
- The initial attempt under `.native-analysis` was rejected before import because Ghidra forbids path elements beginning with `.`. Its diagnostic is preserved as `logs/ghidra-path-rejection.log`. The successful project and the complete baseline therefore reside under `_inspect`.

## Results

| Measurement | Ghidra 12.1.3 | Rizin 0.8.1 |
|---|---:|---:|
| Functions | 4,577 total; 4,478 non-external | 3,152 |
| References/Xrefs | 236,382 | 72,720 |
| Strings | 4,248 defined strings | 40,171 raw strings |
| Imports | 106 | 106 |
| Exports | 78 | available in the project |
| Sections/blocks | 9 including headers and external `tdb` | 7 PE sections |

The different function, reference and string counts are expected because the tools use different discovery and classification strategies. Treat agreement as strong corroboration and investigate disagreements rather than blindly preferring one tool.

Ghidra attempted to decompile all 4,478 non-external functions:

- Completed: 4,475
- Failed: 3
- Failed addresses: `0x180077E60`, `0x18009B2B0`, `0x1800ABB90`
- Failure reason: decompiler flow exceeded the maximum allowable instruction count

Every attempt is recorded in `exports/decompile-status.jsonl`.

## Reusable artifacts

### Ghidra-derived, search-friendly data

- `exports/functions.jsonl`: VA, RVA, name, namespace, size, signature, calling convention and thunk/external flags
- `exports/xrefs.jsonl`: source/destination VA and RVA, reference type and containing function
- `exports/strings.jsonl`: VA, RVA, encoding, value and reference count
- `exports/imports.jsonl`, `exports/exports.jsonl`, `exports/sections.json`
- `exports/decompiled-functions.c`: consolidated pseudocode with a VA/RVA marker before every successfully decompiled function
- `exports/decompiled-functions.c` SHA-256: `17C4AF34FBBDF5A3E4EDF4B6E6185984D99EDBCF0FEA2FD7777EFB6F24A9D541`
- `exports/decompile-status.jsonl`: per-function completion status

### Raw Rizin JSON

- `exports/rizin-functions.json`
- `exports/rizin-xrefs.json`
- `exports/rizin-strings.json`
- `exports/rizin-imports.json`
- `exports/rizin-sections.json`

The reusable Ghidra scripts are stored under `tools/`.

## Opening and searching

Open the Rizin project from the workspace root:

    & '.native-analysis\Run-Rizin-With-Ghidra.cmd' -p '_inspect\CrusaderDE-Native-Baseline\FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2\rizin\CrusaderDE-rizin-0.8.1-aaa.rzdb' --

Open the Ghidra GUI with `.tools\ghidra-12.1.3\ghidra_12.1.3_PUBLIC\ghidraRun.bat`, then select `ghidra\CrusaderDE-Ghidra-12.1.3.gpr`. Keep this baseline unchanged and use a copy for project-specific renames or annotations.

Typical fast text searches:

    rg -n -i 'market|price|goods' exports\strings.jsonl exports\decompiled-functions.c
    rg -n -F '0x180077E60' exports\functions.jsonl exports\xrefs.jsonl exports\decompile-status.jsonl

## Validation record

- Canonical source SHA-256 rechecked immediately before analysis.
- Rizin project reopened from its final location: 3,152 functions, 72,720 Xrefs, 40,171 raw strings, 106 imports and 7 sections.
- Ghidra project reopened in a fresh read-only process: 4,577 total functions, 4,248 strings and 236,382 references.
- All six JSONL files and all five raw Rizin JSON files parsed successfully.
- All 13,282 Ghidra function, string, export and decompile-status records plus all 236,382 Xref records passed complete VA/RVA relation and internal source-range validation.
- All generated exports use CRLF and contain no naked LF characters.
- Fifteen escaped `\r\n` sequences in `strings.jsonl` are intentional source data from two embedded strings: the PKWARE copyright text and the PE manifest XML.
- Existing `.native-analysis/enemy-gate-*` projects were not changed.
