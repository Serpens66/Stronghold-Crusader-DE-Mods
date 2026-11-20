"""Extract compact, analyzer-compatible historical evidence. This tool never deletes sources."""
import argparse
import collections
import datetime as dt
import hashlib
import json
from pathlib import Path
import re
import struct

HEADER = struct.Struct('<IHHqqqq10i')
NAME = re.compile(r'^\d{8}-\d{6}-\d{3}-p\d+-s(\d+)$')
STREAMS = ('events.bin', 'snapshots.bin', 'checkpoints.bin', 'checkpoints.previous.bin', 'markers.bin', 'evidence.bin')
KNOWN = set(STREAMS) | {'manifest.json', 'summary.txt', 'manifest.json.tmp'}


def frames(source):
    with source.open('rb', buffering=1024*1024) as stream:
        while header := stream.read(HEADER.size):
            if len(header) != HEADER.size:
                return
            size, version, kind, seq, session, _, _, tick, phase, op, player, command, planned, unit, identity, stage, length = HEADER.unpack(header)
            if version != 2 or length < 0 or length > 256 * 1024 * 1024 or size != HEADER.size + length:
                raise ValueError(f'invalid frame in {source.name} at {stream.tell()-HEADER.size}')
            yield stream, header, (kind, tick, phase, op, stage, unit, planned, length)


def inventory(source):
    if source.is_symlink() or getattr(source.lstat(), 'st_file_attributes', 0) & 1024:
        raise ValueError(f'linked package {source}')
    result = []
    for path in sorted(source.iterdir()):
        stat = path.lstat()
        if path.name not in KNOWN or not path.is_file() or path.is_symlink() or getattr(stat, 'st_file_attributes', 0) & 1024:
            raise ValueError(f'unrecognized package entry {path}')
        result.append(dict(name=path.name, bytes=stat.st_size, mtimeUtcTicks=stat.st_mtime_ns//100+621355968000000000))
    return result


def archive(source, target):
    original = inventory(source)
    manifest = json.loads((source/'manifest.json').read_text(encoding='utf-8-sig'))
    if manifest.get('plugin') != 'DesyncTest_Serp' or manifest.get('format') != 2 or str(manifest.get('session')) != NAME.fullmatch(source.name)[1]:
        raise ValueError(f'unrecognized manifest {source}')
    incident_ticks, repaired_units = set(), set()
    summary = source/'summary.txt'
    if summary.is_file():
        repaired_units.update(int(s) for s in re.findall(r'\bUnit opcode=56 stage=[04] unit=(\d+)',summary.read_text(encoding='utf-8-sig')))
    for name in ('events.bin', 'markers.bin'):
        path = source/name
        if not path.exists():
            continue
        for stream, _, (kind, tick, phase, op, stage, unit, planned, length) in frames(path):
            if kind == 4 and phase == 0 and op == 54 and stage == 0:
                incident_ticks.add(tick)
            stream.seek(length, 1)
    if manifest.get('incidents', 0) and not incident_ticks:
        raise ValueError(f'no readable incident marker for {source}')
    target.mkdir()
    kept = collections.Counter()
    for name in STREAMS:
        path = source/name
        if not path.exists():
            continue
        with (target/name).open('xb') as output:
            for stream, header, (kind, tick, phase, op, stage, unit, planned, length) in frames(path):
                near = any(center-1200 <= tick <= center+200 for center in incident_ticks)
                focused = unit in repaired_units and any(center-400 <= tick <= center+40 for center in incident_ticks)
                marker = kind in (1, 2, 11, 12, 13) or (kind == 4 and phase == 0 and op in (54, 67, 89))
                # Keep compact aggregate controls even when there was no native repair.
                keep = marker or (not incident_ticks and kind == 5)
                if kind == 3 and op == 106 and any(center-1200 <= planned <= center+200 for center in incident_ticks):
                    keep = True
                if near:
                    keep |= kind in (5, 6, 8, 14, 15, 16, 17, 18, 19)
                    keep |= kind in (9, 10) and focused
                    keep |= kind in (4, 7) and op != 120
                if keep:
                    payload = stream.read(length)
                    if len(payload) != length:
                        break
                    output.write(header)
                    output.write(payload)
                    kept[kind] += 1
                else:
                    stream.seek(length, 1)
    if summary.is_file():
        (target/'summary.txt').write_bytes(summary.read_bytes())
    (target/'manifest.json').write_text(json.dumps(dict(manifest, complete=False, archive=dict(
        originalComplete=manifest.get('complete'), repairTicks=sorted(incident_ticks), beforeTicks=1200, afterTicks=200,
        keptByType=kept, sourceFiles=original)), indent=2), encoding='utf-8')
    if inventory(source) != original:
        raise ValueError(f'source changed during archival: {source}')
    files = []
    for path in sorted(target.iterdir()):
        with path.open('rb') as stream:
            files.append(dict(name=path.name, bytes=path.stat().st_size, sha256=hashlib.file_digest(stream,'sha256').hexdigest()))
    return dict(folder=source.name, originals=original, archiveFiles=files, repairTicks=sorted(incident_ticks),
                sourceBytes=sum(v['bytes'] for v in original), archiveBytes=sum(v['bytes'] for v in files))


def main():
    cli = argparse.ArgumentParser(description=__doc__)
    cli.add_argument('source', type=Path)
    cli.add_argument('destination', type=Path)
    cli.add_argument('--before', required=True, help='ISO 8601 UTC cutoff for last change')
    args = cli.parse_args()
    cutoff = dt.datetime.fromisoformat(args.before).timestamp()
    args.destination.mkdir(parents=True, exist_ok=False)
    results = []
    for source in sorted(args.source.iterdir()):
        if not source.is_dir() or not NAME.fullmatch(source.name):
            continue
        try:
            if any(p.stat().st_mtime >= cutoff for p in source.iterdir()):
                continue
            item = archive(source, args.destination/source.name)
            results.append(item)
            print(f"{source.name}: {item['sourceBytes']} -> {item['archiveBytes']} bytes", flush=True)
        except (OSError, ValueError) as exc:
            print(f'{source.name}: preserved source ({exc})', flush=True)
    (args.destination/'archive-index.json').write_text(json.dumps(results, indent=2), encoding='utf-8')


if __name__ == '__main__':
    main()
