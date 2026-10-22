"""Offline authoring only. Capture knowledge without modifying APIShared or the baseline.

Source-mentioned fields enter as metadata only. Only explicit REVIEWED entries can
grant bounded reads. Existing storage IDs are append-only in catalog-identities.json.
Run from the workspace with the documented portable Python; runtime has no Python/JSON dependency.
"""
import hashlib
import json
import pathlib
import re
import subprocess

ROOT = pathlib.Path(__file__).resolve().parents[3]
HERE = pathlib.Path(__file__).resolve().parent
OUTPUT = ROOT / "Findings/src/NativeKnowledge"
BASE = ROOT / "_inspect/CrusaderDE-Native-Baseline"
CURRENT = json.loads((BASE / "CURRENT.json").read_text(encoding="utf-8-sig"))
SEM = BASE / CURRENT["semanticDirectory"]
MANIFEST = json.loads((BASE / CURRENT["databaseManifest"]).read_text(encoding="utf-8-sig"))
HASH = CURRENT["currentNativeHash"]
# These reviewed offsets were audited for this image, never for whichever image is
# current during a future run. A changed native image needs a new explicit audit.
AUDITED_NATIVE_HASH = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2"
if HASH != AUDITED_NATIVE_HASH:
    raise RuntimeError("New native image: re-audit REVIEWED/OFFSET_NOTES; do not relabel old bindings")
if HASH != MANIFEST["identities"]["currentNativeHash"]:
    raise RuntimeError("Baseline identities disagree")
NATIVE = pathlib.Path(r"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll")
with NATIVE.open("rb") as stream:
    if hashlib.file_digest(stream, "sha256").hexdigest().upper() != HASH:
        raise RuntimeError("Installed native image is not the captured baseline")
SOURCE = ROOT / "shcde-script-extender"
SOURCE_COMMIT = subprocess.check_output(["git", "-C", str(SOURCE), "rev-parse", "HEAD"], text=True).strip()

SCALARS = {"Byte": ("UInt8", 1), "SByte": ("Int8", 1), "UInt16": ("UInt16", 2),
           "Int16": ("Int16", 2), "UInt32": ("UInt32", 4), "Int32": ("Int32", 4),
           "UInt64": ("UInt64", 8), "Int64": ("Int64", 8), "Single": ("Float32", 4),
           "float": ("Float32", 4), "double": ("Float64", 8)}
for alias, canonical in {"byte":"Byte", "sbyte":"SByte", "short":"Int16", "ushort":"UInt16", "int":"Int32", "uint":"UInt32", "long":"Int64", "ulong":"UInt64"}.items():
    SCALARS[alias] = SCALARS[canonical]
FIELDS = {}
SIZES = {}
TYPE_FILES = {}
ENUM_TYPES = {}
for path in (SOURCE / "src").rglob("*.cs"):
    for enum in re.finditer(r"\benum\s+(\w+)\s*(?::\s*([\w.]+))?\s*\{", path.read_text(encoding="utf-8-sig")):
        underlying = (enum[2] or "int").split(".")[-1]
        underlying = {"short":"Int16", "ushort":"UInt16", "int":"Int32", "uint":"UInt32", "byte":"Byte", "sbyte":"SByte", "long":"Int64", "ulong":"UInt64"}.get(underlying, underlying)
        ENUM_TYPES[enum[1]] = SCALARS.get(underlying, ("Unknown", 0))
DECL = re.compile(r"public\s+(?:unsafe\s+)?(?:fixed\s+)?([\w.]+)\s+(\w+)\s*(?:\[(\d+)\])?\s*;[^\n]*?//\s*(0x[0-9A-Fa-f]+)")
for path in sorted((SOURCE / "src/SHCDESE.BepInEx/Interop").glob("*.cs")):
    text = path.read_text(encoding="utf-8-sig")
    match = re.search(r"\bstruct\s+(\w+)", text)
    if not match:
        continue
    owner = match[1]
    size = re.search(r"\bSize\s*=\s*(0x[\da-fA-F]+|\d+)", text)
    SIZES[owner] = int(size[1], 0) if size else 0
    TYPE_FILES[owner] = path
    for item in DECL.finditer(text):
        raw, width = SCALARS.get(item[1], ENUM_TYPES.get(item[1].split(".")[-1], ("Unknown", 0)))
        FIELDS[(owner, item[2])] = dict(owner=owner, sourceName=item[2], raw=raw, width=width,
            offset=int(item[4], 16), count=int(item[3] or 1), path=path,
            line=text[:item.start()].count("\n") + 1)

