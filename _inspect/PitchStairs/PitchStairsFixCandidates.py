"""Compare pitch/stair fix concepts in a PRIVATE native image; no production patch.

Run separately with --mode logical or --mode producer.
The original audit remains unchanged. Neither the installed DLL nor a live game
is modified. Height-read redirection is a test proxy for per-read normalization,
not a proposed shadow-grid runtime implementation. No ABI/RedBird/MP acceptance.
"""
import argparse, pathlib, struct, json
args = argparse.ArgumentParser()
args.add_argument('--mode', choices=['logical', 'producer'], required=True)
mode = args.parse_args().mode
source = pathlib.Path(__file__).with_name('PitchStairsAudit.py').read_text(encoding='utf-8')
source = source.split('for withpitch in [False,True]:')[0]
source = source.replace('SizeOfImage,0x3000', 'SizeOfImage+0x100000,0x3000')
exec(compile(source, 'PitchStairsAudit setup', 'exec'), globals())
import capstone
shadow_rva = (pe.OPTIONAL_HEADER.SizeOfImage + 0xfff) & ~0xfff
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
md.detail = True

def checked_patch(rva, before, after):
    assert ct.string_at(base+rva, len(before)) == before, hex(rva)
    assert len(before) == len(after)
    put(rva, after)

# Fresh AIV height no longer contains a pitch depression in either concept.
checked_patch(0x6cced, bytes.fromhex('80843aa0e5d70004'), b'\x90'*8)
manual_add = bytes.fromhex('4380842ca0e5d70004')
assert len(manual_add) == 9
read_sites = []
if mode == 'logical':
    # Redirect ONLY audited comparison readers to a normalized input plane.
    # Production would calculate the value at each read and store no shadow grid.
    for rva, size in [(0xd86f0,1515),(0xdafd0,1656),(0xc90e0,551),(0xbb3d0,1639),(0xe1640,1512)]:
        for ins in md.disasm(pe.get_data(rva,size), base+rva):
            for op in ins.operands:
                if op.type == capstone.x86.X86_OP_MEM and op.mem.disp == 0x4ddd350:
                    assert ins.disp_size == 4
                    put(ins.address-base+ins.disp_offset, struct.pack('<i',shadow_rva))
                    read_sites.append(hex(ins.address-base))
    assert len(read_sites) == 15
    # Private arithmetic prototype: SI is the mapper at this original add site.
    # Keep ordinary walls' +4; skip only stairs (mapper 27). No production hook.
    gateway = shadow_rva+0x60000
    continuation = 0x6f6bf
    code = bytearray.fromhex('9c6683fe1b740f9d') + manual_add
    def jump_from(offset, dest):
        return b'\xe9'+struct.pack('<i',dest-(offset+5))
    code += jump_from(gateway+len(code),continuation)
    code += b'\x9d'
    code += jump_from(gateway+len(code),continuation)
    put(gateway,code)
    checked_patch(0x6f6b6,manual_add,jump_from(0x6f6b6,gateway)+b'\x90'*4)
else:
    # Concept B keeps actual pitch ground height unchanged from creation onward.
    checked_patch(0x594cb,bytes.fromhex('41888c30a0e5d700'),b'\x90'*8)
    # No pitch depression exists in this concept, including for ordinary walls.
    checked_patch(0x6f6b6,manual_add,b'\x90'*9)

def sync_plane():
    if mode != 'logical':
        return
    plane = bytearray(ct.string_at(base+0x4ddd350,320800))
    for i in range(320800):
        if flags[i]&8 and heights[i] == max(0,int(defaults[i])-4):
            plane[i] = defaults[i]
    put(shadow_rva,plane)

slow = ct.WINFUNCTYPE(ct.c_int,ct.c_void_p,ct.c_uint,ct.c_uint,ct.c_uint,ct.c_uint,ct.c_int,ct.c_int)(base+0xdafd0)
fast = ct.WINFUNCTYPE(ct.c_int,ct.c_void_p,ct.c_uint,ct.c_uint,ct.c_uint,ct.c_uint,ct.c_int,ct.c_int)(base+0xda590)
manual = ct.WINFUNCTYPE(None,ct.c_void_p,ct.c_int,ct.c_uint,ct.c_uint,ct.c_uint,ct.c_int,ct.c_short,ct.c_int)(base+0x6f230)
access = ct.WINFUNCTYPE(ct.c_int,ct.c_void_p,ct.c_int,ct.c_int)(base+0xc90e0)
bb = ct.WINFUNCTYPE(ct.c_int,ct.c_void_p,ct.c_int,ct.c_int,ct.c_int)(base+0xbb3d0)
pc = uview(0x50ec690,320800,ct.c_ushort)
reconstruct = ct.WINFUNCTYPE(None,ct.c_void_p,ct.c_uint,ct.c_uint,ct.c_uint,ct.c_uint,ct.c_int)(base+0xe1640)
path_buffer = ct.create_string_buffer(2048)
put(0x60ad660+0x155f60,struct.pack('<Q',ct.addressof(path_buffer)))
put(0x3665fc8,struct.pack('<i',1))
results = {}
def reset_world():
    ct.memset(base+0x4ddd350,8,320800)
    ct.memset(base+0x4e2b870,8,320800)
    ct.memset(base+0x51890d0,0,320800)
    ct.memset(base+0x4b6aa50,0,320800*2)
    ct.memset(base+0x64ccbb0+0x5c,0,4000*812)
    ct.memset(base+0x379af10,0,9*22588)
    for i in range(320800):
        flags[i] = 0x8000
        pc[i] = 1
    reset()
    sync_plane()

