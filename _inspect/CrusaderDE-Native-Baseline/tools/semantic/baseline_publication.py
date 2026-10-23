"""Cooperating writer lock and compare-before-replace protection for baseline manifests."""
from contextlib import contextmanager
import hashlib
import os
from pathlib import Path


def fingerprint(path):
    path=Path(path)
    return hashlib.sha256(path.read_bytes()).hexdigest() if path.exists() else None


@contextmanager
def publication_guard(manifest, expected, *, pointer=None, expected_pointer=None):
    manifest=Path(manifest)
    root=next((p for p in manifest.parents if (p/'CURRENT.json').exists()),manifest.parent)
    lock=root/'BASELINE.publication.lock'
    stream=lock.open('a+b');locked=False
    try:
        if stream.tell()==0:stream.write(b'\0');stream.flush()
        stream.seek(0)
        try:
            if os.name=='nt':
                import msvcrt
                msvcrt.locking(stream.fileno(),msvcrt.LK_NBLCK,1)
            else:
                import fcntl
                fcntl.flock(stream.fileno(),fcntl.LOCK_EX|fcntl.LOCK_NB)
            locked=True
        except OSError as error:
            raise RuntimeError('Another baseline writer is publishing; retry using the retained candidate') from error
        if fingerprint(manifest)!=expected:
            raise RuntimeError('Baseline manifest changed during analysis; candidate retained, newer knowledge was not replaced')
        if pointer is not None and fingerprint(pointer)!=expected_pointer:
            raise RuntimeError('CURRENT identity changed during analysis; candidate retained')
        yield
    finally:
        if locked:
            stream.seek(0)
            if os.name=='nt':
                import msvcrt
                msvcrt.locking(stream.fileno(),msvcrt.LK_UNLCK,1)
            else:
                import fcntl
                fcntl.flock(stream.fileno(),fcntl.LOCK_UN)
        stream.close()