DOCS = {p.name: p for p in sorted((SEM / "knowledge").glob("*.md"))}
ROWS = {}

def evidence(path, kind, first, last=None, summary=""):
    root = SOURCE if path.is_relative_to(SOURCE) else BASE
    return dict(kind=kind, source=("extender/" if root == SOURCE else "baseline/") + path.relative_to(root).as_posix(),
        sha256=hashlib.sha256(path.read_bytes()).hexdigest().upper(), first=first, last=last or first, summary=summary)

def source_row(owner, name):
    f = FIELDS[(owner, name)]
    identity = (owner, f["offset"], f["raw"], f["count"], f["width"])
    if identity not in ROWS:
        source_evidence = evidence(f["path"], "ExtenderSource", f["line"], summary="Captured source layout/name only; not independent native semantic or read validation.")
        ROWS[identity] = dict(owner=owner, offset=f["offset"], raw=f["raw"], count=f["count"], stride=f["width"],
            size=SIZES.get(owner, 0), name=name, confidence="Candidate", description="Source-named field mentioned by a current Knowledge document; native meaning/access not automatically established.",
            aliases=[owner + "." + name, name], evidence=[source_evidence], verified=False, related=[], extraMeanings=[])
    return ROWS[identity]

# Exact qualified references, not fuzzy topic matching. Incidental method names cannot become fields.
for document, path in DOCS.items():
    for line_number, line in enumerate(path.read_text(encoding="utf-8-sig").splitlines(), 1):
        for owner, name in re.findall(r"\b(Game\w+|AivSystem)\.(\w+)\b", line):
            if (owner, name) in FIELDS:
                row = source_row(owner, name)
                row["evidence"].append(evidence(path, "KnowledgeReference", line_number,
                    summary="Mention only. Read the full feature audit; this reference does not promote field confidence."))