# Both native search variants and directions agree for pitch before a stair.
reset_world()
for i in range(320800):flags[i] = 0x80
floor,step = tid(400,400),tid(400,399)
flags[floor],flags[step] = 0x8000,0x8900
heights[step] = 24
pitch(tilebase,1,400,400)
actual_pitch = heights[floor]
assert actual_pitch == (4 if mode=='logical' else 8)
sync_plane()
nativeMask(base+0x60ad660,400,floor);nativeMask(base+0x60ad660,399,step)
routes = [(fast(base+0x60ad660,*a,*z,200,0),slow(base+0x60ad660,*a,*z,200,0))
          for a,z in [((400,400),(400,399)),((400,399),(400,400))]]
assert routes == [(1,1),(1,1)]
assert heights[floor] == actual_pitch
results['pitch_stair_both_searches_both_directions'] = routes
results['stored_pitch_height'] = actual_pitch
reconstructed = []
for a,z in [((400,400),(400,399)),((400,399),(400,400))]:
    assert slow(base+0x60ad660,*a,*z,200,0) == 1
    put(0x60ad660+0x155f68,struct.pack('<i',0))
    before = ct.c_int.from_address(base+0x60ad660+0x78).value
    reconstruct(base+0x60ad660,*a,*z,2)
    count = ct.c_int.from_address(base+0x60ad660+0x155f68).value
    assert count == 1 and ct.c_int.from_address(base+0x60ad660+0x78).value == before
    reconstructed.append(count)
results['mode2_reconstruction_without_repair'] = reconstructed

# Clamping at zero and the highest representable ordinary 16-unit stair pair.
for ground in [0,1,2,3,4,8,40,239]:
    reset_world()
    for i in range(320800):flags[i] = 0x80
    flags[floor],flags[step] = 0x8000,0x8900
    heights[floor] = defaults[floor] = defaults[step] = ground
    heights[step] = ground+16
    pitch(tilebase,1,400,400)
    assert heights[floor] == (max(0,ground-4) if mode=='logical' else ground)
    sync_plane()
    nativeMask(base+0x60ad660,400,floor);nativeMask(base+0x60ad660,399,step)
    assert mask[floor]&1 and mask[step]&0x10, ground
    assert slow(base+0x60ad660,400,400,400,399,200,0) == 1, ground
    assert slow(base+0x60ad660,400,399,400,400,200,0) == 1, ground
results['clamped_and_elevated_ground_levels'] = [0,1,2,3,4,8,40,239]

# Genuine 20-unit edge without pitch remains blocked.
flags[floor] = 0x8000;heights[floor] = 8;heights[step] = 28
sync_plane()
nativeMask(base+0x60ad660,400,floor);nativeMask(base+0x60ad660,399,step)
assert not (mask[floor]&1) and not (mask[step]&0x10)
assert slow(base+0x60ad660,400,400,400,399,200,0) == 0
results['genuine_height_20_remains_blocked'] = True

# AIV replacement creates the nominal height, not nominal+4.
reset_world()
pitch(tilebase,1,400,400);sync_plane()
make_stair(399,400,184);sync_plane()
make_stair(400,400,185);sync_plane()
for x,y in [(399,400),(400,400),(400,401)]:nativeMask(base+0x60ad660,y,tid(x,y))
assert heights[tid(400,400)] == 24
assert not(flags[tid(400,400)]&8)
assert mask[tid(400,400)]&0x10 and mask[tid(400,401)]&1
results['aiv_over_pitch_height'] = heights[tid(400,400)]

# Manual stairs: only the stair-specific redundant restoration is removed in A.
reset_world()
flags[tid(399,400)] = 0x8100;heights[tid(399,400)] = 98
pitch(tilebase,1,400,400);sync_plane()
manual(tilebase,1,400,400,404,400,27,10);sync_plane()
assert [heights[tid(x,400)] for x in range(400,405)] == [82,66,50,34,18]
for x in range(400,405):nativeMask(base+0x60ad660,400,tid(x,400))
assert mask[tid(400,400)]&4 and mask[tid(401,400)]&64
results['manual_over_pitch_heights'] = [heights[tid(x,400)] for x in range(400,405)]

# Ordinary manual walls must still end at their nominal height in both concepts.
reset_world()
pitch(tilebase,1,400,400);sync_plane()
manual(tilebase,1,400,400,400,400,25,10)
assert heights[tid(400,400)] == 98
results['ordinary_manual_wall_height'] = heights[tid(400,400)]

# Independent building access: corrected tile readers agree with logical height.
reset_world()
rec = 0x64ccbb0+0x5c+812
for off,v in [(0xd0,2),(0xd2,7),(0xd6,1),(0xec,24),(0xee,400),(0xf0,400),(0xfe,403),(0x100,401)]:
    put(rec+off,struct.pack('<H',v))
put(rec+0xf8,struct.pack('<i',3))
put(0x379af10+0x583c+0xa0,struct.pack('<i',tid(410,410)))
for y in range(395,408):
    for x in range(395,408):
        i = tid(x,y)
        if 400<=x<=402 and 400<=y<=402:
            heights[i] = defaults[i] = 24
        else:
            pitch(tilebase,1,x,y)
sync_plane()
got = [access(base+0x64ccbb0,1,0),bb(base+0x64ccbb0,1,0,0)]
assert got == [1,1]
results['building_boundary_C90E0_BB3D0'] = got
print(json.dumps({'mode':mode,'read_sites':read_sites,'results':results,'scope':'isolated-concept-test-only'},sort_keys=True))
