"""Offline DesyncTest format-2 reader. No game process, network or third-party packages required."""
import argparse
import collections
import json
import hashlib
from pathlib import Path
import struct

HEADER = struct.Struct('<IHHqqqq10i')
NAMES = dict(enumerate(('Start','End','Arrival','Dispatch','Checksum','CategoryFlags','Field','Unit','UnitHash','WorldUnit','ForcedRun','DeepEnded','Fault'),1))

def records(path, errors):
    if not path.exists():
        errors.append(f'missing: {path.name}')
        return
    with path.open('rb') as f:
        while True:
            header=f.read(HEADER.size)
            if not header:
                return
            if len(header)!=HEADER.size:
                errors.append(f'{path.name}: incomplete final header');return
            size,version,kind,seq,session,clock,utc,tick,phase,op,player,command,planned,unit,global_id,stage,length=HEADER.unpack(header)
            if version!=2 or kind not in NAMES or length<0 or length>256*1024*1024 or size!=80+length:
                errors.append(f'{path.name}: invalid frame at {f.tell()-80}');return
            payload=f.read(length)
            if len(payload)!=length:
                errors.append(f'{path.name}: incomplete final payload');return
            yield dict(kind=NAMES[kind],seq=seq,session=session,clock=clock,utcTicks=utc,tick=tick,phase=phase,opcode=op,player=player,command=command,
                       planned=planned,unit=unit,global_id=global_id,stage=stage,payload=payload,offset=f.tell()-length,filename=str(path))

def normalize_record(data):
    # Compare only the audited gameplay checksum inputs, not the preceding local animation/hover fields.
    if len(data)!=1168:
        raise ValueError('wire unit record must contain 1168 bytes')
    result=bytearray(data[0x8c:]);result[0x41c-0x8c:0x424-0x8c]=bytes(8)
    return bytes(result)+data[0x3c:0x48]

def validate_manifest(manifest):
    if not isinstance(manifest,dict) or type(manifest.get('format')) is not int or manifest['format']!=2:
        raise ValueError('unsupported or invalid manifest format')
    if type(manifest.get('session')) is not int or not isinstance(manifest.get('identity'),dict):
        raise ValueError('manifest requires an integer session and an identity object')
    if 'clockFrequency' in manifest and (type(manifest['clockFrequency']) is not int or manifest['clockFrequency']<=0):
        raise ValueError('clockFrequency must be a positive integer')
    coverage=manifest.get('coverage',{})
    if not isinstance(coverage,dict) or type(coverage.get('requestedUnitSlots',9999)) is not int or not 1<=coverage.get('requestedUnitSlots',9999)<=9999:
        raise ValueError('invalid coverage/requestedUnitSlots')
    for field in ('plugins','participants'):
        values=manifest['identity'].get(field,[])
        if not isinstance(values,list) or any(not isinstance(v,dict) for v in values):raise ValueError(f'identity.{field} must be an object array')
    for plugin in manifest['identity'].get('plugins',[]):
        if any(not isinstance(plugin.get(k),str) for k in ('guid','version','sha256')):raise ValueError('invalid component identity')
    for field in ('nativeHash','managedHash','startKind','file'):
        value=manifest['identity'].get(field)
        if value is not None and not isinstance(value,str):raise ValueError(f'identity.{field} must be a string or null')