# Each reviewed item is deliberately scalar/array-specific. Offsets are record-relative machine
# contracts, not domain IDs. A source-type mismatch is resolved explicitly rather than renamed away.
REVIEWED = [
 ("GameUnit",0x29C,"UInt16",1,"DeathMarker","Confirmed",True,"GATEHOUSE_LIVING_CAPTURE.md",7,21,
  "Low 16-bit death marker, set for melee as well as projectile death. AliveState can remain IsAlive. Adjacent +0x29E is separate storage."),
 ("GameUnit",0x88,"Int16",1,"LifeStateRaw","Confirmed",True,"GATEHOUSE_LIVING_CAPTURE.md",10,21,
  "Raw signed AliveState encoding; not alone proof of combat life, slot identity or ownership."),
 ("GameUnit",0x92,"UInt16",1,"ControllingPlayerWord","Confirmed",True,"GATEHOUSE_LIVING_CAPTURE.md",53,64,
  "Complete 16-bit controlling-player word. Native MOVSX consumers reinterpret through signed Int16; this reader returns raw unsigned bits."),
 ("GameUnit",0x2A0,"UInt16",1,"CaptureEligibilityWord","Probable",True,"GATEHOUSE_LIVING_CAPTURE.md",7,18,
  "Nonzero word gates gatehouse capture and healer-list inclusion. Broader classification and all writers are not established; it is not a life predicate."),
 ("GameUnit",0x3C4,"UInt32",1,"CurrentHealthRaw","Confirmed",True,"HEALTH_BAR_RENDERING.md",5,17,
  "Current unsigned HP storage; damage/healing update cached percentage/bar separately. Positive HP is not a universal life/identity predicate."),
 ("GameUnit",0x3C8,"UInt32",1,"MaximumHealthRaw","Confirmed",True,"HEALTH_BAR_RENDERING.md",5,17,
  "Maximum unsigned HP; initialization 0x19A240 sets current/max from type. No write or recomputed bar is provided."),
 ("GameUnit",0x34,"UInt32",1,"CachedHealthBarBlocks","Confirmed",True,"HEALTH_BAR_RENDERING.md",13,17,
  "Cached bar-block count used by rendering; damage/healing paths refresh it. Not a replacement for HP storage."),
 ("GameUnit",0x2BC,"Int16",1,"AiStateRaw","Confirmed",False,"OUTPOSTS.md",45,51,
  "Raw type-dependent AI state. Maceman death interpretation is scoped; no universal state enum or complete field-width proof is inferred from selected writes."),
 ("GameUnit",0x450,"UInt16",1,"HealerTargetUnitId","Probable",False,"BEDOUIN_HEALER_TARGETS.md",5,10,
  "Healer updater's one-based target unit ID; target global identity/injury/range checks remain separate. Other unit-type uses need their own audit."),
 ("GameUnit",0xB6,"Int16",1,"StandingBaseHeightRaw","Confirmed",False,"PLAGUE_HEIGHT.md",27,38,
  "Standing-height input at native slot+0x712; actual standing height combines correction and special movement/surface branches. Signedness/complete storage access remains withheld."),
 ("GameUnit",0xB8,"Int16",1,"StandingHeightCorrection","Confirmed",False,"PLAGUE_HEIGHT.md",27,38,
  "Signed vertical correction at native slot+0x714. Do not substitute a tile height for the complete standing-plane calculation."),
 ("GameBuilding",0xEC,"Int16",1,"RigidBuildingHeight","Confirmed",False,"DRAWBRIDGES.md",16,35,
  "Common rigid building-height reference written by allocator and consumed by rendering/unit placement. Not the mutable tile height; complete scalar signedness remains withheld."),
 ("GameBuilding",0x10C,"Int16",1,"CurrentHealthRaw","Confirmed",True,"HEALTH_BAR_RENDERING.md",5,13,
  "Current HP is consumed as a signed word by the building render paths; not a universal alive predicate."),
 ("GameBuilding",0x10E,"UInt16",1,"MaximumHealthRaw","Confirmed",True,"HEALTH_BAR_RENDERING.md",5,13,
  "Maximum HP is consumed as an unsigned word. Raw view deliberately does not inherit signedness from an interop display name."),
 ("GameBuilding",0x2A8,"UInt32",1,"SharedBuildingLinkWord","Probable",True,"AI_ECONOMY_OBSERVATION.md",20,27,
  "Nonzero dword links compound building parts for accessibility propagation; siege-only interpretation is too narrow. Source/header historical names can refer to neighboring storage."),
 ("GameTribe",0x30,"UInt16",1,"LeaderUnitGameId","Confirmed",True,"ENEMY_GATE_BUILDING_CONTEXT.md",59,72,
  "One-based unit game ID. Native address uses the manager's reserved-zero record; public span lookup uses gameId-1 exactly once."),
 ("GameTribe",0x622,"UInt16",1,"StoredTargetBuildingGameId","Confirmed",True,"RAID_RETARGET.md",7,18,
  "Stored one-based building ID; command 9 and other writers update it. A changed/still-stored target is not proof that a fresh melee search executed."),
 ("GameTribe",0x626,"UInt32",1,"StoredTargetBuildingGlobalId","Confirmed",True,"RAID_RETARGET.md",7,18,
  "Global identity paired with the stored target building game ID; not a second pair at +0x64C/+0x650 (those are manager-relative)."),
 ("GamePlayerResources",0x50C,"Int32",1,"GoldRaw","Confirmed",True,"DEFEAT_LOOT.md",3,9,
  "Signed gold balance; defeat rewards, purchases, sales and tax settlement have independent writers. A raw read does not imply a completed transaction."),
 ("GamePlayerResources",0x35BC,"UInt16",100,"RaidCandidateBuildingIds","Confirmed",True,"RAID_RETARGET.md",13,16,
  "Bounded 100-element candidate storage relative to the public player-resource pointer. Elements are building game IDs; only the current count is meaningful. Native origin is +0x5C ahead."),
 ("GamePlayerResources",0x3684,"UInt32",1,"RaidCandidateBuildingCount","Confirmed",True,"RAID_RETARGET.md",13,16,
  "Current candidate count at public-pointer+0x3684, not native-origin+0x3628. The raw reader does not clamp the observed count or infer valid entries."),
 ("GamePlayerResources",0x21F8,"UInt32",1,"LordUnitGameId","Confirmed",True,"PLAYER_DEFEAT.md",15,23,
  "One-based Lord unit game ID. Validate current global identity/life separately; save loading supplies baseline state, not a new defeat transition."),
 ("GamePlayerResources",0x2210,"UInt32",1,"PlayerPauseWord","Probable",False,"PLAYER_DEFEAT.md",15,23,
  "Upstream r_IsPaused in the defeat audit; exact pause semantics and width are not independently established here."),
 ("GamePlayerResources",0x2244,"UInt32",1,"WinLossStateRaw","Confirmed",True,"PLAYER_DEFEAT.md",15,23,
  "Raw win/loss state periodically written via 0xCDE60 -> 0x1D160; save restore is a separate writer. No state-number interpretation is imposed."),
]

