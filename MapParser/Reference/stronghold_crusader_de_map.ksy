meta:
  id: stronghold_crusader_de_map
  title: Stronghold Crusader Definitive Edition map container
  file-extension: map
  endian: le
  license: MIT
doc: |
  Read-only documentation of the SCDE .map container implemented by
  scde_map_parser.py and MapParser.Core. Directory capacity is derived from
  the directory tag; 1076, 2100 and 2108 are known non-standard variants.
seq:
  - id: magic
    type: u4
    valid: 0xfffffffe
  - id: radar
    type: sized_block
  - id: description
    type: sized_block
  - id: u1
    type: sized_block
  - id: u2
    type: sized_block
  - id: u3
    type: sized_block
  - id: u4
    type: sized_block
  - id: restart
    type: restart_block
    if: u4.size != 0
  - id: directory_tag
    type: u4
  - id: directory
    type: directory_body(directory_tag)
    size: directory_tag - 4
    if: directory_tag == 2036 or directory_tag == 3036 or directory_tag == 4036
  - id: opaque_tail
    size-eos: true
types:
  sized_block:
    seq:
      - id: size
        type: u4
      - id: data
        size: size
  restart_block:
    seq:
      - id: size
        type: u4
      - id: data
        size: size
      - id: terminator
        type: u4
        if: size > 0
  directory_body:
    params:
      - id: tag
        type: u4
    seq:
      - id: payload_size
        type: u4
      - id: section_count
        type: u4
      - id: format_version
        type: u4
      - id: reserved
        type: u4
        repeat: expr
        repeat-expr: 4
      - id: uncompressed_sizes
        type: u4
        repeat: expr
        repeat-expr: capacity
      - id: stored_sizes
        type: u4
        repeat: expr
        repeat-expr: capacity
      - id: section_ids
        type: u4
        repeat: expr
        repeat-expr: capacity
      - id: compression_flags
        type: u4
        repeat: expr
        repeat-expr: capacity
      - id: payload_offsets
        type: u4
        repeat: expr
        repeat-expr: capacity
      - id: trailer
        type: u4
    instances:
      capacity:
        value: (tag - 36) / 20
        doc: 100 for tag 2036, 150 for 3036, and 200 for 4036.
