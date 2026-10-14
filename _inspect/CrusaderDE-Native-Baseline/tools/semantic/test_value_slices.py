"""Value relevance and context isolation, including negative binding evidence."""
import sqlite3
import unittest
from value_slices import slice_values


def param(n): return {'parameterOrdinal': n}
def definition(name): return {'definition': name}
def constant(n): return {'constant': True, 'offset': hex(n), 'size': 4}
def op(name, code, inputs=(), block=0, output=True, target=None):
    return {'id': name, 'opcode': code, 'site': name.split('#')[0], 'inputs': list(inputs),
            'output': {'size': 4} if output else None, 'block': block, 'blockMembership': True, 'callTarget': target}
def function(rva, operations, count=1, blocks=None):
    return {'binaryHash': 'hash', 'functionRva': rva, 'signatureSource': 'USER_DEFINED',
            'flow': {'status': 'complete', 'parameters': [{'ordinal': i} for i in range(count)],
                     'operations': operations, 'blocks': blocks or [{'id': 0, 'successors': []}]}}


class ValueSliceTests(unittest.TestCase):
    def setUp(self):
        self.db = sqlite3.connect(':memory:')
        self.db.execute('CREATE TABLE function_claims(binary_hash,function_rva,abi_confidence)')
        self.functions = {}
    def tearDown(self): self.db.close()
    def load(self, _, value):
        if value not in self.functions: raise ValueError('missing')
        return self.functions[value]
    def run_slice(self, root='0x1000', **kw):
        return slice_values(self.db, self.load, root, direction=kw.pop('direction', 'backward'), **kw)
    def test_unused_parameter_absent_from_result(self):
        self.functions['0x1000'] = function('0x1000', [op('add', 'INT_ADD', [param(0), constant(3)]),
                                                        op('ret', 'RETURN', [constant(0), definition('add')], output=False)], count=2)
        result = self.run_slice(returns=True)
        self.assertEqual({'0x1000@root/ret', '0x1000@root/add', '0x1000@root/parameter/0'}, {n['id'] for n in result['nodes']})
    def test_unused_argument_not_bound_to_result(self):
        self.functions['0x2000'] = function('0x2000', [op('ret', 'RETURN', [constant(0), param(1)], output=False)], count=2)
        self.functions['0x1000'] = function('0x1000', [op('call', 'CALL', [constant(0), param(0), param(1)], target='0x2000'),
                                                        op('ret', 'RETURN', [constant(0), definition('call')], output=False)], count=2)
        result = self.run_slice(returns=True)
        keys = {n['id'] for n in result['nodes']}
        self.assertIn('0x1000@root/parameter/1', keys)
        self.assertNotIn('0x1000@root/parameter/0', keys)
    def test_two_calls_have_separate_contexts(self):
        self.functions['0x2000'] = function('0x2000', [op('ret', 'RETURN', [constant(0), param(0)], output=False)])
        self.functions['0x1000'] = function('0x1000', [op('first', 'CALL', [constant(0), param(0)], target='0x2000'),
                                                        op('second', 'CALL', [constant(0), param(1)], target='0x2000'),
                                                        op('ret', 'RETURN', [constant(0), definition('first')], output=False)], count=2)
        result = self.run_slice(returns=True)
        self.assertNotIn('0x1000@root/parameter/1', {n['id'] for n in result['nodes']})
        self.assertEqual(2, len({c['context'] for c in result['contexts'] if c['function'] == '0x2000'}))
    def test_explicit_caller_origin_and_constant(self):
        self.functions['0x2000'] = function('0x2000', [op('ret', 'RETURN', [constant(0), param(0)], output=False)])
        self.functions['0x1000'] = function('0x1000', [op('0x1010#0', 'CALL', [constant(0), constant(17)], target='0x2000')])
        result = self.run_slice('0x2000', parameter=0, caller='0x1000', call_site='0x1010')
        self.assertEqual('0x11', result['nodes'][0]['boundConstant']['offset'])
    def test_unverified_call_binding_remains_gap(self):
        self.functions['0x2000'] = function('0x2000', [op('ret', 'RETURN', [constant(0), param(0)])])
        self.functions['0x2000']['signatureSource'] = 'DEFAULT'
        self.functions['0x1000'] = function('0x1000', [op('call', 'CALL', [constant(0), param(0)], target='0x2000')])
        result = self.run_slice(operation='call')
        self.assertEqual(1, len(result['contexts']))
        self.assertTrue(any('unverified' in g['reason'] for g in result['unknowns']))
    def test_forward_call_input_not_assumed_return(self):
        self.functions['0x2000'] = function('0x2000', [op('ret', 'RETURN', [constant(0), constant(42)], output=False)])
        self.functions['0x1000'] = function('0x1000', [op('call', 'CALL', [constant(0), param(0)], target='0x2000')])
        result = self.run_slice(parameter=0, direction='forward')
        keys = {n['id'] for n in result['nodes']}
        self.assertIn('0x1000@root/call/invoke', keys)
        self.assertNotIn('0x1000@root/call', keys)
    def test_control_branch_is_relevant(self):
        ops = [op('condition', 'CBRANCH', [constant(1), param(0)], output=False),
               op('write', 'STORE', [constant(0), constant(100), constant(1)], block=1, output=False),
               op('ret', 'RETURN', [constant(0)], block=2, output=False)]
        self.functions['0x1000'] = function('0x1000', ops, blocks=[{'id': 0, 'successors': [1, 2]},
                                                                 {'id': 1, 'successors': [2]}, {'id': 2, 'successors': []}])
        result = self.run_slice(operation='write')
        self.assertIn('0x1000@root/parameter/0', {n['id'] for n in result['nodes']})
        self.assertTrue(any(e['kind'] == 'control-dependence' for e in result['edges']))
        result = self.run_slice(operation='write', include_control=False)
        self.assertNotIn('0x1000@root/parameter/0', {n['id'] for n in result['nodes']})
    def test_infinite_loop_control_unknown(self):
        self.functions['0x1000'] = function('0x1000', [op('loop', 'CBRANCH', [constant(0), param(0)], output=False)],
                                            blocks=[{'id': 0, 'successors': [0]}])
        result = self.run_slice(operation='loop')
        self.assertTrue(any('nonterminating' in g['reason'] for g in result['unknowns']))
    def test_raw_dead_flag_does_not_remove_live_value(self):
        item = op('add', 'INT_ADD', [param(0), constant(2)]); item['dead'] = True
        self.functions['0x1000'] = function('0x1000', [item])
        self.assertEqual(2, len(self.run_slice(operation='add')['nodes']))
    def test_budget_and_recursion_visible(self):
        self.functions['0x1000'] = function('0x1000', [op('call', 'CALL', [constant(0), param(0)], target='0x1000')])
        result = self.run_slice(operation='call')
        self.assertTrue(result['graphFrontier'])
        self.functions['0x1000'] = function('0x1000', [op('add', 'INT_ADD', [param(0), constant(2)])])
        self.assertTrue(self.run_slice(operation='add', max_slice=1)['frontier'])
    def test_missing_seed_fails(self):
        self.functions['0x1000'] = function('0x1000', [])
        with self.assertRaises(ValueError): self.run_slice(operation='missing')
    def test_memory_boundary_visible(self):
        self.functions['0x1000'] = function('0x1000', [op('read', 'LOAD', [constant(0), param(0)])])
        self.assertTrue(self.run_slice(operation='read')['unknowns'])
    def test_variadic_binding_is_not_trusted(self):
        self.functions['0x2000']=function('0x2000',[op('ret','RETURN',[constant(0),param(0)])]);self.functions['0x2000']['variadic']=True
        self.functions['0x1000']=function('0x1000',[op('call','CALL',[constant(0),param(0)],target='0x2000')])
        self.assertTrue(any('unverified' in g['reason'] for g in self.run_slice(operation='call')['unknowns']))
    def test_edge_budget_never_claims_completeness(self):
        self.functions['0x1000']=function('0x1000',[op('a','INT_ADD',[param(0),param(1)])],count=2)
        result=self.run_slice(operation='a',max_edges=1)
        self.assertEqual(1,result['omittedGraphEdges']);self.assertEqual('partial',result['status'])
    def test_data_only_does_not_require_cfg(self):
        self.functions['0x1000']=function('0x1000',[op('a','INT_ADD',[param(0),constant(1)])])
        self.functions['0x1000']['flow'].pop('blocks')
        result=self.run_slice(operation='a',include_control=False)
        self.assertFalse(any('CFG' in g['reason'] for g in result['unknowns']))


if __name__ == '__main__': unittest.main()
