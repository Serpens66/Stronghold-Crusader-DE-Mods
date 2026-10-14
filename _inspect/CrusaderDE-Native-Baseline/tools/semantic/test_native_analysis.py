"""Machine-contract regressions with private fixtures; no game execution or hooks."""
import copy
import hashlib
import json
from pathlib import Path
import sqlite3
import struct
import subprocess
import tempfile
import unittest
from unittest.mock import patch
import native_analysis as a
import native_queries as q

def instruction(rva, length=1, flow='Next', regs=(), targets=(), fallthrough=None, flags=0):
    return {'rva':a.hx(rva),'length':length,'bytes':'90'*length,'icedVerified':True,'icedFlow':flow,
            'registers':[{'register':r,'access':s} for r,s in regs], 'targets':[a.hx(t) for t in targets],
            'fallthrough':a.hx(fallthrough) if fallthrough else None,'flagsRead':flags}

class NativeAnalysisTests(unittest.TestCase):
    def setUp(self):
        self.db=sqlite3.connect(':memory:')
        self.db.executescript('CREATE TABLE metadata(key,value);CREATE TABLE analysis_functions(binary_hash,rva,payload);CREATE TABLE analysis_profiles(key,payload);CREATE TABLE analysis_features(id,payload);CREATE TABLE xrefs(binary_hash,from_rva,to_rva,type);CREATE TABLE hook_spans(binary_hash,payload);CREATE TABLE function_claims(binary_hash,function_rva,abi_confidence);CREATE TABLE functions(binary_hash,rva,name);')
        self.db.execute('INSERT INTO metadata VALUES(?,?)',('current_native_hash','HASH'))
        self.profiles={'X64InlineHook':{'backend':'RedBird.X64','minimum':14,'assembly':{'path':'fixture','sha256':'SHA'}},
                       'NativeDetour':{'backend':'RedBird.Backends.NativeX64','schemes':{'Indirect':6,'Absolute':14,'PushRet':12,'PushRetRedZoneSafe':23},'assembly':{'path':'fixture','sha256':'SHA'}}}
        self.db.execute('INSERT INTO analysis_profiles VALUES(?,?)',('backends',json.dumps(self.profiles)))
        self.f={'binaryHash':'HASH','functionRva':'0x1000','ranges':[{'start':'0x1000','endInclusive':'0x103F'}],
            'instructions':[instruction(i,fallthrough=i+1) for i in range(0x1000,0x1040)],'signatureSource':'DEFAULT','flow':{'status':'not-selected'}}
        self.put(self.f)
    def tearDown(self):self.db.close()
    def put(self,f):
        self.db.execute('DELETE FROM analysis_functions WHERE rva=?',(f['functionRva'],))
        self.db.execute('INSERT INTO analysis_functions VALUES(?,?,?)',(f['binaryHash'],f['functionRva'],json.dumps(f)))
    def check(self, **kw):
        with patch.object(q,'sha',return_value='SHA'):
            return q.hookcheck(self.db,kw.get('start','0x1000'),kw.get('kind','X64InlineHook'),kw.get('backend','RedBird.X64'),kw.get('scheme'),kw.get('minimum',7),kw.get('owner'))
    def test_inline_minimum_is_14(self):self.assertEqual(14,self.check()['displacedByteCount'])
    def test_actual_instruction_rounding(self):
        self.f['instructions']=[instruction(0x1000,3,fallthrough=0x1003),instruction(0x1003,7,fallthrough=0x100A),instruction(0x100A,7,fallthrough=0x1011),instruction(0x1011)]
        self.put(self.f);self.assertEqual(17,self.check()['displacedByteCount'])
    def test_indirect_schema_is_6(self):self.assertEqual(6,self.check(kind='NativeDetour',backend='RedBird.Backends.NativeX64',scheme='Indirect',minimum=1)['displacedByteCount'])
    def test_other_schemas(self):
        for s,n in [('Absolute',14),('PushRet',12),('PushRetRedZoneSafe',23)]:self.assertEqual(n,self.check(kind='NativeDetour',backend='RedBird.Backends.NativeX64',scheme=s,minimum=1)['displacedByteCount'])
    def test_unknown_backend(self):
        with self.assertRaises(ValueError):self.check(backend='Other')
    def test_missing_schema(self):
        with self.assertRaises(ValueError):self.check(kind='NativeDetour',backend='RedBird.Backends.NativeX64')
    def test_mid_instruction(self):
        self.f['instructions'][0]=instruction(0x1000,3);self.f['instructions']=self.f['instructions'][:1]+self.f['instructions'][3:];self.put(self.f)
        with self.assertRaisesRegex(ValueError,'inside'):self.check(start='0x1001')
    def test_wrong_entry(self):
        with self.assertRaisesRegex(ValueError,'entry'):self.check(start='0x1001',kind='NativeDetour',backend='RedBird.Backends.NativeX64',scheme='Indirect',minimum=6)
    def test_incoming_interior_conflict(self):
        self.db.execute('INSERT INTO xrefs VALUES(?,?,?,?)',('HASH','0x2000','0x1003','JUMP'));self.assertEqual('conflict',self.check()['status'])
    def test_backend_self_branch_widens(self):
        self.f['instructions'][20]['targets']=['0x1002'];self.f['instructions'][20]['icedFlow']='ConditionalBranch';self.put(self.f)
        self.assertEqual(21,self.check(kind='NativeDetour',backend='RedBird.Backends.NativeX64',scheme='Indirect',minimum=1)['displacedByteCount'])
    def test_recursive_entry_call_does_not_widen(self):
        self.f['instructions'][20]['targets']=['0x1000'];self.f['instructions'][20]['icedFlow']='Call';self.put(self.f)
        self.assertEqual(6,self.check(kind='NativeDetour',backend='RedBird.Backends.NativeX64',scheme='Indirect',minimum=1)['displacedByteCount'])
    def test_detour_minimum_is_not_a_fake_backend_option(self):
        with self.assertRaisesRegex(ValueError,'schema'):self.check(kind='NativeDetour',backend='RedBird.Backends.NativeX64',scheme='Indirect',minimum=7)
    def test_overlap(self):
        self.db.execute('INSERT INTO hook_spans VALUES(?,?)',('HASH',json.dumps({'spanId':'other','startRva':'0x1004','endRva':'0x1014'})))
        self.assertEqual('conflict',self.check()['status'])
    def test_other_span_in_same_feature_is_not_overlap(self):
        self.db.execute('INSERT INTO analysis_features VALUES(?,?)',('feature',json.dumps({'hookSpans':[
            {'spanId':'self','startRva':'0x1000','endRva':'0x100E'}, {'spanId':'distant','startRva':'0x2000','endRva':'0x2010'}]})))
        self.assertEqual('statically-passed',self.check(owner='self')['status'])
    def test_decode_disagreement(self):
        self.f['instructions'][0]['icedVerified']=False;self.put(self.f)
        with self.assertRaises(ValueError):self.check()
    def test_indirect_targets_unknown(self):
        self.f['instructions'][30]['icedFlow']='IndirectBranch';self.put(self.f);self.assertEqual('incomplete',self.check()['status'])
    def test_partial_and_conditional_registers(self):
        for reg,access in [('AX','Write'),('EAX','Write'),('RAX','ReadCondWrite'),('XMM0','Write')]:
            f={'instructions':[instruction(1,regs=[(reg,access)])]};a.machine_liveness(f)
            self.assertIn(a.normalize_reg(reg),f['instructions'][0]['liveIn'])
    def test_flags_and_loop(self):
        f={'instructions':[instruction(1,targets=[2],fallthrough=2),instruction(2,flow='ConditionalBranch',targets=[1],flags=4)]}
        a.machine_liveness(f);self.assertIn('FLAG:4',f['instructions'][0]['liveOut'])
    def test_unknown_call(self):
        f={'instructions':[instruction(1,flow='Call')]};a.machine_liveness(f);self.assertTrue(f['liveness']['unknownCallsOrEdges'])
    def test_pagination(self):
        p=q.page(list(range(20)),5,3);self.assertEqual([5,6,7],p['items']);self.assertTrue(p['truncated']);self.assertEqual(8,p['nextOffset'])
    def test_invalid_pagination(self):
        with self.assertRaises(ValueError):q.page([],0,0)
    def test_flow_failure_visible(self):
        t=q.trace(self.db,'0x1000',10,100);self.assertEqual('partial',t['status']);self.assertEqual('not-selected',t['unknowns'][0]['reason'])
    def test_unverified_call_binding_withheld(self):
        callee=copy.deepcopy(self.f);callee['functionRva']='0x2000';callee['flow']={'status':'complete','parameters':[{'ordinal':0}],'operations':[]};self.put(callee)
        self.f['flow']={'status':'complete','operations':[{'id':'op','site':'0x1000','opcode':'CALL','callTarget':'0x2000','inputs':[{},{}],'output':None}]};self.put(self.f)
        t=q.trace(self.db,'0x1000',10,100);self.assertFalse(any(e['kind']=='call-argument' for e in t['edges']));self.assertTrue(t['unknowns'])
    def test_supported_call_and_return_bindings(self):
        callee=copy.deepcopy(self.f);callee['functionRva']='0x2000';callee['signatureSource']='USER_DEFINED'
        callee['flow']={'status':'complete','parameters':[{'ordinal':0}],'operations':[{'id':'ret','site':'0x2000','opcode':'RETURN','inputs':[{},{}],'output':None}]};self.put(callee)
        self.f['flow']={'status':'complete','operations':[{'id':'op','site':'0x1000','opcode':'CALL','callTarget':'0x2000','inputs':[{},{}],'output':{'space':'register','size':4,'offset':'0x0'}}]};self.put(self.f)
        t=q.trace(self.db,'0x1000',10,100);self.assertEqual({'call-argument','return-binding'},{e['kind'] for e in t['edges']});self.assertFalse(t['endpointsOutsideReturnedNodes'])
    def test_trace_budget_frontier(self):
        self.f['flow']={'status':'complete','operations':[{'id':'one','site':'0x1000','opcode':'COPY','inputs':[]},{'id':'two','site':'0x1001','opcode':'COPY','inputs':[]}]};self.put(self.f)
        t=q.trace(self.db,'0x1000',10,1);self.assertEqual('partial',t['status']);self.assertIn('0x1000',t['frontier'])
    def chunk_fixture(self, offset=0, expected=2):
        root=a.WORKSPACE/'.tools/native-analysis/fixtures'/self._testMethodName;root.mkdir(parents=True,exist_ok=True)
        a.write(root/'profile.json',{'binaryHash':'HASH','digest':'digest'})
        a.write(root/'features.json',[]);a.write(root/'sources.json',[])
        header={'binaryHash':'HASH','functionRva':'0x1000','flow':{'status':'not-selected'},'expectedCounts':{'instructions':expected,'operations':0}}
        records=[{'recordType':'header','payload':header},{'recordType':'instructions','functionRva':'0x1000','offset':offset,'items':[{'rva':'0x1000'},{'rva':'0x1001'}]}]
        (root/'facts-000.jsonl').write_text(''.join(json.dumps(r)+'\r\n' for r in records),encoding='utf-8',newline='')
        return root
    def test_chunk_restore_roundtrip(self):
        with sqlite3.connect(':memory:') as c, patch.object(a,'BASELINE',a.WORKSPACE):
            a.import_analysis(c,a.BASELINE,self.chunk_fixture())
            f=json.loads(c.execute('SELECT payload FROM analysis_functions').fetchone()[0]);self.assertEqual(2,len(f['instructions']))
    def test_missing_chunk_rejected(self):
        with sqlite3.connect(':memory:') as c, patch.object(a,'BASELINE',a.WORKSPACE):
            with self.assertRaisesRegex(ValueError,'Incomplete'):a.import_analysis(c,a.BASELINE,self.chunk_fixture(expected=3))
    def test_reordered_chunk_rejected(self):
        with sqlite3.connect(':memory:') as c, patch.object(a,'BASELINE',a.WORKSPACE):
            with self.assertRaisesRegex(ValueError,'reordered'):a.import_analysis(c,a.BASELINE,self.chunk_fixture(offset=1))
    def test_actual_iced_fixture(self):
        root=a.WORKSPACE/'.tools/native-analysis/fixtures';root.mkdir(parents=True,exist_ok=True)
        if root:
            pe=bytearray(1024);pe[:2]=b'MZ';struct.pack_into('<I',pe,0x3C,0x80);pe[0x80:0x84]=b'PE\0\0'
            struct.pack_into('<H',pe,0x86,1);struct.pack_into('<H',pe,0x94,0xF0);struct.pack_into('<H',pe,0x98,0x20B);struct.pack_into('<Q',pe,0x98+24,0x180000000)
            struct.pack_into('<III',pe,0x188+12,0x1000,0x200,0x200)
            code=bytes.fromhex('6689C0 31C0 99 48F7F9 488B0500000000 7400 C3');pe[0x200:0x200+len(code)]=code
            native=root/'fixture.dll';native.write_bytes(pe);work=root/'work';work.mkdir(exist_ok=True)
            sizes=[3,2,1,3,7,2,1];offset=0;rows=[]
            for n in sizes:rows.append({'rva':a.hx(0x1000+offset),'length':n,'bytes':code[offset:offset+n].hex().upper()});offset+=n
            a.write(work/'1000.json',{'instructions':rows});digest=hashlib.sha256(pe).hexdigest().upper()
            exe=a.WORKSPACE/'.tools/native-analysis/NativeFacts/Release/net10.0/NativeFacts.dll'
            subprocess.run(['dotnet',str(exe),str(native),digest,str(work)],check=True,capture_output=True)
            facts=json.loads((work/'1000.json').read_text())['instructions']
            self.assertTrue(all(r['icedVerified'] for r in facts));self.assertTrue(facts[2]['registers']);self.assertTrue(facts[3]['registers'])
            self.assertEqual('0x1010',facts[4]['ripTarget']);self.assertNotEqual(0,facts[5]['flagsRead'])
            result=subprocess.run(['dotnet',str(exe),str(native),'0'*64,str(work)],capture_output=True)
            self.assertNotEqual(0,result.returncode)

if __name__=='__main__':unittest.main()
