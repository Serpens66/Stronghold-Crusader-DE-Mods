# Popularity contribution parameters

Audit: 2026-10-16. Installed Native SHA-256:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
The installed file matches CURRENT.json and the current-hash function records.
Local SE source commit `5908e1f12437deb7ef5f5b1dc2d64e301178fba3`, tree
`c238441ea12db35ee36c5c5668d3c8fce0897295`, matches database provenance.
Installed SHCDESE assembly version: 2.14.1.0. Its GameGlobalsManager declarations
were independently decompiled and the consumer type-checked against that DLL.

## Audited path and scope

Current-hash semantic records: resource scheduler 0xD1AD0 calls popularity updater
0xCB090 outside the excluded app modes. The updater rebuilds church/cathedral
counts through 0xCCD20, iterates player IDs 1..8, and checks player eligibility.
Special app-mode branches assign fixed popularity; small-population protection
can assign 7500 instead of executing the component block. These bypasses remain
authoritative; configuring a contribution does not force those branches to run.

The ordinary component block selects ration, food-variety, crowding and tax
contributions, followed by fair, blessing/building, ale and fear contributions.
The SE's existing inline replacement reads 41 ManagedValue<int> parameters for
these contributions, 13 separate coverage/ratio thresholds, and publishes
OnPlayerCalculatePopularity(Pre). This event is before subsequent native event
penalties and the final clamp, not a final-popularity notification.

The names above are semantic role descriptions. Generic database function names
retain their candidate classification. The component branches, constants and
managed replacement are supported by the current-hash decompiler records and
the matching SE implementation; no new hook span or instruction claim is made.

## Display scale and defaults

Total popularity uses 100 internal units per displayed point (0..10000 internally).
Component reports instead divide their signed contribution by 25; this is directly
visible in the current managed FatControler consumer. Configuring component points
therefore multiplies by 25, not 100.

| Components | Defaults in report points |
|---|---|
| Rations None/Half/Full/Extra/Double | -8/-4/0/+4/+8 |
| No preferred/available food; no population | -8; +8 |
| Food variety exactly 2/3/4 types | +1/+2/+3 |
| Crowding 101..120/121..140/141..160/161..180/>180% | -2/-4/-6/-8/-10 |
| Taxes modes HugeDonation through Cruellest | +7/+5/+3/+1/-2/-4/-6/-8/-12/-16/-20/-24 |
| Donation without positive gold; unknown mode | +1; -24 |
| Fair event flag active | +16 |
| Blessing 25..49/50..74/75..94/>=95% | +2/+4/+6/+8 |
| At least one church; at least one cathedral | +1; +2 (additive, not per building) |
| Ale coverage 25..49/50..74/75..99/100% | +2/+4/+6/+8 |
| Fear coefficient, multiplied by signed fear boost | +1 per level |

Ration-mode contribution requires positive population and preferred-food state;
otherwise the separate fallback applies. Food variety is added independently.
Donation selection requires positive gold for modes below None. Thresholds use
strict > for religion (24/49/74/94), >= for ale (25/50/75/100), and strict > for
overpopulation (100/120/140/160/180). Ale coverage helper 0xCEA90 computes the
coverage from inn state and population and clamps to 0..100. Configuring the
coefficient does not change those producers or thresholds.

## Downstream effects and acceptance limits

The replacement updates the normal contribution report fields, the current
popularity accumulator and its worst-source selection, then resumes the remaining
native path. Plague and other timed events continue afterwards; the existing
BugfixesAndQoL plague correction at 0xCB57C remains outside this mod's ownership.
Vanilla clamps the final total to 0..10000 and retains its popularity-change
speech/history decisions. Report producer 0x19D960 and FatControler consume these
values; reporting is not a separate manually maintained mod calculation.

Religion also adds `(uint)combinedReligionContribution / 25` to
`r_UnknownPopularityAccumulationByCurrentDividedBy25`. The SE preserves this
unsigned native bookkeeping expression. A negative configured religion sum
therefore has a large unsigned quotient in that auxiliary counter. Its gameplay
meaning is unconfirmed; do not claim that negative religion values preserve all
unrelated statistics. PopularityTest intentionally exposes the existing direct SE
parameters and adds no correction hook. Negative modifiers' complete in-game
effects and report presentation still require runtime acceptance.

No game UI or real multiplayer acceptance is inferred from source/type checks.
PopularityTest's independent catalog/UI checks compare all 41 SE defaults and
exercise signed settings, persistence and anonymous-stack ownership separately.
