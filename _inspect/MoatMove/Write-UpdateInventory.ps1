$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$modDir = Join-Path $workspace 'Testmods\MoatMove'
$rows = [Collections.Generic.List[string]]::new()
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $modDir 'src') -Filter '*.cs' -File) {
    $lines = [IO.File]::ReadAllLines($file.FullName)
    for ($index = 0; $index -lt $lines.Length; $index++) {
        $line = $lines[$index]
        $constant = [regex]::Match($line, 'const\s+(?:int|uint|ulong)\s+(\w*Rva)\s*=\s*(0x[0-9A-Fa-f]+)')
        if ($constant.Success) {
            $rows.Add('| ' + $constant.Groups[1].Value + ' | `' + $constant.Groups[2].Value + '` | ' + $file.Name + ':' + ($index + 1) + ' |')
        }
        elseif ($line -match 'libraryBase\s*\+\s*(0x[0-9A-Fa-f]+)') {
            foreach ($address in [regex]::Matches($line, 'libraryBase\s*\+\s*(0x[0-9A-Fa-f]+)')) {
                $rows.Add('| Module-relative expression | `' + $address.Groups[1].Value + '` | ' + $file.Name + ':' + ($index + 1) + ' |')
            }
        }
        elseif ($line -match 'InstallConnectivityObserver|Rvas\s*=') {
            $segment = ($lines[$index..([Math]::Min($index + 2, $lines.Length - 1))] -join ' ')
            foreach ($address in [regex]::Matches($segment, '0x[0-9A-Fa-f]+')) {
                $rows.Add('| Inline expression / observer / address array | `' + $address.Value + '` | ' + $file.Name + ':' + ($index + 1) + ' |')
            }
        }
    }
}
$header = @'
# Updating MoatMove for another native DLL

## Reference identity and scope

Feature owner for every entry below: MoatMove precise friendly/allied moat movement.
Reference SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Script Extender: 2.5.0, commit `5f02af6d074af7c741ebdaaccb48add39eba1bf4`.
Installed RedBird.X64: `1.1.0+0611afaa6da6f3e302ac40df00c88398880a81ef`.

Read CURRENT.md/CURRENT.json and verify the installed canonical DLL before using
these addresses. The user authorized an unchanged copy with a limited repeated
Vanilla-audit exception; no complete optimization audit is claimed. The behavioral
source and every copied hash are recorded in SOURCE_PROVENANCE.json.

## Resolution, signatures and failure contracts

- Named function targets use the corresponding `*Pattern` constant and `Resolve`
  call in the referenced source. On the exact hash, matching bytes at the reference
  RVA are used first. If they differ, Shared.NativePatternResolver searches executable
  PE sections for one match. The copied wrapper additionally requires the resolved
  RVA to equal its reference. Unknown hashes are rejected before construction.
- The group movement helper at `0x117C70` has an existing three-match fallback
  pattern. Its canonical entry bytes match. A fallback attempt is intentionally
  fail-closed; this extraction does not silently strengthen/change that signature.
- Observer entries use their inline exact byte strings, then an executable-section
  uniqueness check. A changed entry fails before installation; there is no alternate
  observer location. Reconstruction at `0xE32B0` validates all 56 bytes including its
  call to `0xE1640`; its detour target is the function entry, not that call site.
- Call-site entries validate complete five-byte E8 instructions and signed relative
  targets. Cursor gate and consumer entries are read/validated contexts, not global
  patches. Their exact byte arrays and derivations are in the linked source lines.
- Fixed data/table RVAs, manager prefixes and unit/tribe/moat/path-buffer offsets
  have no independent semantic relocation fallback. A code pattern alone does not
  prove these layouts. Keep the entire dependent feature hash-gated on an update.
- Rooting and constructor/transaction rollback are unchanged. Main initialization
  failure logs an error. Existing optional cursor/attack/work groups log their own
  failure and can leave ordinary movement installed: inspect every group in a test.

## Inline recovery machine contract

The sole inline hook is `[0x19664B, 0x196659)`, exactly 14 bytes:

`33 C0 8B D6 48 89 05 8E 70 F1 05 49 8B CF`

It displaces `xor eax,eax; mov edx,esi; mov [RIP+...],rax; mov rcx,r15`.
RedBird's installed implementation decodes at least max(requested length,14)
whole bytes. The decode-only installed-assembly test confirms DisplacedByteCount=14.
The constructor does not install a hook during this test. Function-detour prefix
checks are conservative 14-byte instruction boundaries, not an assumption that
all detour backends share X64InlineHook's implementation.

RSP is aligned at the site. RSI/RDI/RBP/R12-R15 remain live and are ABI-preserved;
volatile GPRs/flags are dead on the two audited continuations, and the containing
function has no XMM operands. The adapter reserves 0x30 bytes, reads original start
coordinates at the adjusted RSP+0xA0/+0xA8 before writing the argument slots at
RSP+0x20/+0x28, and passes manager R15 and 1-based unit ID ESI. A positive callback
branches to the original buffer initialization at 0x196585. Failure replays all
displaced instructions and returns through RedBird to 0x196659. The real emitted
73-byte body is decoded and checked, including its RIP-relative store.

Local branches and all hash-matched baseline cross-references show no entry into
the inline span's interior. Recheck full instruction/branch/ABI contracts on any
native or RedBird update. No runtime native patch, search or hook was optimized.

## Address inventory

Names retain the copied source's terminology, which is not an independent proof
of native parameter semantics. A further full feature audit must resolve remaining
semantic ambiguities before optimization. Source-relative line references identify
the authoritative pattern/byte validator or fixed-layout expression. Address-array
and inline-expression rows supplement named constants.

| Source symbol / derivation | Reference RVA | Source under src/ |
| --- | --- | --- |
'@
$text = $header + "`r`n" + (($rows | Select-Object -Unique) -join "`r`n") + "`r`n"
$expected = [regex]::Replace($text, '\r?\n', "`r`n")
$path = Join-Path $modDir 'UpdateToNewDLL.md'
[IO.File]::WriteAllText($path,$expected,[Text.UTF8Encoding]::new($false))
if (-not [string]::Equals([IO.File]::ReadAllText($path),$expected,[StringComparison]::Ordinal)) { throw 'Inventory write mismatch.' }
Write-Output "Wrote native update contracts and $($rows.Count) source-address rows."
