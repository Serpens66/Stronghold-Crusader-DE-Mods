# Python reference parser

`scde_map_parser.py` is the strict, read-only reference implementation for the
container format used by `MapParser.Core`. It was reduced from the existing
workspace parser: all replace, rewrite, build, set and serialization paths were
removed. The PKWARE-DCL decoder and the Kaitai format notes originate from that
reverse-engineering work.

The reader accepts only SCDE `.map` files. It derives 100/150/200 directory
slots from `(directoryTag - 36) / 20`, validates bounds, sizes, DCL output and
CRC32, and exposes `info`, `list`, `dump`, `validate` and a parity-oriented
`manifest` command.

Known tags `1076`, `2100` and `2108` retain their remainder as an opaque tail.
Some shipped maps contain a zero-filled, internally contradictory DCL
placeholder for section 1190. It is explicitly marked unavailable because no
decoder can reconstruct bytes absent from the file; all other sections remain
strictly validated.
