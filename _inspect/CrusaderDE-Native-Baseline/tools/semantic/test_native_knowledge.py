"""Regression checks for transfer boundaries, not proof of Vanilla behavior."""
import copy
import json
import unittest
from pathlib import Path
from native_knowledge import prepare, validate_function_claims


class KnowledgeViews(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        base = Path(__file__).resolve().parents[2]
        current = json.loads((base / 'CURRENT.json').read_text(encoding='utf-8'))
        cls.catalog = json.loads((base / current['semanticDirectory'] / 'knowledge/native-field-catalog.json').read_text(encoding='utf-8'))
        cls.hash = current['currentNativeHash']
        cls.claims = [json.loads(line) for line in (base / current['semanticDirectory'] / 'knowledge/function-claims.jsonl').read_text(encoding='utf-8').splitlines()]

    def test_wrong_image_rejected(self):
        with self.assertRaisesRegex(ValueError, 'hash mismatch'):
            prepare(self.catalog, '0' * 64)

    def test_exact_inventory_and_independent_record_identity(self):
        plan = prepare(self.catalog, self.hash)
        expected = {f['id'] for f in self.catalog['fields']}
        self.assertEqual(set(plan['fieldIds']), expected)
        self.assertEqual(len([t for t in plan['types'] if '/Fields/' in t['path']]), len(expected))
        root_views = [t for t in plan['types'] if t['path'].endswith('_KnowledgeView')]
        self.assertEqual(len(root_views), len({f['recordTypeId'] for f in self.catalog['fields']}))
        modified = copy.deepcopy(self.catalog)
        for field in modified['fields']:
            field['aliases'].insert(0, '2.14.2.HistoricalName')
        other = prepare(modified, self.hash)
        self.assertEqual([t['path'] for t in plan['types']], [t['path'] for t in other['types']])
        for document in self.catalog['documents']:
            self.assertTrue(any(t['description'].endswith(document['text']) for t in plan['types']))

    def test_death_split_arrays_unknown_extent_and_uncertainty(self):
        plan = prepare(self.catalog, self.hash)
        by_name = {t['path'].split('/')[-1]: t for t in plan['types']}
        death = by_name['F000054_DeathMarker']
        self.assertEqual(death['length'], 2)
        self.assertEqual(death['components'][0]['type'], 'UInt16')
        self.assertIn('ProjectileDeathOnly', death['description'])
        self.assertIn('Rejected', death['description'])
        self.assertEqual(by_name['F000091_historical_FormerCombinedDeathAndAdjacentWord']['length'], 4)
        raid = next(t for n, t in by_name.items() if n.startswith('F000011_'))
        self.assertEqual((raid['components'][0]['count'], raid['length']), (100, 200))
        manager = by_name['GameUnitManager_UnknownExtent_KnowledgeView']
        self.assertIn('FULL RECORD EXTENT UNKNOWN', manager['description'])
        for f in self.catalog['fields']:
            view = next(t for t in plan['types'] if '/Fields/' + f['id'].replace('nkf-', 'F') + '_' in t['path'])
            self.assertIn(json.dumps(f, ensure_ascii=False, indent=2), view['description'])
            if not f['bindings'][0]['readVerified']:
                self.assertEqual(view['components'][0]['type'], 'Undefined')

    def test_overlapping_fields_all_reachable_at_record_offsets(self):
        plan = prepare(self.catalog, self.hash)
        types = {t['path']: t for t in plan['types']}
        def visit(path, base=0):
            for member in types[path]['components']:
                ref = member['type']
                if ref in types:
                    if '/Fields/' in ref:
                        yield ref.split('/Fields/F')[1].split('_')[0], base + member['offset']
                    else:
                        yield from visit(ref, base + member['offset'])
        found = dict(pair for t in plan['types'] if t['path'].endswith('_KnowledgeView') for pair in visit(t['path']))
        self.assertEqual(found, {f['id'].removeprefix('nkf-'): f['bindings'][0]['offset'] for f in self.catalog['fields']})

    def test_function_names_require_exact_confirmed_bindings(self):
        validate_function_claims(self.catalog,self.claims)
        plan=prepare(self.catalog,self.hash)
        self.assertEqual(len(plan['functions']),len(self.catalog.get('functions',[])))
        for source,stored in zip(self.catalog.get('functions',[]),plan['functions']):
            self.assertEqual(stored['name'],source['canonicalName'])
            self.assertEqual(stored['size'],int(source['functionEndRva'],16)-int(source['functionRva'],16))
            self.assertEqual(json.loads(stored['comment']),source)
        if plan['functions']:
            drifted=copy.deepcopy(self.catalog)
            drifted['functions'][0]['canonicalName']='accidentally_changed_name'
            with self.assertRaisesRegex(ValueError,'differs from its audited'):
                validate_function_claims(drifted,self.claims)
            altered=copy.deepcopy(self.catalog)
            altered['functions'][0]['semanticConfidence']='probable'
            with self.assertRaisesRegex(ValueError,'confirmed'):
                prepare(altered,self.hash)
            altered=copy.deepcopy(self.catalog)
            altered['functions'][0]['binaryHash']='0'*64
            with self.assertRaisesRegex(ValueError,'Wrong-image'):
                prepare(altered,self.hash)


if __name__ == '__main__':
    unittest.main()