for owner, offset, raw, count, name, confidence, verified, document, first, last, description in REVIEWED:
    width = next(value[1] for value in SCALARS.values() if value[0] == raw)
    identity = (owner, offset, raw, count, width)
    row = ROWS.get(identity)
    if row is None:
        row = dict(owner=owner, offset=offset, raw=raw, count=count, stride=width, size=SIZES.get(owner, 0),
            aliases=[], evidence=[], related=[], extraMeanings=[])
        ROWS[identity] = row
    row.update(name=name, confidence=confidence, verified=verified, description=description)
    row["evidence"].append(evidence(DOCS[document], "NativeAudit", first, last,
        "Feature audit establishes only the scoped description; gameplay acceptance is independent."))
    # Same bytes/type can be discovery aliases. A differently sized source field is a relation instead.
    for (source_owner, source_name), f in FIELDS.items():
        if source_owner == owner and f["offset"] == offset and f["raw"] == raw and f["count"] == count:
            row["aliases"].extend([source_owner + "." + source_name, source_name])
            row["evidence"].append(evidence(f["path"], "ExtenderSource", f["line"], summary="Current declaration captured separately from native semantics."))
    if row["size"] <= 0:
        row["verified"] = False

# Offset-only relationships from full feature reports are retained even when a complete
# scalar/extent audit has not been performed. Never turn source typing into read permission.
OFFSET_NOTES = [
 ("GameUnit", 0x2A2, "NearestEnemyDistanceRaw", "SE_2_14_1_ENGAGE_RANGE.md", 7, 11, "Distance producers use distinct metrics and signed 16-bit comparisons despite public UInt16 storage; no universal distance meaning or range policy is imposed."),
 ("GameBuilding", 0x2BE, "OnFireCounter", "FIREMAN_TARGETING.md", 9, 23, "Nonzero fire counter read by selector 0xB8F40; extinguishing 0xC3C80 clears it. Struct origin is native slot+0x5C."),
 ("GameUnit", 0x39A, "FiremanTargetBuildingId", "FIREMAN_TARGETING.md", 15, 23, "Fireman states 3/4 validate and consume target building ID with paired global identity; other unit contexts are not inferred."),
 ("GameUnit", 0x39C, "FiremanTargetBuildingGlobalId", "FIREMAN_TARGETING.md", 15, 23, "Global identity paired with the fireman's target; field meaning is scoped to this updater."),
 ("GameUnit", 0xB2, "HealerWorldPositionX", "BEDOUIN_HEALER_TARGETS.md", 5, 10, "World coordinate consumed by healer selection, not automatically a tile coordinate."),
 ("GameUnit", 0xB4, "HealerWorldPositionY", "BEDOUIN_HEALER_TARGETS.md", 5, 10, "World coordinate consumed by healer selection, not automatically a tile coordinate."),
 ("GameUnit", 0x2FA, "WorkerWorkMode", "WORKSHOP_IDLE_DELAY.md", 20, 34, "Native manager-relative +0x956 minus record origin 0x65C. Entertainment can reuse AI state 1; this is a separate work-mode gate."),
 ("GameUnit", 0x334, "WorkerWorkshopId", "WORKSHOP_IDLE_DELAY.md", 20, 34, "Native manager-relative +0x990 minus record origin 0x65C; ID alone cannot establish current workshop identity."),
 ("GameUnit", 0x364, "WorkerWorkshopGlobalId", "WORKSHOP_IDLE_DELAY.md", 20, 34, "Native manager-relative +0x9C0 minus record origin 0x65C; paired workshop global identity."),
 ("GameUnit", 0x37C, "WorkerAnimationVariant", "WORKSHOP_IDLE_DELAY.md", 20, 34, "Native manager-relative +0x9D8 minus record origin 0x65C. r_TicksSinceRestPulse is reused by this worker path; do not assume universal rest timing."),
 ("GameUnit", 0x426, "OutpostAiTribeRole", "OUTPOSTS.md", 16, 31, "Outpost writes the AI tribe role; compatibility with every special unit type is not established."),
 ("GameUnit", 0xAC, "RecruitmentRallyField", "OUTPOSTS.md", 16, 31, "Outpost production clears this recruitment-rally field. Do not infer a universal command context."),
 ("GamePlayerResources", 0x46C, "PendingIncomingGoodsTable", "DEFEAT_LOOT.md", 5, 9, "Base of twenty incoming-goods totals. No array type/extent is granted from the first source declaration alone."),
]
outpost_names = ["ProfileMask", "ActiveTribeId", "ActiveTribeGlobalId", "ProductionCounter", "TargetGroupSize", "ActiveProfile", "SizeSetting", "ProductionDelay", "CatapultFlag", "VicinityInitialized", "GroupAcceleration", "SpawnAcceleration"]
for offset, name in zip([0x300,0x302,0x304,0x308,0x30A,0x30C,0x30E,0x310,0x312,0x314,0x316,0x318], outpost_names):
    OFFSET_NOTES.append(("GameBuilding", offset, "Outpost" + name, "OUTPOSTS.md", 12, 20,
        "Outpost-specific field; native allocator/ABB90 update flow is documented. Source scalar typing alone is not a complete read-access audit."))
