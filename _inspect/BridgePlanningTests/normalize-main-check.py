from pathlib import Path
import hashlib
p=Path('BugfixesAndQoL/Test-DrawbridgeDestructionFix.ps1')
old=p.read_bytes();text=old.decode('utf-8-sig');bom=b'\xef\xbb\xbf' if old.startswith(b'\xef\xbb\xbf') else b''
expected=bom+text.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8')
assert p.read_bytes()==old
p.write_bytes(expected)
assert p.read_bytes()==expected and expected.decode('utf-8-sig').replace('\r\n','\n')==text.replace('\r\n','\n')
assert b'\n' not in expected.replace(b'\r\n',b'')
print('PASS content-identical CRLF',str(p),'beforeSHA256',hashlib.sha256(old).hexdigest(),'afterSHA256',hashlib.sha256(expected).hexdigest())
