# Updating Some Settings for a new CrusaderDE.dll

## Audited baseline

- Steam build ID: `24530188`
- DLL size: `3450880` bytes
- SHA-256: `1E6D4C2E10CC35A7B8082A7E2BCD8BB20680EBEDA803D9B943257B948145CB2B`

The plugin reports every changed hash. Features with unprovable raw layouts
write timestamped Errors and remain inactive; independently signature-validated
features may continue.

## Hash-gated layouts

Revalidate these before approving a new hash:

- knight stable fields `GameUnit +0x3D2/+0x3DC` and the stable-horse-release AOB;
- tribe speed fields `GameTribe +0x542/+0x54E` and the native record relation
  documented as public pointer `+0x2A` / complete record `+0x56C`;
- troop movement detour fields including `+0x582`, `+0x65C`, `+0x660`, `+0x688`,
  `+0x914`, `+0x916`, `+0x930`, `+0x99E`, and `+0xA64`;
- quarry candidate fields `GameBuildingManager +0x31B7D0/+0x31B7D4` and the
  `setupBuildingEntrancesOffset` AOB/ABI;
- enemy-proximity ChoreManager flag `+0x870` and the cursor/range hook ABI.

## Independently validated native code

1. Require exactly one semantic match for all AI-economy, troop movement,
   quarry, knight, and assembly-point AOBs.
2. For assembly-point patches, verify every original opcode byte before writing
   and verify restoration behavior.
3. For church worker counts, require one table match and verify the complete
   Vanilla default table before changing it.
4. Revalidate stack/register assumptions in AI-economy and movement detours,
   even when their AOB still matches.
5. Test every setting both enabled and disabled, map reload, multiplayer packet
   formatters, knight mount/dismount, quarry relocation, and process-lifetime
   hook rooting.
6. Update the shared current hash only after the hash-gated layouts pass.

Missing or ambiguous signatures and failed byte validation must continue to log
Errors and leave the corresponding feature inactive.
