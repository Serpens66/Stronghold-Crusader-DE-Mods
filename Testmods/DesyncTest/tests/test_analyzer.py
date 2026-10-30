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

if __name__=='__main__':unittest.main(testRunner=unittest.TextTestRunner(stream=sys.stdout))