for owner, offset, name, document, first, last, description in OFFSET_NOTES:
    matches = [f for (o,n),f in FIELDS.items() if o == owner and f["offset"] == offset]
    if not matches:
        raise RuntimeError("Offset-only knowledge has no current source layout: " + owner + hex(offset))
    row = source_row(owner, matches[0]["sourceName"])
    row.update(name=name, confidence="Probable", description=description)
    row["evidence"].append(evidence(DOCS[document], "NativeAudit", first, last, "Scoped offset relationship; read access remains withheld pending complete scalar/extent validation."))

# The source supplement is later than the generated baseline: preserve both provenances.
supplement = DOCS["SE_2_14_3_INTEROP_ADDITIONS.md"]
for owner, name in [("GamePlayerResources", "r_AILastStandPhase"), ("GameUnit", "r_CanAutoEngageInMelee"),
                    ("GameUnit", "r_AttackingUnitGlobalId")] + [("GameTribe", "r_LastRangedAttackerGlobalId" + str(i)) for i in range(1, 11)]:
    row = source_row(owner, name)
    row["description"] = "Upstream 2.14.3 interpretation, not an independently closed Vanilla semantic/access audit. See the source supplement, not just the older generated types."
    row["evidence"].append(evidence(supplement, "ExtenderSource", 15, 50, "Explicit source-only supplement; new four-byte Tribe slots are not aliases of their old upper halves."))

# Preserve the old halfword identities as non-readable historical knowledge, not value-converting aliases.
for i in range(1, 11):
    current = source_row("GameTribe", "r_LastRangedAttackerGlobalId" + str(i))
    offset = current["offset"]
    for delta, suffix in [(0, "FormerLowWord"), (2, "FormerUpperWord")]:
        identity = ("GameTribe", offset + delta, "UInt16", 1, 2)
        ROWS[identity] = dict(owner="GameTribe", offset=offset + delta, raw="UInt16", count=1, stride=2,
            size=SIZES["GameTribe"], name=suffix + str(i), confidence="Candidate", verified=False,
            description="Historical 2.14.2 halfword declaration, replaced in source by a four-byte field. No independent semantic/read validation and no alias conversion.",
            aliases=["2.14.2:GameTribe." + ("unk" if delta == 0 else "r_LastRangedAttackerGlobalId") + str(i)],
            evidence=[evidence(supplement, "ExtenderSource", 27, 46, "Historical halfword layout; related to, not identical with, the new UInt32 field.")],
            related=[], extraMeanings=[])

