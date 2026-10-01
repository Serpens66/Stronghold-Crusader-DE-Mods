# Retired standalone raid testmod (0.1.1)

The tested correction is integrated into BugfixesAndQoL 1.0.174, controlled by its default-on host setting EnableAiRaidRetargetFix. This source tree, local package, native audit and retained logs remain a reference. Its build driver deliberately refuses installation; do not load a second copy of the raid observation hook alongside the mainmod.

Canonical ongoing regression tests compile the mainmod sources in BugfixesAndQoL/tests/AiRaidRetarget.Tests. Native provenance, integration ownership, lifecycle and remaining gameplay/multiplayer acceptance are documented in BugfixesAndQoL/UpdateToNewDLL.md and the RAID_RETARGET Native-Baseline. The former installed package is moved into .inspect/RaidRetargetEvaluation/ArchivedInstalledTestmod-0.1.1-<timestamp> only after the new mainmod installation is verified.
