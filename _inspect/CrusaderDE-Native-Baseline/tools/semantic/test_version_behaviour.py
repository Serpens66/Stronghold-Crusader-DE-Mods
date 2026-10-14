import copy
import json
import unittest
from version_behaviour import compare,rows,value_shape


def fixture(rva,binary):
    return {'functionRva':rva,'binaryHash':binary,'instructions':[{'bytes':'C3','mnemonic':'Ret'}],
            'flow':{'status':'complete','operations':[],'parameters':[],'blocks':[{'id':0,'successors':[]}]}}
def constant(n):return {'constant':True,'offset':hex(n),'size':8}


class VersionBehaviourTests(unittest.TestCase):
    def setUp(self):self.old=fixture('0x1000','old');self.new=fixture('0x2000','new')
    def test_no_difference_is_not_equivalence(self):
        result=compare(self.old,self.new,{'confidence':'candidate'})
        self.assertEqual('no-difference-in-compared-observations',result['status'])
        self.assertEqual('candidate',result['match']['confidence']);self.assertTrue(result['limits'])
    def test_field_address_change_visible(self):
        operation={'id':'a','opcode':'LOAD','inputs':[constant(0),constant(0x180003000)],'output':{'size':4}}
        self.old['flow']['operations']=[operation];self.new['flow']['operations']=[copy.deepcopy(operation)]
        self.new['flow']['operations'][0]['inputs'][1]=constant(0x180003004)
        self.assertIn('memory',compare(self.old,self.new,{})['differences'])
    def test_condition_change_visible(self):
        operations=[{'id':'condition','opcode':'INT_LESS','inputs':[{'parameterOrdinal':0,'size':4},constant(10)]},
                    {'id':'branch','opcode':'CBRANCH','inputs':[constant(0),{'definition':'condition','size':1}]}]
        self.old['flow']['operations']=operations;self.new['flow']['operations']=copy.deepcopy(operations)
        self.new['flow']['operations'][0]['opcode']='INT_SLESS'
        self.assertIn('conditions',compare(self.old,self.new,{})['differences'])
    def test_unavailable_flow_does_not_manufacture_deletions(self):
        self.old['flow']['status']='failed'
        self.new['flow']['operations']=[{'id':'a','opcode':'LOAD','inputs':[constant(0),constant(100)],'output':{'size':4}}]
        result=compare(self.old,self.new,{})
        self.assertNotIn('memory',result['differences']);self.assertEqual('partial',result['completeness'])
    def test_confirmed_callee_identity_normalizes_relocation(self):
        call={'id':'c','opcode':'CALL','inputs':[constant(0)],'callTarget':'0x3000'}
        self.old['flow']['operations']=[call];self.new['flow']['operations']=[{**call,'callTarget':'0x4000'}]
        self.assertNotIn('calls',compare(self.old,self.new,{},call_map={'0x3000':'0x4000'})['differences'])
        self.assertIn('calls',compare(self.old,self.new,{})['differences'])
    def test_cycles_and_dags_remain_bounded(self):
        operations={'phi':{'opcode':'MULTIEQUAL','inputs':[{'definition':'phi','size':8},constant(0)]}}
        shape=value_shape({'definition':'phi','size':8},operations)
        self.assertIn('cyclic',str(shape));self.assertLess(len(json.dumps(shape)),1000)
        for i in range(100):operations[str(i)]={'opcode':'INT_ADD','inputs':[{'definition':str(i-1),'size':8}]*2}
        shape=value_shape({'definition':'99','size':8},operations);self.assertLess(len(json.dumps(shape)),1000)
    def test_feature_membership_is_candidate(self):
        match={'oldRva':'0x1000','newRva':'0x2000','confidence':'probable'}
        result=list(rows([self.old],[self.new],[match],[{'id':'feature','relatedFunctions':['0x2000']}]))[0]
        self.assertEqual(['feature'],result['affectedFeatureCandidates']);self.assertEqual('probable',result['match']['confidence'])


if __name__=='__main__':unittest.main()
