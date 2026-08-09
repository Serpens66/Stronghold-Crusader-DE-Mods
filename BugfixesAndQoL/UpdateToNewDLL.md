# Updating Bugfixes and QoL / Extra Features for a new CrusaderDE.dll

## Audited baseline

- Steam build ID: `24530188`
- DLL size: `3450880` bytes
- SHA-256: `1E6D4C2E10CC35A7B8082A7E2BCD8BB20680EBEDA803D9B943257B948145CB2B`

The plugin checks the hash silently. Only enabled features with unprovable raw
layouts write one timestamped Error and remain inactive. Independently
signature- and byte-validated features continue without a known hash.

## Plague audit

- The direct event handler sets the player's plague timer at resource offset
  `+0x22AC` to 24, but does not reset the independent healing progress at
  `+0x22C4`. Regular scenario events and RandomEvents both reach this Vanilla
  path.
- Vanilla popularity begins at `-150` internal points (`-6` in the UI) and
  changes only in 25-point steps according to that healing progress. An
  apothecary cure increments the progress, while natural `Disease` projectile
  expiry does not. This explains both the permanent penalty and later events
  inheriting completed healing progress.
- One invocation of the herd function at RVA `0xD1780` selects one player-owned
  building and creates 6-10 `Disease` projectiles. The fix records their slot
  and global IDs as one herd and keeps it active until none remain alive.
- All Vanilla plague branches converge at RVA `0xCB52C`. Before the overwritten
  report write, `AX` holds Vanilla's signed plague component, `EDX` holds the
  running popularity result after that component, and `R14D` holds the player
  ID. The fix removes only that component and replaces it with `-25` per active
  herd; subsequent Vanilla factors and clamping remain unchanged.

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

1. Revalidate the plague herd-creation detour at RVA `0xD1780` and the common
   plague-popularity exit at RVA `0xCB52C`. On the audited DLL the herd function
   creates one group of 6-10 `Disease` projectiles around the Vanilla-selected
   building. At the popularity exit, `AX` contains Vanilla's signed plague
   modifier, `EDX` contains popularity after that modifier, and `R14D` contains
   the player ID. Both AOBs must match exactly once.
2. Require exactly one semantic match for all AI-economy, troop movement,
   quarry, knight, and assembly-point AOBs.
3. For assembly-point patches, verify every original opcode byte before writing
   and verify restoration behavior.
4. For church worker counts, require one table match and verify the complete
   Vanilla default table before changing it.
5. Revalidate stack/register assumptions in AI-economy and movement detours,
   even when their AOB still matches.
6. Test every setting both enabled and disabled, map reload, multiplayer packet
   formatters, knight mount/dismount, quarry relocation, and process-lifetime
   hook rooting.
7. Update the shared current hash only after every fixed layout listed above
   passes. Do not gate independently validated features on that hash.

Missing or ambiguous signatures and failed byte validation must continue to log
Errors and leave the corresponding feature inactive.
