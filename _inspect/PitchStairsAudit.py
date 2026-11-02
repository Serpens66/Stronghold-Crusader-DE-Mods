"""Read-only SHCDE pitch/stair audit; original code executes in a private process image.

Requires 64-bit Windows Python, pefile, ctypes and the exact native hash below.
No save file is consumed. The installed game image and live game are never patched.
A synthetic world is explicitly initialized; these are isolated native cases,
not complete live-game or multiplayer acceptance.
"""
import sys, pathlib, struct, hashlib
sys.dont_write_bytecode = True
DLL = pathlib.Path(r'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll')
EXPECTED = 'fbcb93195fc7efca9bdac5204852efdd76f9818f59a6711750d77c9cef2831e2'
assert sys.platform == 'win32' and struct.calcsize('P') == 8, '64-bit Windows required'
assert hashlib.sha256(DLL.read_bytes()).hexdigest() == EXPECTED, 'Unaudited native image; refusing execution'

def tid(x,y):
 first=y*(y+1) if y<400 else 320800-(800-y)*(801-y)
 return first+x-(399-y if y<400 else y-400)
def h16(b,o):return struct.unpack_from('<h',b,o)[0]
def u32(b,o):return struct.unpack_from('<I',b,o)[0]


import ctypes as ct,pefile
dllpath=r'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'
pe=pefile.PE(dllpath)
k=ct.windll.kernel32;k.VirtualAlloc.restype=ct.c_void_p;k.VirtualAlloc.argtypes=[ct.c_void_p,ct.c_size_t,ct.c_ulong,ct.c_ulong]
base=k.VirtualAlloc(None,pe.OPTIONAL_HEADER.SizeOfImage,0x3000,0x40)
if not base:raise OSError('VirtualAlloc')
ct.memmove(base,pe.header,len(pe.header))
for sec in pe.sections:
 data=sec.get_data();ct.memmove(base+sec.VirtualAddress,data,len(data))
delta=base-pe.OPTIONAL_HEADER.ImageBase
for block in pe.DIRECTORY_ENTRY_BASERELOC:
 for rel in block.entries:
  if rel.type==10:
   q=ct.c_uint64.from_address(base+rel.rva);q.value=(q.value+delta)&0xffffffffffffffff
def put(rva,data):ct.memmove(base+rva,bytes(data),len(data))
def uview(rva,n,f):return (f*n).from_address(base+rva)
# Original function uses eight row-dependent packed neighbor deltas.
rows=[]
inv=bytearray(320800*2)
for y in range(800):
 first=y*(y+1) if y<400 else 320800-(800-y)*(801-y)
 fx=399-y if y<400 else y-400
 width=2*y+2 if y<400 else 2*(800-y)
 rows.append(first-fx)
 for t in range(first,first+width):struct.pack_into('<H',inv,t*2,y)
put(0x3aae2a4,inv)
for y in range(800):
 a=(rows[y-1]-rows[y]) if y else 0
 b=(rows[y+1]-rows[y]) if y<799 else 0
 put(0x405edb0+y*32,struct.pack('<8i',a,a+1,1,b+1,b,b-1,-1,a-1))
 put(0x402ff2c+y*12,struct.pack('<i',rows[y]))
 first=y*(y+1) if y<400 else 320800-(800-y)*(801-y)
 put(0x402ff28+y*12,struct.pack('<i',first))
put(0x402ff28+800*12,struct.pack('<i',320800))
put(0x64ccbb0+0x50,struct.pack('<i',266))
put(0x60ad660,struct.pack('<i',6))
mask=uview(0x51890d0,320800,ct.c_ubyte)
nativeMask=ct.WINFUNCTYPE(ct.c_uint64,ct.c_void_p,ct.c_int,ct.c_uint)(base+0xd86f0)


# Fully specified synthetic field/building/owner inputs, independent of mutable save contents.
flags=uview(0x48f71b0,320800,ct.c_uint32);heights=uview(0x4ddd350,320800,ct.c_ubyte)
defaults=uview(0x4e2b870,320800,ct.c_ubyte)
ct.memset(base+0x4b6aa50,0,320800*2);ct.memset(base+0x64ccbb0+0x5c,0,4000*812);ct.memset(base+0x379af10,0,9*22588)
ct.memset(base+0x4ddd350,8,320800);ct.memset(base+0x4e2b870,8,320800)
for i in range(320800):flags[i]=0x8000
for y in range(800):
 fx=399-y if y<400 else y-400
 width=2*y+2 if y<400 else 2*(800-y)
 put(0x3a11ea4+y*800+fx,bytes([1])*width)
put(0x37edf3c+4,struct.pack('<i',1));put(0x64ccbb0+0x50,struct.pack('<i',1))
pitch=ct.WINFUNCTYPE(ct.c_uint64,ct.c_void_p,ct.c_short,ct.c_uint,ct.c_uint)(base+0x59380)
stair=ct.WINFUNCTYPE(None,ct.c_void_p,ct.c_int,ct.c_uint,ct.c_uint,ct.c_ushort)(base+0x6cb20)
prepare=ct.WINFUNCTYPE(ct.c_uint,ct.c_void_p,ct.c_int,ct.c_int,ct.c_int,ct.c_short,ct.c_int)(base+0x5cd90)
tilebase=base+0x405edb0
def reset():
 for y in range(385,416):
  for x in range(385,416):
   z=tid(x,y);heights[z]=8;defaults[z]=8;flags[z]=0x8000
 ct.memset(tilebase+0x2038e48,0,4000*20)
 put(0x60ad660+0x155f34,struct.pack('<i',0))
 put(0x405edb0+0x204c6c4,struct.pack('<i',1))
 put(0x405edb0+0x204e724,struct.pack('<i',255))
