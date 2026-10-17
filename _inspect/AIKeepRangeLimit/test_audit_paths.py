"""Resolve the manifest-selected database without reading or changing a real baseline."""
import argparse
import json
import pathlib
from audit import resolve_database


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--fixture', required=True)
    base = pathlib.Path(parser.parse_args().fixture) / 'resolver'
    source, shared = base / 'source', base / 'shared'
    index = {'currentNativeHash': 'fixture-native', 'databaseManifest': 'sem/current/DATABASE_INFO.json'}
    manifest = {'identities': {'currentNativeHash': 'fixture-native'},
                'database': {'path': 'sem/current/analysis/current.sqlite', 'schemaVersion': 6}}

    def text(path, value):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(value), encoding='utf-8')

    def reset():
        for folder in (source, shared):
            text(folder / 'CURRENT.json', index)
            text(folder / index['databaseManifest'], manifest)
        database = shared / manifest['database']['path']
        database.parent.mkdir(parents=True, exist_ok=True)
        database.write_bytes(b'resolver-only fixture')
        return database.resolve()

    def rejected():
        try:
            resolve_database(shared, source)
        except (ValueError, FileNotFoundError):
            return
        raise AssertionError('Invalid or mismatched baseline was accepted')

    expected = reset()
    assert resolve_database(shared, source)[2] == expected
    assert not (shared / 'sem/current/CrusaderDE-semantic.sqlite').exists()
    text(shared / 'CURRENT.json', dict(index, currentNativeHash='different'))
    rejected()
    reset()
    text(shared / index['databaseManifest'], dict(manifest, database={'path': 'other.sqlite'}))
    rejected()
    reset().unlink()
    rejected()
    reset()
    for folder in (source, shared):
        text(folder / 'CURRENT.json', dict(index, databaseManifest='../outside.json'))
    rejected()
    reset()
    escaped = dict(manifest, database={'path': '../outside.sqlite'})
    for folder in (source, shared):
        text(folder / index['databaseManifest'], escaped)
    rejected()
    reset()
    wrong = dict(manifest, identities={'currentNativeHash': 'different'})
    for folder in (source, shared):
        text(folder / index['databaseManifest'], wrong)
    rejected()
    print('PASS: manifest database selection; source/identity mismatch, missing file and escaping paths rejected (7 cases).')


if __name__ == '__main__':
    main()