ROWS[("GameUnit", 0x29C, "UInt32", 1, 4)] = dict(owner="GameUnit", offset=0x29C, raw="UInt32", count=1, stride=4,
    size=SIZES["GameUnit"], name="FormerCombinedDeathAndAdjacentWord", confidence="Candidate", verified=False,
    description="Historical N0000019A combined the death low word and unrelated adjacent high word. Not an alias of the current death marker and never automatically narrowed.",
    aliases=["historical:GameUnit.N0000019A", "N0000019A"],
    evidence=[evidence(DOCS["GATEHOUSE_LIVING_CAPTURE.md"], "NativeAudit", 49, 58, "Historical combined storage; native combat checks use only the low 16 bits.")],
    related=[], extraMeanings=[])

# Reject the over-specific death cause without losing the exact-storage source alias.
death_identity = ("GameUnit", 0x29C, "UInt16", 1, 2)
ROWS[death_identity]["extraMeanings"].append(dict(name="ProjectileDeathOnly", confidence="Rejected",
    description="Melee writer 0x199110 also sets this marker. The upstream name cannot establish projectile causation.",
    evidence=[evidence(DOCS["GATEHOUSE_LIVING_CAPTURE.md"], "NativeAudit", 49, 58, "Melee counter-evidence to the projectile-only interpretation.")]))

ledger_path = HERE / "catalog-identities.json"
ledger = json.loads(ledger_path.read_text()) if ledger_path.exists() else {"schemaVersion": 1, "records": {}, "fields": {}}
def storage_key(identity):
    owner, offset, raw, count, stride = identity
    return f"{owner}|{offset:X}|{raw}|{count}|{stride}"

for owner in sorted({r["owner"] for r in ROWS.values()}):
    if owner not in ledger["records"]:
        ledger["records"][owner] = "nkr-" + str(len(ledger["records"]) + 1).zfill(6)
for identity in sorted(ROWS):
    key = storage_key(identity)
    if key not in ledger["fields"]:
        ledger["fields"][key] = "nkf-" + str(len(ledger["fields"]) + 1).zfill(6)

for identity, row in ROWS.items():
    row["id"] = ledger["fields"][storage_key(identity)]
    row["recordId"] = ledger["records"][row["owner"]]
    for other, other_row in ROWS.items():
        if other != identity and other_row["owner"] == row["owner"]:
            # Only relate overlapping known extents; never merge identities on overlapping addresses.
            a_end = row["offset"] + max(1, row["stride"]) * row["count"]
            b_end = other_row["offset"] + max(1, other_row["stride"]) * other_row["count"]
            if row["offset"] < b_end and other_row["offset"] < a_end:
                row["related"].append(ledger["fields"][storage_key(other)])

def write(path, text):
    path.parent.mkdir(parents=True, exist_ok=True)
    expected = text.replace("\r\n", "\n").replace("\r", "\n").replace("\n", "\r\n")
    path.write_bytes(expected.encode("utf-8"))
    if path.read_bytes().decode("utf-8") != expected:
        raise RuntimeError("Write verification failed: " + str(path))

def cs(s): return json.dumps(s, ensure_ascii=True)
def ev(e):
    return "new NativeFieldEvidence(NativeEvidenceKind.%s, %s, %s, %d, %d, %s)" % (e["kind"],cs(e["source"]),cs(e["sha256"]),e["first"],e["last"],cs(e["summary"]))
def meaning(name, confidence, description, items):
    return "new NativeFieldMeaning(%s, NativeMeaningConfidence.%s, %s, %s)" % (cs(name),confidence,cs(description),", ".join(ev(e) for e in items))