def load(folder):
    folder=Path(folder)
    if not (folder/'manifest.json').exists():
        raise ValueError(f'Choose an explicit session directory containing manifest.json: {folder}')
    manifest=json.loads((folder/'manifest.json').read_text(encoding='utf-8-sig'))
    validate_manifest(manifest)
    errors=[];counts=collections.Counter();hashes={};snapshots={};checksums={};commands={};flags=[]
    last_seq={};duplicate=0;repair_groups=[];pending_repairs={};series={};incidents=[];active_incident=None;arrivals={};execution_clocks={}
    ambiguous_hashes=set();ambiguous_checksums=set();ambiguous_snapshots=set()
    for filename in ('events.bin','snapshots.bin'):
        for r in records(folder/filename,errors):
            counts[r['kind']]+=1
            if r['session']!=manifest['session']:errors.append('record session differs from manifest');continue
            if r['seq']<=last_seq.get(filename,0):errors.append(f'{filename}: non-monotonic sequence');continue
            last_seq[filename]=r['seq']
            key=(r['tick'],r['phase'],r['unit'],r['global_id'])
            if r['kind']=='Arrival' and r['command']!=-1:
                arrivals.setdefault((r['player'],r['command'],r['opcode']),[]).append((r['clock'],r['planned'],r['seq']))
            elif r['kind']=='UnitHash':
                if len(r['payload'])!=12:errors.append('invalid unit hash length');continue
                if key in hashes:ambiguous_hashes.add(key)
                hashes[key]=r['payload']
            elif r['kind']=='Checksum':
                key=(r['tick'],r['phase'],r['planned'])
                if len(r['payload'])!=56:errors.append('invalid aggregate length');continue
                if key in checksums:ambiguous_checksums.add(key)
                checksums[key]=r['payload']
            elif r['kind'] in ('WorldUnit','Unit'):
                key=(r['kind'],)+key+(r['stage'],)
                if key in snapshots:duplicate+=1;ambiguous_snapshots.add(key)
                if r['stage'] not in ((0,1) if r['kind']=='WorldUnit' else (0,1,4,5)):
                    errors.append('invalid unit snapshot stage');continue
                expected=1168 if r['stage']%2==0 else 1000
                if len(r['payload'])!=expected:errors.append('invalid unit snapshot length');continue
                if expected==1168 and struct.unpack_from('<i',r['payload'],0x98)[0]!=r['global_id']:
                    errors.append('unit identity differs between header and copied record');continue
                value=normalize_record(r['payload']) if expected==1168 else r['payload']
                reference=(hashlib.sha256(value).digest(),r['filename'],r['offset'],len(r['payload']))
                snapshots[key]=reference
                if r['kind']=='Unit':
                    repair_key=(r['tick'],r['phase'],r['unit'],r['player'],r['command'])
                    if r['stage']==0 or repair_key not in pending_repairs:
                        group=dict(tick=r['tick'],unit=r['unit'],command=r['command'],startSequence=r['seq'],oldGlobal=r['global_id'] if r['stage']==0 else None,parts={})
                        pending_repairs[repair_key]=group;repair_groups.append(group)
                    group=pending_repairs.get(repair_key)
                    if group is not None:
                        group['parts'][r['stage']]=reference
                        if r['stage']==4:group['finalGlobal']=r['global_id']
                        if r['stage']==5:pending_repairs.pop(repair_key,None)
                else:series.setdefault(('WorldUnit',r['tick'],r['phase']),{}).setdefault(r['unit'],set()).add(r['stage'])
            elif r['kind']=='Dispatch' and r['phase']==0 and r['stage']==0:
                key=(r['player'],r['command'],r['opcode'])
                commands.setdefault(key,[]).append((r['tick'],r['planned']))
                execution_clocks.setdefault(key,[]).append(r['clock'])
                if r['opcode']==54:
                    active_incident=dict(attempt=len(incidents)+1,startSequence=r['seq'],startTick=r['tick'],state='no-observed-end',units=[])
                    incidents.append(active_incident)
            elif r['kind']=='Dispatch' and r['phase']==0 and r['stage']==1 and r['opcode'] in (67,89):
                if active_incident is not None:
                    active_incident.update(endSequence=r['seq'],endTick=r['tick'],state='finished' if r['opcode']==67 else 'aborted')
                    active_incident=None
            elif r['kind'] in ('ForcedRun','Fault','CategoryFlags'):
                flag={k:v for k,v in r.items() if k not in ('payload','filename','offset')}
                if r['kind']=='CategoryFlags':flag['categoryFlags']=list(r['payload'])
                flags.append(flag)
            if r['kind']=='UnitHash':series.setdefault(('UnitHash',r['tick'],r['phase']),{}).setdefault(r['unit'],set()).add(0)
    expected=manifest.get('coverage',{}).get('requestedUnitSlots',9999)
    coverage=[]
    for key,slots in sorted(series.items()):
        complete=sum((0 in stages and (key[0]=='UnitHash' or 1 in stages)) for stages in slots.values())
        all_slots=set(slots)==set(range(1,expected+1))
        coverage.append(dict(kind=key[0],tick=key[1],phase=key[2],slots=len(slots),completeSlots=complete,expectedSlots=expected,
                             allRequestedSlotsPresent=all_slots,allRequestedDataPresent=all_slots and complete==expected))
    repairs=[]
    for group in repair_groups:
        parts=group.pop('parts');group['recordEndpointsPresent']=0 in parts and 4 in parts
        group['pathEndpointsPresent']=1 in parts and 5 in parts
        if group['recordEndpointsPresent']:
            x,y=read_snapshot(parts[0]),read_snapshot(parts[4])
            offsets=gameplay_offsets(x,y)
            group.update(changedGameplayBytes=len(offsets),wireOffsets=[hex(i) for i in offsets[:64]],
                         identityChanged=group['oldGlobal']!=group.get('finalGlobal'))
        if group['pathEndpointsPresent']:group['storedPathChanged']=parts[1][0]!=parts[5][0]
        for incident in reversed(incidents):
            if incident['startSequence']<=group['startSequence']<=incident.get('endSequence',float('inf')):
                group['resyncStartSequence']=incident['startSequence']
                if group['unit'] not in incident['units']:incident['units'].append(group['unit'])
                break
        repairs.append(group)
    matched=sorted(arrivals.keys()&commands.keys());frequency=manifest.get('clockFrequency')
    receipt_samples=[]
    for key in matched[:128]:
        first=min(arrivals[key]);executed=execution_clocks[key][0]
        receipt_samples.append(dict(sourceCommandOpcode=key,arrivalSequence=first[2],arrivalPlannedTick=first[1],execution=commands[key][0],
                                    localCaptureDeltaSeconds=(executed-first[0])/frequency if frequency else None))
    arrival_coverage=dict(matchedCommands=len(matched),arrivalWithoutObservedExecution=len(arrivals.keys()-commands.keys()),
                          executionWithoutObservedArrival=len(commands.keys()-arrivals.keys()),samples=receipt_samples,
                          limits='Only ordinary inner headers. Local sender normally has no wire receipt. Times are local capture points, not network latency; compression/special packets remain raw.')
    return dict(folder=str(folder),manifest=manifest,errors=errors,counts=dict(counts),hashes=hashes,snapshots=snapshots,
                checksums=checksums,commands=commands,flags=flags,duplicateSnapshots=duplicate,repairs=repairs,snapshotCoverage=coverage,incidents=incidents,arrivalCoverage=arrival_coverage,
                ambiguous=dict(hashes=ambiguous_hashes,checksums=ambiguous_checksums,snapshots=ambiguous_snapshots))

