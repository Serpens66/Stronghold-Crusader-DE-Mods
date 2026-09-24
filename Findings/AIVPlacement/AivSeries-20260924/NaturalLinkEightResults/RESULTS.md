# Natural-link series, 2026-09-24

The eight configured runs completed in order. A ninth map start occurred after the
series ended and is a separate Reed Sea reverse-on repeat. The test mod previously
left the last AI lineup in the lobby after completion; this enabled the extra
start. Its replacement now clears AI slots on the next local lobby opening after
the final confirmed run and records that cleanup once.

Native CrusaderDE.dll SHA-256:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Installed Script Extender 2.10.1 SHA-256:
`85591256082C6F2329EDC0BFB0C1C163D9CF1BF2991DC8B11F6790907B60F2F2`.
The two map binaries and all eight used AIVJSON files are in `Assets`, with
their hashes in `Assets/SHA256SUMS.txt`. The complete append log, config,
progress and all raw trace files are retained here. `AllRaw` holds 134 start
trace TSVs (67 start calls with paired record tables), 27 prebuild trace TSVs,
and 334 fit cell/live-grid TSVs. No captured start reported a native failure
flag or snapshot failure.

| Runs | Oracle cases | Exact | Deliberately gray | Comparator mismatch |
| --- | ---: | ---: | ---: | ---: |
| Crossing forward/reverse, off/on (sessions 001-004) | 64 | 16 | 48 | 0 |
| Reed forward/reverse, off/on (sessions 005-008) | 66 | 9 | 49 | 8 |
| Extra Reed reverse-on repeat (session 009) | 17 | 4 | 13 | 0 |

The eight comparator mismatches are the four rotations of Sentinel Default 2
for player 3 in each Reed forward run (sessions 005 and 006). The native
sequential state differs from the comparator's reconstructed state after the
earlier Wolf start; session 006 also includes earlier AIV prebuild. The
comparison tool uses the **observed** selected earlier rotation and marker,
and its local preplacement guard does not prove all possible lobby outcomes
or all connected-record cleanup effects. This makes these eight an offline
reconstruction gap, not eight wrong lobby colors. The actual CastlePlanner
lobby log kept player 3 `NotEvaluable`: `StartOverlapUnproven` with prebuild
off, `PreBuildSequenceUnsupported` with prebuild on. Subsequent dependent
players were gray as well. Do not use the comparator's colored values for
these eight as a safe forecast.

The first Crossing forward-off capture shows a separate native start effect:
player 3's start cleared 101 occupied building cells from player 2, including
the 49-cell Keep record, its 49-cell camp and three linked cells. The goods
yard records survived. Both constructors reported success. This agrees with
the screenshot showing a flag and goods yard without a Keep. The game and
other mods were present, so a single repeat of exactly this preset with only
the five diagnostic dependencies is prepared to isolate mod interaction.

Confidence: high for run count, hashes, captured live cell changes, native
fit scores and the product's gray status; medium for attributing the first
Keep removal to unmodified Vanilla until the minimal-mod control is captured.
No general completed-castle or connected-record prediction is released from
these runs.
