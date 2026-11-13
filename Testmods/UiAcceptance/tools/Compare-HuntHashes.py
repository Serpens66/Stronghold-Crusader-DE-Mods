"""Compares the HUNT_HASH lines of a host and a client log.

Usage: Compare-HuntHashes.py <host log> <client log>
Exit code 0 only when both logs share ticks and every shared tick has the same hash.
"""
import re
import sys

LINE = re.compile(r"HUNT_HASH tick=(-?\d+) hash=([0-9A-F]{16}) units=(\d+) hunters=(\d+) targeting=(\d+) meat=([\d,]+)")


def read(path):
    result = {}
    with open(path, encoding="utf-8", errors="replace") as handle:
        for line in handle:
            match = LINE.search(line)
            if match:
                result[int(match.group(1))] = match.groups()[1:]
    return result


host, client = read(sys.argv[1]), read(sys.argv[2])
shared = sorted(set(host) & set(client))
different = [tick for tick in shared if host[tick][0] != client[tick][0]]
print(f"host={len(host)} client={len(client)} shared={len(shared)} different={len(different)}")
if shared:
    last = host[shared[-1]]
    print(f"last tick={shared[-1]} units={last[1]} hunters={last[2]} targeting={last[3]} meat={last[4]}")
if different:
    tick = different[0]
    print(f"first difference at tick {tick}: host={host[tick]} client={client[tick]}")
sys.exit(0 if shared and not different else 1)