def read_snapshot(reference):
    with Path(reference[1]).open('rb') as f:
        f.seek(reference[2]);data=f.read(reference[3])
        if len(data)!=reference[3]:raise ValueError('snapshot changed since indexing')
        return data

def gameplay_offsets(x,y):
    return ([i for i in range(0x8c,1168) if not 0x41c<=i<0x424 and x[i]!=y[i]]+
            [i for i in range(0x3c,0x48) if x[i]!=y[i]])

def compare(a,b):
    identity_a=a['manifest']['identity'];identity_b=b['manifest']['identity']
    # Session IDs are LOCAL. Never equate them across machines.
    warnings=[]
    if not a['manifest'].get('complete') or not b['manifest'].get('complete'):warnings.append('at least one package is incomplete or completion is unknown')
    if a['errors'] or b['errors']:warnings.append('input errors: compare only validated captured frames')
    if any(a['ambiguous'].values()) or any(b['ambiguous'].values()):warnings.append('repeated observation identity: ambiguous state comparisons are skipped; individual repairs remain listed')
    for field in ('nativeHash','managedHash','startKind','file'):
        if not identity_a.get(field) or not identity_b.get(field) or identity_a.get(field)!=identity_b.get(field):warnings.append(f'different or unresolved identity: {field}')
    def participant_map(identity):
        return sorted(tuple(json.dumps(v.get(k),sort_keys=True) for k in ('player','steam','ai')) for v in identity.get('participants',[]))
    if not identity_a.get('participants') or not identity_b.get('participants'):warnings.append('participant mapping unavailable')
    elif participant_map(identity_a)!=participant_map(identity_b):warnings.append('participant mapping differs; source-player command identities may not be comparable')
    plugins_a={v['guid']:(v['version'],v['sha256']) for v in identity_a.get('plugins',[])}
    plugins_b={v['guid']:(v['version'],v['sha256']) for v in identity_b.get('plugins',[])}
    if plugins_a!=plugins_b:warnings.append('component sets differ; local-only mods can legitimately differ')
    incompatible=not identity_a.get('nativeHash') or identity_a.get('nativeHash')!=identity_b.get('nativeHash')
    for field in ('unitWirePrefix','unitRecord','pathBytes'):
        va=a['manifest'].get('coverage',{}).get(field);vb=b['manifest'].get('coverage',{}).get(field)
        if va is not None and vb is not None and va!=vb:incompatible=True
    if incompatible:
        return dict(verdict='incompatible-capture-contracts',earliestObservedTick=None,warnings=warnings,mismatches=[],focusUnitIds=[],
                    limits='No state comparison across different or unresolved native/layout contracts.')
    mismatches=[]
    compared=0
    def unit_identities(data):
        result={}
        for key in data['hashes']:
            if key not in data['ambiguous']['hashes']:result.setdefault(key[:3],set()).add(key[3])
        for key in data['snapshots']:
            if key[0]=='WorldUnit' and key[-1]==0 and key not in data['ambiguous']['snapshots']:
                result.setdefault(key[1:4],set()).add(key[4])
        return result
    ids_a,ids_b=unit_identities(a),unit_identities(b)
    ambiguous_slots={k for data in (ids_a,ids_b) for k,values in data.items() if len(values)!=1}
    if ambiguous_slots:warnings.append('multiple unit identities at one tick/phase: affected slots are not comparable')
    for label,field in (('unit-hash','hashes'),('category-checksum','checksums'),('command-execution','commands')):
        shared=a[field].keys()&b[field].keys()
        shared-=a['ambiguous'].get(field,set())|b['ambiguous'].get(field,set())
        if field=='hashes':shared={k for k in shared if k[:3] not in ambiguous_slots}
        compared+=len(shared)
        for k in sorted(shared):
            if a[field][k]!=b[field][k]:
                tick=min(v[0] for values in (a[field][k],b[field][k]) for v in values) if field=='commands' else k[0]
                mismatches.append(dict(type=label,identity=k,tick=tick))
    missing={field:dict(onlyA=len(a[field].keys()-b[field].keys()),onlyB=len(b[field].keys()-a[field].keys())) for field in ('hashes','checksums','commands')}
    # Different Global-IDs in the same comparable slot are evidence themselves. Including
    # Global-ID in the primary key must not hide the divergence as merely absent coverage.
    for key in sorted(ids_a.keys()&ids_b.keys()):
        if key in ambiguous_slots:continue
        compared+=1
        if len(ids_a[key])==len(ids_b[key])==1 and ids_a[key]!=ids_b[key]:
            mismatches.append(dict(type='unit-identity',identity=key,tick=key[0],globalA=sorted(ids_a[key]),globalB=sorted(ids_b[key])))
    shared_snapshots=a['snapshots'].keys()&b['snapshots'].keys()
    shared_snapshots-=a['ambiguous']['snapshots']|b['ambiguous']['snapshots']
    # Repair endpoints are operation-local, not common world checkpoints. Single-package
    # repair differences remain available, but same-tick repairs are not peer snapshots.
    shared_snapshots={k for k in shared_snapshots if k[0]=='WorldUnit' and k[1:4] not in ambiguous_slots}
    compared+=len(shared_snapshots)
    for key in sorted(shared_snapshots):
        if a['snapshots'][key][0]==b['snapshots'][key][0]:continue
        x=read_snapshot(a['snapshots'][key]);y=read_snapshot(b['snapshots'][key])
        if key[-1]%2==0:
            if len(x)!=1168 or len(y)!=1168:continue
            nx,ny=normalize_record(x),normalize_record(y)
            if nx!=ny:
                offsets=gameplay_offsets(x,y)
                mismatches.append(dict(type='unit-record',identity=key,tick=key[1],wireOffsets=[hex(i) for i in offsets[:64]],
                                       note='Extender GameUnit offset = wire offset - 4; remaining differences may be truncated'))
        elif x!=y:mismatches.append(dict(type='stored-path',identity=key,tick=key[1]))
    earliest=min((v['tick'] for v in mismatches),default=None)
    focus=set()
    for value in mismatches:
        if value['type'] in ('unit-hash','unit-identity'):focus.add(value['identity'][2])
        elif value['type'] in ('unit-record','stored-path'):focus.add(value['identity'][3])
    return dict(verdict='observed-difference' if mismatches else ('no-difference-in-comparable-captured-data' if compared else 'insufficient-comparable-data'),
                earliestObservedTick=earliest,warnings=warnings,coverageDifferences=missing,
                mismatches=mismatches,focusUnitIds=sorted(focus),comparedObservations=compared,
                limits='No automatic root-cause proof. Explicit folder choice does not prove same mission. Host is the repair source, not necessarily correct.')

