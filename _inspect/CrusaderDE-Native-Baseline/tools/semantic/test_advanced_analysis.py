import hashlib
import json
from pathlib import Path
import sqlite3
import subprocess
import sys
import tempfile
import unittest
from advanced_analysis import export,import_exports,summarize
from baseline_publication import fingerprint,publication_guard
from native_analysis import sha,write


class AdvancedAnalysisTests(unittest.TestCase):
    def setUp(self):
        self.root=Path(__file__).resolve().parents[4]/'.tools/native-analysis/advanced-tests'
        self.root.mkdir(parents=True,exist_ok=True)
        self.temporary=tempfile.TemporaryDirectory(dir=self.root)
        self.work=Path(self.temporary.name)
        self.f={'binaryHash':'hash','functionRva':'0x1000','instructions':[{'rva':'0x1000','bytes':'C3','icedVerified':True}],
                'ranges':[{'start':'0x1000','endInclusive':'0x1000'}],
                'flow':{'status':'complete','parameters':[],'operations':[]}}
    def tearDown(self):self.temporary.cleanup()
    def test_contract_requires_full_bytes_and_current_evidence(self):
        declaration={'functionRva':'0x1000','binaryHash':'hash','functionBytesSha256':hashlib.sha256(bytes.fromhex('C3')).hexdigest().upper(),
                     'evidenceStatus':'current'}
        m={'status':'complete-static-observations','accesses':[]}
        self.assertEqual('matched',summarize(self.f,m,[declaration])['documentedContracts'][0]['validation'])
        declaration['evidenceStatus']='stale'
        self.assertEqual('requires-review',summarize(self.f,m,[declaration])['documentedContracts'][0]['validation'])
        declaration['evidenceStatus']='current';self.f['instructions'][0]['icedVerified']=False
        self.assertEqual('requires-review',summarize(self.f,m,[declaration])['documentedContracts'][0]['validation'])
    def test_export_reconstructs_schema_six_and_counts(self):
        types=self.work/'work/TYPES.json';write(types,{'binaryHash':'hash','profile':'facts','types':[{'path':'/int','size':4}]})
        types.with_name('TYPES.json.sha256').write_text(sha(types))
        write(self.work/'profile.json',{'binaryHash':'hash','factsDigest':'facts'})
        models=self.work/'models.json';write(models,{'models':[],'functionContracts':[]})
        result=export(self.work,[self.f],self.work,models,sha,write)
        self.assertEqual(1,result['contracts'])
        connection=sqlite3.connect(':memory:')
        connection.executescript("CREATE TABLE metadata(key,value);INSERT INTO metadata VALUES('schema_version','5');CREATE TABLE analysis_profiles(key,payload);")
        import_exports(connection,self.work)
        self.assertEqual('6',connection.execute("SELECT value FROM metadata WHERE key='schema_version'").fetchone()[0])
        self.assertEqual(1,connection.execute('SELECT COUNT(*) FROM analysis_contracts').fetchone()[0]);connection.close()
    def test_missing_memory_export_cannot_be_success(self):
        write(self.work/'advanced.json',{'binaryHash':'hash','layouts':[],'contracts':[],
                                        'memoryCoverage':[{'function':'0x1000','count':1}]})
        connection=sqlite3.connect(':memory:');connection.execute('CREATE TABLE analysis_profiles(key,payload)')
        with self.assertRaisesRegex(ValueError,'coverage'):import_exports(connection,self.work)
        connection.close()
    def test_manifest_changed_is_not_replaced(self):
        manifest=self.work/'manifest.json';manifest.write_text('original');expected=fingerprint(manifest)
        manifest.write_text('other writer')
        with self.assertRaisesRegex(RuntimeError,'changed'):
            with publication_guard(manifest,expected):manifest.write_text('would overwrite')
        self.assertEqual('other writer',manifest.read_text())
    def test_current_identity_changed_is_rejected(self):
        manifest=self.work/'manifest.json';manifest.write_text('original')
        pointer=self.work/'CURRENT.json';pointer.write_text('first');expected=fingerprint(pointer);pointer.write_text('second')
        with self.assertRaisesRegex(RuntimeError,'CURRENT'):
            with publication_guard(manifest,fingerprint(manifest),pointer=pointer,expected_pointer=expected):pass
    def test_parallel_writer_cannot_enter_guard(self):
        manifest=self.work/'manifest.json';manifest.write_text('original')
        code="from baseline_publication import publication_guard,fingerprint;import sys;\nwith publication_guard(sys.argv[1],fingerprint(sys.argv[1])): pass"
        with publication_guard(manifest,fingerprint(manifest)):
            result=subprocess.run([sys.executable,'-c',code,str(manifest)],cwd=Path(__file__).parent,capture_output=True,text=True)
        self.assertNotEqual(0,result.returncode);self.assertIn('Another baseline writer',result.stderr)
    def test_old_generation_remains_importable(self):
        connection=sqlite3.connect(':memory:');import_exports(connection,self.work)
        self.assertIsNone(connection.execute("SELECT name FROM sqlite_master WHERE name='analysis_memory'").fetchone());connection.close()


if __name__=='__main__':unittest.main()
