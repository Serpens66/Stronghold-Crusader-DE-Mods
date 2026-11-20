import importlib.util
import json
from pathlib import Path
import struct
import tempfile
import unittest
import sys
import os

def fixture():
    return tempfile.TemporaryDirectory(dir=os.environ.get('DESYNC_TEST_ROOT'))

spec=importlib.util.spec_from_file_location('analyzer',Path(__file__).parents[1]/'tools/Analyze-Desync.py')
analyzer=importlib.util.module_from_spec(spec);spec.loader.exec_module(analyzer)

class AnalyzerTests(unittest.TestCase):
    def test_observations_show_distinct_worker_branch_and_ignore_camera_alone(self):
        def worker(selected,calls,x,view):
            values=(17,selected,calls,0,x,0,view,1234,1,1234,1,15,9,123)
            row=dict(kind='WorkerInputs',seq=1,tick=100,player=6,command=15,planned=0,unit=9,global_id=123,
                     stage=0,phase=11,opcode=-1,payload=struct.pack('<14i',*values))
            return analyzer.parse_write(row)
        incident=dict(startTick=200,startSequence=2)
        left=dict(writes=[worker(0,1,0,0)])
        right=dict(writes=[worker(17,1,34,2)])
        difference=analyzer.compare_writes(left,right,incident,incident)['WorkerInputs'][0]
        self.assertEqual(difference['host'][0]['outcome'],'no-valid-entrance')
        self.assertEqual(difference['client'][0]['outcome'],'assigned')
        self.assertFalse(difference['missingObservation'])
        self.assertEqual(analyzer.compare_writes(right,dict(writes=[worker(17,1,34,0)]),incident,incident),{})

    def test_pinned_checkpoint_deduplicates_rotating_copy_in_event_order(self):
        with fixture() as tmp:
            folder=self.packet(Path(tmp),'rolling')
            def frame(kind,seq,tick,phase,op,stage,p=b''):
                return analyzer.HEADER.pack(80+len(p),2,kind,seq,1,seq,1,tick,phase,op,1,40,tick,0,0,stage,len(p))+p
            early=frame(5,5,200,10,-1,0,bytes(56))
            (folder/'checkpoints.previous.bin').write_bytes(early)
            (folder/'checkpoints.bin').write_bytes(frame(5,8,400,13,-1,1,bytes(56)))
            (folder/'evidence.bin').write_bytes(early)
            (folder/'markers.bin').write_bytes(frame(4,6,399,0,54,0)+frame(4,9,400,0,67,1))
            tape=analyzer.load(folder)
            self.assertEqual(tape['errors'],[])
            self.assertEqual(len(tape['checksums']),2)
            self.assertEqual(tape['incidents'][0]['state'],'finished')
            (folder/'checkpoints.previous.bin').unlink()
            self.assertEqual(len(analyzer.load(folder)['checksums']),2)

    def test_compares_native_worker_decisions_and_executed_chore_fields(self):
        with fixture() as tmp:
            tapes=[]
            for name,worker_result,entrance_result,chore in (('host',0,0,b'abc'),('client',1559,1,b'abd')):
                folder=self.packet(Path(tmp),name)
                def frame(kind,seq,tick,phase,op,command,planned,slot,identity,stage,p=b''):
                    return analyzer.HEADER.pack(80+len(p),2,kind,seq,1,1,1,tick,phase,op,6,command,planned,slot,identity,stage,len(p))+p
                events=frame(19,1,297,11,-1,15,2,271,1761573,1,analyzer.WORKER_DECISION.pack(entrance_result,3,0,0,0,0))
                events+=frame(18,2,297,11,-1,15,2,271,1761573,1,analyzer.WORKER_DECISION.pack(worker_result,0,0,0,0,0))
                events+=frame(4,4,312,0,54,0,312,0,0,0)
                # A later decision for the same building in the repair tick is outside the window.
                events+=frame(18,5,312,11,-1,15,2,271,1761573,0,analyzer.WORKER_DECISION.pack(0,0,0,0,0,0))
                (folder/'events.bin').write_bytes(events)
                (folder/'snapshots.bin').write_bytes(frame(7,3,304,0,106,2001,304,0,1,4,chore))
                tape=analyzer.load(folder)
                self.assertEqual(tape['errors'],[])
                tapes.append(tape)
            report=analyzer.compare(*tapes)
            self.assertEqual(report['customChoreFields']['compared'],1)
            self.assertEqual(report['mismatches'][0]['type'],'executed-custom-chore-field')
            difference,=report['unitChecksumWindows'][0]['nativeWorkerDecisionDifferences']
            self.assertEqual(difference['buildingGlobalId'],1761573)
            self.assertEqual([v['result'] for v in difference['host']],[0,0])
            self.assertEqual([v['result'] for v in difference['client']],[1,1559])
            self.assertFalse(difference['missingObservation'])

    def test_transient_building_difference_identifies_slot_without_repair(self):
        with fixture() as tmp:
            tapes=[]
            for name,checksum in (('host',10),('client',20)):
                folder=self.packet(Path(tmp),name)
                def frame(kind,seq,tick,phase,stage,payload,slot=0,identity=0,tag=0):
                    return analyzer.HEADER.pack(80+len(payload),2,kind,seq,1,1,1,tick,phase,-1,1,tag,tick,slot,identity,stage,len(payload))+payload
                def table(seq,tick,phase,value):
                    payload=bytearray(analyzer.BUILDING_CHECKSUMS.size)
                    struct.pack_into('<I',payload,36*4,value)
                    return frame(16,seq,tick,phase,int(phase==13),payload)
                row=bytearray(analyzer.BUILDING_SIZE)
                struct.pack_into('<I',row,0xd8,1881290)
                row[0x192]=checksum
                row[0x2fc]=checksum # Native checksum masks this word.
                (folder/'events.bin').write_bytes(table(1,7800,10,7)+table(2,8000,10,checksum)+table(4,8012,13,33))
                (folder/'snapshots.bin').write_bytes(frame(17,3,8000,10,0,row,37,1881290,checksum))
                tapes.append(analyzer.load(folder))
            report=analyzer.compare(*tapes)
            self.assertEqual(report['comparedNativeBuildingTables'],3)
            different=[v for v in report['mismatches'] if v['type']=='native-building-checksum-table']
            self.assertEqual([v['tick'] for v in different],[8000])
            building=different[0]['buildings'][0]
            self.assertEqual((building['slot'],building['host']['globalId']),(37,1881290))
            self.assertEqual(building['byteChanges'],[dict(offset='0x192',host=10,client=20)])

    def test_checksum_finds_different_player_and_slot_when_unit_repair_is_missing(self):
        with fixture() as tmp:
            tapes=[]
            for name,checksum in (('host',10),('client',20)):
                folder=self.packet(Path(tmp),name)
                def frame(kind,seq,tick,phase,op,p=b'',slot=0,identity=0):
                    return analyzer.HEADER.pack(80+len(p),2,kind,seq,1,1,1,tick,phase,op,3,42,tick,slot,identity,0,len(p))+p
                def table(seq,tick,value):
                    p=bytearray(analyzer.UNIT_CHECKSUMS.size)
                    struct.pack_into('<I',p,771*4,value)
                    return frame(14,seq,tick,10,-1,p)
                mark=analyzer.WORKER.pack(83060,21093,1,15,33,8,0,2,12,31,13,33)
                events=table(1,560,7)+frame(15,2,673,11,-1,mark,772,83060)+table(3,760,checksum)+frame(4,4,771,0,54)
                (folder/'events.bin').write_bytes(events)
                (folder/'snapshots.bin').write_bytes(b'')
                tapes.append(analyzer.load(folder))
            window=analyzer.compare(*tapes)['unitChecksumWindows'][0]
            self.assertEqual(window['repairedSlots'],[])
            self.assertEqual(window['checksumOnlySlots'],[dict(slot=772,lastMatchingTick=560,firstDifferentTick=760)])
            self.assertEqual(window['workerTrace']['host']['transitions'][0]['player'],3)
            self.assertEqual(window['workerTrace']['workplaceGlobalIds'],[21093])

    def test_worker_trace_links_repaired_slot_and_other_worker_by_workplace_global_id(self):
        with fixture() as tmp:
            tapes=[]
            for name,slot in (('host',12),('client',13)):
                folder=self.packet(Path(tmp),name)
                def frame(kind,seq,tick,phase,op,stage,p=b'',unit=0,identity=0):
                    return analyzer.HEADER.pack(80+len(p),2,kind,seq,1,1,1,tick,phase,op,6,42,tick,unit,identity,stage,len(p))+p
                def worker(seq,tick,slot,workplace):
                    identity=80+slot
                    mark=analyzer.WORKER.pack(identity,workplace,1,15 if workplace else 0,7,8,0,2,42,23,41,22)
                    return frame(15,seq,tick,11,-1,1,mark,slot,identity)
                (folder/'events.bin').write_bytes(worker(1,160,12,0)+worker(2,185,slot,901)+frame(4,3,200,0,54,0)+worker(5,200,12,902))
                (folder/'snapshots.bin').write_bytes(frame(8,4,200,0,56,0,bytes(1168),12))
                tapes.append(analyzer.load(folder))
            worker=analyzer.compare(*tapes)['unitChecksumWindows'][0]['workerTrace']
            self.assertEqual(worker['workplaceGlobalIds'],[901])
            self.assertEqual([(r['slot'],r['tick'],r['pendingType']) for r in worker['host']['transitions']],[(12,160,0),(12,185,15)])
            self.assertEqual([(r['slot'],r['tick'],r['pendingType']) for r in worker['client']['transitions']],[(12,160,0),(13,185,15)])

    def test_light_incident_uses_matching_then_divergent_native_checkpoints(self):
        with fixture() as tmp:
            traces=[]
            for name,value in (('host',10),('client',20)):
                folder=self.packet(Path(tmp),name)
                def frame(kind,seq,tick,phase,opcode,payload=b'',unit=0):
                    return analyzer.HEADER.pack(80+len(payload),2,kind,seq,1,1,1,tick,phase,opcode,1,42,tick,unit,0,0,len(payload))+payload
                def table(seq,tick,phase,hash_value):
                    payload=bytearray(analyzer.UNIT_CHECKSUMS.size)
                    struct.pack_into('<I',payload,11*4,hash_value)
                    block=bytearray(frame(14,seq,tick,phase,-1,payload))
                    if phase==13:struct.pack_into('<i',block,72,1)
                    return block
                events=table(1,170,10,7)+table(2,180,10,value)+frame(4,3,200,0,54)+table(4,200,13,value)
                (folder/'events.bin').write_bytes(events+table(6,200,10,value))
                (folder/'snapshots.bin').write_bytes(frame(8,5,200,0,56,bytes(1168),12))
                traces.append(analyzer.load(folder))
            report=analyzer.compare(*traces)
            self.assertEqual(report['comparedNativeUnitTables'],4)
            self.assertEqual(report['focusUnitIds'],[12])
            self.assertEqual(report['earliestObservedTick'],180)
            self.assertEqual(report['unitChecksumWindows'],[dict(resyncTick=200,comparedRegularCheckpoints=2,
                repairedSlots=[dict(slot=12,lastMatchingTick=170,firstDifferentTick=180)])])

    def packet(self,root,name,hash_value=1,record_value=0,global_id=100):
        folder=root/name;folder.mkdir()
        (folder/'manifest.json').write_text(json.dumps(dict(format=2,session=1,identity=dict(nativeHash='same',managedHash='same',startKind='test',file='same',plugins=[]))))
        def frame(kind,seq,stage,payload):
            return analyzer.HEADER.pack(80+len(payload),2,kind,seq,1,1,1,200,10,56,1,42,200,12,global_id,stage,len(payload))+payload
        (folder/'events.bin').write_bytes(frame(9,1,0,struct.pack('<IQ',hash_value,hash_value)))
        record=bytearray(1168);record[0xb2]=record_value;struct.pack_into('<i',record,0x98,global_id)
        (folder/'snapshots.bin').write_bytes(frame(10,2,0,record)+frame(10,3,1,bytes(1000)))
        return folder
    def test_difference_and_focus(self):
        with fixture() as tmp:
            root=Path(tmp);a=analyzer.load(self.packet(root,'a'));b=analyzer.load(self.packet(root,'b',2,1))
            report=analyzer.compare(a,b)
            self.assertEqual(report['earliestObservedTick'],200)
            self.assertEqual(report['focusUnitIds'],[12])
            self.assertTrue(any(v['type']=='unit-record' and '0xb2' in v['wireOffsets'] for v in report['mismatches']))
            self.assertEqual(a['errors'],[])
    def test_local_fields_and_mask_are_ignored(self):
        a=bytearray(1168);b=bytearray(a);b[0x10]=1;b[0x41c]=1
        self.assertEqual(analyzer.normalize_record(a),analyzer.normalize_record(b))
        b[0x3c]=1;self.assertNotEqual(analyzer.normalize_record(a),analyzer.normalize_record(b))
    def test_truncated_tail(self):
        with fixture() as tmp:
            p=self.packet(Path(tmp),'a');f=p/'events.bin';f.write_bytes(f.read_bytes()+b'partial')
            self.assertTrue(any('incomplete final header' in e for e in analyzer.load(p)['errors']))
    def test_malformed_length(self):
        with fixture() as tmp:
            p=self.packet(Path(tmp),'a');f=p/'events.bin';b=bytearray(f.read_bytes());b[76:80]=struct.pack('<i',-1);f.write_bytes(b)
            self.assertTrue(any('invalid frame' in e for e in analyzer.load(p)['errors']))
    def test_explicit_session_required(self):
        with fixture() as tmp:
            with self.assertRaisesRegex(ValueError,'explicit session'):analyzer.load(tmp)
    def test_repairs_remain_distinct_and_preserved_fields_are_not_gameplay(self):
        with fixture() as tmp:
            p=self.packet(Path(tmp),'a')
            def frame(seq,stage,payload,global_id):
                return analyzer.HEADER.pack(80+len(payload),2,8,seq,1,1,1,200,0,56,1,42,200,12,global_id,stage,len(payload))+payload
            old=bytearray(1168);old[0x34]=77;struct.pack_into('<i',old,0x98,100);new=bytearray(old);new[0xb2]=1;struct.pack_into('<i',new,0x98,101)
            data=b''
            for seq in (10,20):
                data+=frame(seq,0,old,100)+frame(seq+1,1,bytes(1000),100)
                data+=frame(seq+2,4,new,101)+frame(seq+3,5,bytes([1])*1000,101)
            (p/'snapshots.bin').write_bytes(data)
            loaded=analyzer.load(p)
            self.assertEqual(len(loaded['repairs']),2)
            for repair in loaded['repairs']:
                self.assertEqual(repair['wireOffsets'],['0x98','0xb2'])
                self.assertTrue(repair['identityChanged'] and repair['storedPathChanged'])
    def test_partial_snapshot_series_is_explicit(self):
        with fixture() as tmp:
            loaded=analyzer.load(self.packet(Path(tmp),'a'))
            self.assertTrue(loaded['snapshotCoverage'])
            self.assertTrue(all(v['slots']==1 and not v['allRequestedSlotsPresent'] for v in loaded['snapshotCoverage']))
    def test_repeated_resyncs_have_separate_attempts(self):
        with fixture() as tmp:
            p=self.packet(Path(tmp),'a')
            def frame(seq,op,stage):
                return analyzer.HEADER.pack(80,2,4,seq,1,1,1,200,0,op,1,42,200,0,0,stage,0)
            (p/'events.bin').write_bytes(frame(1,54,0)+frame(2,67,1)+frame(3,54,0)+frame(4,89,1))
            incidents=analyzer.load(p)['incidents']
            self.assertEqual([v['startSequence'] for v in incidents],[1,3])
            self.assertEqual([v['state'] for v in incidents],['finished','aborted'])
    def test_arrival_rescheduling_and_local_clocks_are_separate(self):
        with fixture() as tmp:
            root=Path(tmp);loaded=[]
            for name,clock in (('a',10),('b',1000)):
                p=self.packet(root,name);manifest=json.loads((p/'manifest.json').read_text());manifest['clockFrequency']=10
                (p/'manifest.json').write_text(json.dumps(manifest))
                def frame(kind,seq,tick,phase,planned,stamp):
                    return analyzer.HEADER.pack(80,2,kind,seq,1,stamp,1,tick,phase,36,1,42,planned,0,0,0,0)
                (p/'events.bin').write_bytes(frame(3,1,-1,-1,210,clock)+frame(4,2,220,0,220,clock+10))
                loaded.append(analyzer.load(p))
            sample=loaded[0]['arrivalCoverage']['samples'][0]
            self.assertEqual(sample['arrivalPlannedTick'],210);self.assertEqual(sample['execution'],(220,220))
            self.assertEqual(sample['localCaptureDeltaSeconds'],1.0)
            self.assertEqual(analyzer.compare(*loaded)['mismatches'],[])
    def test_different_global_ids_are_state_evidence_not_only_missing_coverage(self):
        with fixture() as tmp:
            root=Path(tmp);report=analyzer.compare(analyzer.load(self.packet(root,'a')),analyzer.load(self.packet(root,'b',global_id=101)))
            self.assertTrue(any(v['type']=='unit-identity' for v in report['mismatches']))
            self.assertEqual(report['earliestObservedTick'],200);self.assertEqual(report['focusUnitIds'],[12])
    def test_snapshot_only_difference_has_an_earliest_tick(self):
        with fixture() as tmp:
            root=Path(tmp);a=self.packet(root,'a');b=self.packet(root,'b',record_value=1)
            for p in (a,b):(p/'events.bin').write_bytes(b'')
            report=analyzer.compare(analyzer.load(a),analyzer.load(b))
            self.assertEqual(report['earliestObservedTick'],200);self.assertEqual(report['focusUnitIds'],[12])
    def test_ambiguous_world_snapshot_is_not_arbitrarily_compared(self):
        with fixture() as tmp:
            root=Path(tmp);a=self.packet(root,'a');b=self.packet(root,'b',record_value=1)
            f=a/'snapshots.bin';data=f.read_bytes();extra=bytearray(data[:1248]);struct.pack_into('<q',extra,8,4);extra[80+0xb2]=2;f.write_bytes(data+extra)
            report=analyzer.compare(analyzer.load(a),analyzer.load(b))
            self.assertFalse(any(v['type']=='unit-record' for v in report['mismatches']))
            self.assertTrue(any('ambiguous' in v for v in report['warnings']))
    def test_missing_path_prevents_complete_world_series(self):
        with fixture() as tmp:
            p=self.packet(Path(tmp),'a');manifest=json.loads((p/'manifest.json').read_text());manifest['coverage']={'requestedUnitSlots':1}
            (p/'manifest.json').write_text(json.dumps(manifest));f=p/'snapshots.bin';data=bytearray(f.read_bytes()[:1248]);struct.pack_into('<i',data,64,1);f.write_bytes(data)
            series=next(v for v in analyzer.load(p)['snapshotCoverage'] if v['kind']=='WorldUnit')
            self.assertTrue(series['allRequestedSlotsPresent']);self.assertFalse(series['allRequestedDataPresent']);self.assertEqual(series['completeSlots'],0)
    def test_invalid_manifests_have_explicit_errors(self):
        with fixture() as tmp:
            p=self.packet(Path(tmp),'a')
            invalid=[[],{'format':True},{'format':2,'session':'1','identity':{}},
                     {'format':2,'session':1,'identity':{},'clockFrequency':0},
                     {'format':2,'session':1,'identity':{'plugins':[{}]}},
                     {'format':2,'session':1,'identity':{},'coverage':{'requestedUnitSlots':-1}}]
            for manifest in invalid:
                with self.subTest(manifest=manifest):
                    (p/'manifest.json').write_text(json.dumps(manifest))
                    with self.assertRaises(ValueError):analyzer.load(p)
    def test_different_native_contracts_are_not_compared(self):
        with fixture() as tmp:
            root=Path(tmp);a=self.packet(root,'a');b=self.packet(root,'b',record_value=1)
            manifest=json.loads((b/'manifest.json').read_text());manifest['identity']['nativeHash']='other';(b/'manifest.json').write_text(json.dumps(manifest))
            report=analyzer.compare(analyzer.load(a),analyzer.load(b))
            self.assertEqual(report['verdict'],'incompatible-capture-contracts');self.assertEqual(report['mismatches'],[])
    def test_unit_repair_endpoints_are_not_world_checkpoints(self):
        with fixture() as tmp:
            root=Path(tmp);paths=[self.packet(root,'a'),self.packet(root,'b',record_value=1)]
            for p in paths:
                (p/'events.bin').write_bytes(b'');f=p/'snapshots.bin';data=bytearray(f.read_bytes())
                for at in (0,1248):struct.pack_into('<H',data,at+6,8);struct.pack_into('<i',data,at+44,0)
                f.write_bytes(data)
            report=analyzer.compare(*(analyzer.load(p) for p in paths))
            self.assertEqual(report['verdict'],'insufficient-comparable-data');self.assertEqual(report['mismatches'],[])
    def test_header_record_identity_mismatch_is_not_state_evidence(self):
        with fixture() as tmp:
            p=self.packet(Path(tmp),'a');f=p/'snapshots.bin';data=bytearray(f.read_bytes());struct.pack_into('<i',data,80+0x98,999);f.write_bytes(data)
            self.assertTrue(any('identity differs' in v for v in analyzer.load(p)['errors']))
    def test_old_semantics_are_not_silently_read_as_format_two(self):
        with fixture() as tmp:
            p=self.packet(Path(tmp),'a');f=p/'events.bin';data=bytearray(f.read_bytes());struct.pack_into('<H',data,4,1);f.write_bytes(data)
            self.assertTrue(any('invalid frame' in v for v in analyzer.load(p)['errors']))
            manifest=json.loads((p/'manifest.json').read_text());manifest['format']=1;(p/'manifest.json').write_text(json.dumps(manifest))
            with self.assertRaisesRegex(ValueError,'manifest format'):analyzer.load(p)

if __name__=='__main__':unittest.main(testRunner=unittest.TextTestRunner(stream=sys.stdout))