def make_stair(x,y,mapper):
 prepare(tilebase,1,x,y,mapper,1)
 stair(tilebase,1,x,y,mapper)

for withpitch in [False,True]:
 reset()
 if withpitch:print('pitch-id',pitch(tilebase,1,400,400),flush=True)
 make_stair(399,400,184);make_stair(400,400,185)
 for x,y in [(399,400),(400,400),(400,401)]:nativeMask(base+0x60ad660,y,tid(x,y))
 assert heights[tid(400,400)] == (28 if withpitch else 24)
 assert not (flags[tid(400,400)] & 8)
 assert bool(mask[tid(400,400)]&0x10) == (not withpitch)
 assert bool(mask[tid(400,401)]&1) == (not withpitch)
 print('SYNTH_AIV_OVER_PITCH',withpitch,'height',heights[tid(400,400)],'flags',hex(flags[tid(400,400)]),'foot_to_ground',bool(mask[tid(400,400)]&0x10),'ground_to_foot',bool(mask[tid(400,401)]&1),'higher',heights[tid(399,400)],flush=True)


slow=ct.WINFUNCTYPE(ct.c_int,ct.c_void_p,ct.c_uint,ct.c_uint,ct.c_uint,ct.c_uint,ct.c_int,ct.c_int)(base+0xdafd0)
fast=ct.WINFUNCTYPE(ct.c_int,ct.c_void_p,ct.c_uint,ct.c_uint,ct.c_uint,ct.c_uint,ct.c_int,ct.c_int)(base+0xda590)
# Two-cell corridor; corrected direction masks, original stored pitch height.
for pitched in [False,True]:
 reset()
 for i in range(320800):flags[i]=0x80;mask[i]=0
 a=tid(400,400);z=tid(400,399)
 flags[a]=0x8008 if pitched else 0x8000;flags[z]=0x8900
 heights[a]=8;heights[z]=24
 nativeMask(base+0x60ad660,400,a);nativeMask(base+0x60ad660,399,z)
 if pitched:heights[a]=4
 results=[]
 for source,dest in [((400,400),(400,399)),((400,399),(400,400))]:
  results.append((fast(base+0x60ad660,*source,*dest,200,0),slow(base+0x60ad660,*source,*dest,200,0)))
 assert results == ([(1,0),(1,0)] if pitched else [(1,1),(1,1)])
 print('MASK_FIXED_READERS','pitch' if pitched else 'control','masks',hex(mask[a]),hex(mask[z]),'DA590_DAFD0',results,flush=True)
# Actual complete manual stair construction, optionally pitch on only the top step cell.
manual=ct.WINFUNCTYPE(None,ct.c_void_p,ct.c_int,ct.c_uint,ct.c_uint,ct.c_uint,ct.c_int,ct.c_short,ct.c_int)(base+0x6f230)
for pitched in [False,True]:
 for i in range(320800):flags[i]=0x8000
 reset()
 w=tid(399,400);flags[w]=0x8100;heights[w]=98
 put(0x3665fc8,struct.pack('<i',1))
 if pitched:pitch(tilebase,1,400,400)
 manual(tilebase,1,400,400,404,400,27,10)
 for x in range(400,405):nativeMask(base+0x60ad660,400,tid(x,400))
 assert ct.c_int.from_address(tilebase+0x204e6e4).value == 0
 assert [heights[tid(x,400)] for x in range(400,405)] == ([86,66,50,34,18] if pitched else [82,66,50,34,18])
 assert bool(mask[tid(400,400)]&4) == (not pitched)
 assert bool(mask[tid(401,400)]&64) == (not pitched)
 print('MANUAL_OVER_PITCH',pitched,'validation',ct.c_int.from_address(tilebase+0x204e6e4).value,'height',[heights[tid(x,400)] for x in range(400,405)],'flags',[hex(flags[tid(x,400)]) for x in range(400,405)],'top_to_next',bool(mask[tid(400,400)]&4),'next_to_top',bool(mask[tid(401,400)]&64),flush=True)


# Direct building accessibility with a connected, lower pitch entrance.
access=ct.WINFUNCTYPE(ct.c_int,ct.c_void_p,ct.c_int,ct.c_int)(base+0xc90e0)
bb=ct.WINFUNCTYPE(ct.c_int,ct.c_void_p,ct.c_int,ct.c_int,ct.c_int)(base+0xbb3d0)
pc=uview(0x50ec690,320800,ct.c_ushort)
for pitched in [False,True]:
 reset()
 ct.memset(base+0x64ccbb0+0x5c,0,2*812)
 rec=0x64ccbb0+0x5c+812
 for off,v in [(0xd0,2),(0xd2,7),(0xd6,1),(0xec,24),(0xee,400),(0xf0,400),(0xfe,403),(0x100,401)]:
  put(rec+off,struct.pack('<H',v))
 put(rec+0xf8,struct.pack('<i',3))
 put(0x379af10+0x583c+0xa0,struct.pack('<i',tid(410,410)))
 for y in range(395,408):
  for x in range(395,408):
   i=tid(x,y);pc[i]=1;flags[i]=0x8008 if pitched else 0x8000;heights[i]=4 if pitched else 8
   if 400<=x<=402 and 400<=y<=402:heights[i]=24;defaults[i]=24;flags[i]=0x8000
 pc[tid(410,410)]=1
 expected_access = 0 if pitched else 1
 assert access(base+0x64ccbb0,1,0) == expected_access
 assert bb(base+0x64ccbb0,1,0,0) == expected_access
 print('BUILDING_DIRECT_LOWER_ENTRANCE','pitch' if pitched else 'control','C90E0',access(base+0x64ccbb0,1,0),'BB3D0',bb(base+0x64ccbb0,1,0,0),'same_pcl',pc[tid(403,401)],flush=True)
