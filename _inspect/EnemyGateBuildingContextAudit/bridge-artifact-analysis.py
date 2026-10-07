"""Validate schema/hash/sections, then replay through the production C# shadow adapter."""
from pathlib import Path
import argparse,hashlib,struct,subprocess

def decode(path):
    path=Path(path)
    if path.suffix!='.bin':raise ValueError('unpublished partial artifact')
    raw=path.read_bytes()
    if len(raw)<64 or raw[-8:]!=b'BRGEND01':raise ValueError('missing completion footer')
    payload=struct.unpack_from('<q',raw,len(raw)-16)[0]
    if payload!=len(raw)-48:raise ValueError('payload extent mismatch')
    if hashlib.sha256(raw[:payload]).digest()!=raw[-48:-16]:raise ValueError('payload SHA256 mismatch')
    if raw[:8]!=b'BRGINP01' or struct.unpack_from('<i',raw,8)[0] not in (1,2):raise ValueError('unsupported schema')
    size=struct.unpack_from('<i',raw,12)[0]
    if not 0<=size<=65536 or 16+size>payload:raise ValueError('metadata extent')
    meta=dict(line.split('=',1) for line in raw[16:16+size].decode('utf8').splitlines())
    sections={};p=16+size
    while p<payload:
        if p+4>payload:raise ValueError('section header truncated')
        length=struct.unpack_from('<i',raw,p)[0];p+=4
        if not 1<=length<=128 or p+length+8>payload:raise ValueError('section name extent')
        name=raw[p:p+length].decode('utf8');p+=length
        count,width=struct.unpack_from('<ii',raw,p);p+=8
        if count<0 or width not in (1,2,4) or count*width>16000000 or p+count*width>payload:raise ValueError('section data extent')
        value=raw[p:p+count*width];p+=len(value)
        if name=='gateMaskChunk':sections.setdefault('gateMask',bytearray()).extend(value)
        elif name in sections:raise ValueError('duplicate section '+name)
        else:sections[name]=value
    for name,value in list(sections.items()):
        if name.startswith('plan/ref/'):
            target='plan/'+value.decode('utf8')
            if target not in sections or target.startswith('plan/ref/'):raise ValueError('missing planning definition '+target)
            resolved='plan/'+name[len('plan/ref/'):]
            if resolved in sections:raise ValueError('duplicate planning definition '+resolved)
            sections[resolved]=sections[target]
    if meta.get('planningComplete')=='True':
        for stage in ('seed-pre','seed-post','distance-pre','distance-post'):
            for field,width in [('seeds',1),('visits',2),('distance',2),('queue',4),('queueRows',2)]:
                if len(sections.get('plan/'+stage+'/'+field,b''))!=320800*width:raise ValueError('planning stage capacity '+stage+'/'+field)
        if len(sections.get('plan/profiles90',b''))!=360 or len(sections.get('plan/permissions540',b''))!=2160:raise ValueError('planning table capacities')
    if 'fixture' not in meta:
        n=len(sections['components'])//2
        if not 1<=n<=320800:raise ValueError('map extent')
        for name,width in [('edges',1),('flags',4),('x',2),('y',2),('specialIds',2),('gateMask',1)]:
            if len(sections[name])!=n*width:raise ValueError('grid capacity '+name)
        if len(sections['records13'])%52 or len(sections['allies'])!=81:raise ValueError('record/role extent')
    return meta,sections,hashlib.sha256(raw).hexdigest()

def main():
    parser=argparse.ArgumentParser();parser.add_argument('artifact');parser.add_argument('--replay',action='store_true');args=parser.parse_args()
    meta,sections,digest=decode(args.artifact)
    print('complete=True schema='+str(struct.unpack_from('<i',Path(args.artifact).read_bytes(),8)[0])+' SHA256='+digest)
    print(meta);print('sectionBytes', {k:len(v) for k,v in sections.items()})
    if args.replay:
        mod=Path(__file__).resolve().parents[2]/'Testmods/EnemyBridgePathTest'
        subprocess.run([str(mod/'tests/bin/EnemyBridgePathTest.PolicyTests.exe'),'--replay',str(Path(args.artifact).resolve())],cwd=mod,check=True)

if __name__=='__main__':main()
