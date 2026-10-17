import sqlite3, pathlib, json, hashlib, struct, argparse, os


def resolve_database(base, expected_base):
    base = pathlib.Path(base).resolve()
    expected_base = pathlib.Path(expected_base).resolve()
    def load(folder, relative):
        target = (folder / relative).resolve()
        target.relative_to(folder)  # Reject metadata paths escaping their baseline root.
        return json.loads(target.read_text(encoding='utf-8-sig'))
    index = load(base, 'CURRENT.json')
    expected = load(expected_base, 'CURRENT.json')
    if index != expected:
        raise ValueError('Selected source and shared baseline identify different CURRENT metadata')
    manifest = load(base, index['databaseManifest'])
    if manifest != load(expected_base, expected['databaseManifest']):
        raise ValueError('Selected source and shared baseline database manifests differ')
    if manifest['identities']['currentNativeHash'] != index['currentNativeHash']:
        raise ValueError('CURRENT and database manifest native identities differ')
    database = (base / manifest['database']['path']).resolve()
    database.relative_to(base)
    if not database.is_file():
        raise FileNotFoundError('Semantic database missing; use Build-SemanticBaseline.ps1 RestoreDatabase')
    return index, manifest, database


def main():
    root = pathlib.Path(__file__).resolve().parents[2]
    local_base = root / '_inspect/CrusaderDE-Native-Baseline'
    parser = argparse.ArgumentParser()
    parser.add_argument('--baseline-root', default=os.environ.get('SHCDE_NATIVE_BASELINE_ROOT', str(local_base)))
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    index, manifest, database = resolve_database(args.baseline_root, local_base)
    output = pathlib.Path(args.output).resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    db = sqlite3.connect(database.as_uri() + '?mode=ro', uri=True)
    metadata = dict(db.execute('SELECT key,value FROM metadata'))
    if metadata['current_native_hash'] != index['currentNativeHash'] or int(metadata['schema_version']) != manifest['database']['schemaVersion']:
        raise ValueError('Database metadata differs from the selected manifest')
    db.row_factory = sqlite3.Row
    rows = list(db.execute("select * from xrefs where upper(to_rva) between '0XEEF90' and '0XEEF99'"))
    assert rows and all(int(row['to_rva'], 16) == 0xeef90 for row in rows)
    assert all(row['binary_hash'] == index['currentNativeHash'] for row in rows)
    print('PASS: xrefs (including data/function references) enter only at EEF90:', [dict(row) for row in rows])
    native = pathlib.Path(r'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll').read_bytes()
    assert hashlib.sha256(native).hexdigest().upper() == index['currentNativeHash']
    pe = struct.unpack_from('<I', native, 0x3c)[0]
    count, optional = struct.unpack_from('<H', native, pe+6)[0], struct.unpack_from('<H', native, pe+20)[0]
    for i in range(count):
        header = pe + 24 + optional + 40*i
        size, rva, rawsize, raw = struct.unpack_from('<IIII', native, header+8)
        flags = struct.unpack_from('<I', native, header+36)[0]
        if flags & 0x20000000:
            section = native[raw:raw+rawsize]
            for offset in range(len(section)-6):
                length = 0
                if section[offset] in (0xe8, 0xe9):
                    length = 5
                elif section[offset] == 0x0f and 0x80 <= section[offset+1] <= 0x8f:
                    length = 6
                if length:
                    target = rva + offset + length + struct.unpack_from('<i', section, offset+length-4)[0]
                    assert not 0xeef90 < target < 0xeef9a, ('Potential external rel32 edge into entry', hex(rva+offset))
        if rva <= 0xeef90 < rva + size:
            entry = native[raw+0xeef90-rva:raw+0xeef90-rva+317]
            print('function sha:', hashlib.sha256(entry).hexdigest().upper())
            output.write_bytes(entry)
    assert entry[:10].hex() == '48895c240848896c2410'
    print('PASS: executable-section rel32 candidate scan: no incoming interior edge; complete 317-byte body captured for Iced tests.')
    db.close()


if __name__ == '__main__':
    main()