def summary(a):
    result={k:a[k] for k in ('folder','manifest','errors','counts','flags','duplicateSnapshots','repairs','snapshotCoverage','incidents','arrivalCoverage')}
    result['repairInterpretation']='Observed correction, not proof of the original faulty write. Raw wire offsets use a four-byte prefix before GameUnit.'
    return result

def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('session',nargs='+',help='One explicit session folder, or host and client folders')
    p.add_argument('--output',help='Write JSON report to this separate file; default stdout')
    args=p.parse_args()
    if len(args.session) not in (1,2):p.error('choose exactly one or two explicit session folders')
    try:
        loaded=[load(v) for v in args.session]
        result=dict(sessions=[summary(v) for v in loaded])
        if len(loaded)==2:result['comparison']=compare(*loaded)
        text=json.dumps(result,indent=2,ensure_ascii=False)+'\n'
        if args.output:
            output=Path(args.output).resolve()
            for folder in map(lambda v:Path(v).resolve(),args.session):
                if output==folder or folder in output.parents:raise ValueError('report must be outside input session folders')
            if output.exists():raise ValueError('refusing to overwrite an existing report')
            output.write_text(text,encoding='utf-8')
        else:print(text,end='')
    except (ValueError,OSError,json.JSONDecodeError) as ex:p.exit(2,str(ex)+'\n')

if __name__=='__main__':main()