rows = sorted(ROWS.values(), key=lambda r:r["id"])
lines = ["// Captured offline evidence. Do not infer native behavior from source names or reconstructed types.",
    "// IDs are append-only in the authoring ledger; do not regenerate identities from names or offsets.",
    "using System.Collections.Generic;", "", "namespace APIShared.NativeKnowledge", "{",
    "    public sealed partial class NativeFieldCatalog", "    {",
    "        /// <summary>Native image used for this captured knowledge; not a claim about a running process.</summary>",
    "        public const string CapturedNativeHash = " + cs(HASH) + ";",
    "        /// <summary>Captured analysis identity, for evidence provenance only.</summary>",
    "        public const string CapturedAnalysisGeneration = " + cs(MANIFEST["analysis"]["generation"]) + ";",
    "        /// <summary>Generated-baseline Extender provenance, distinct from the later source supplement.</summary>",
    "        public const string BaselineExtenderCommit = " + cs(MANIFEST["identities"]["scriptExtenderCommit"]) + ";",
    "        /// <summary>Source checkout inspected while capturing declarations; no version-only admission.</summary>",
    "        public const string CapturedSourceCommit = " + cs(SOURCE_COMMIT) + ";",
    "", "        private static IEnumerable<NativeFieldDescriptor> CreateFields()", "        {"]
for row in rows:
    # Deduplicate source coordinates, retaining independently scoped items.
    evidence_items = list({json.dumps(e, sort_keys=True):e for e in row["evidence"]}.values())
    meanings = [meaning(row["name"], row["confidence"], row["description"], evidence_items)]
    meanings += [meaning(m["name"], m["confidence"], m["description"], m["evidence"]) for m in row["extraMeanings"]]
    aliases = sorted(set(row["aliases"] + [row["owner"] + "." + row["name"]]))
    lines += ["            // " + row["owner"] + "+0x%X" % row["offset"] + ": " + row["name"],
        "            yield return new NativeFieldDescriptor(" + cs(row["id"]) + ", " + cs(row["recordId"]) + ", " + cs(row["name"]) + ",",
        "                " + cs(row["description"]) + ",",
        "                new string[] { " + ", ".join(map(cs, aliases)) + " },",
        "                new string[] { " + ", ".join(map(cs, sorted(set(row["related"])))) + " },",
        "                new NativeFieldMeaning[] { " + ", ".join(meanings) + " },",
        "                new NativeFieldBinding[] { new NativeFieldBinding(CapturedNativeHash, %d, 0x%X, NativeScalarType.%s, %d, %d, %s, %s, %s) });" % (
            row["size"],row["offset"],row["raw"],row["count"],row["stride"],str(row["verified"]).lower(),
            cs("Audited bounded raw read; no gameplay outcome or pointer-validity guarantee." if row["verified"] else "Metadata only. Source/reconstruction/partial feature evidence does not grant a read contract."),
            ", ".join(ev(e) for e in evidence_items))]
lines += ["        }", "    }", "}", ""]
write(OUTPUT / "NativeFieldCatalog.Data.cs", "\n".join(lines))

ids = ["namespace APIShared.NativeKnowledge", "{", "    /// <summary>Permanent field IDs. Neutral member names survive improvements to semantic names.</summary>", "    public static class NativeFieldIds", "    {"]
for row in rows:
    ids += ["        /// <summary>" + row["owner"] + " storage, currently described as " + row["name"] + "; see catalog evidence.</summary>",
        "        public const string F" + row["id"].split("-")[1] + " = " + cs(row["id"]) + ";"]
ids += ["    }", "", "    /// <summary>Record family identities; views must begin at the documented origin.</summary>", "    public static class NativeRecordTypes", "    {"]
for owner, record_id in ledger["records"].items():
    ids += ["        /// <summary>" + owner + " public interop-view origin; no manager prefix or ID arithmetic is implied.</summary>",
            "        public const string " + owner + " = " + cs(record_id) + ";"]
ids += ["    }", "}", ""]
write(OUTPUT / "NativeFieldIds.cs", "\n".join(ids))
write(ledger_path, json.dumps(ledger, indent=2) + "\n")
snapshot = dict(capturedAt="2026-10-22", nativeHash=HASH, analysis=MANIFEST["analysis"],
    baselineIdentities=MANIFEST["identities"], sourceCommit=SOURCE_COMMIT,
    fields=rows, documents=[dict(source=p.relative_to(BASE).as_posix(), sha256=hashlib.sha256(p.read_bytes()).hexdigest().upper()) for p in DOCS.values()])
write(HERE / "catalog-evidence.json", json.dumps(snapshot, indent=2) + "\n")
print(json.dumps(dict(fields=len(rows), readVerified=sum(r["verified"] for r in rows), recordFamilies=len(ledger["records"]),
    documents=len(DOCS), owners=sorted(ledger["records"])), indent=2))
