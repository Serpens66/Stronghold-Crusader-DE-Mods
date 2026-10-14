import unittest
from memory_accesses import Addresses, accesses, model_associations


def val(name=None, n=None, parameter=None, size=8):
    return {'definition':name,'constant':n is not None,'offset':hex(n) if n is not None else '0x0',
            'size':size,'parameterOrdinal':parameter,'space':'const' if n is not None else 'unique'}
def op(name,code,inputs,size=8):
    return {'id':name,'opcode':code,'site':'0x1000','inputs':inputs,'output':val(name,size=size)}
def fixture(ops):
    return {'functionRva':'0x1000','binaryHash':'hash','flow':{'status':'complete','operations':ops}}


class MemoryAccessTests(unittest.TestCase):
    def test_base_index_stride_field(self):
        f=fixture([op('index','INT_MULT',[val(parameter=1),val(n=8)]),op('base','INT_ADD',[val(parameter=0),val(n=32)]),
                   op('address','INT_ADD',[val('base'),val('index')]),op('load','LOAD',[val(n=0),val('address')],size=4)])
        model={'id':'table','binaryHash':'hash','root':{'kind':'parameter','functions':['0x1000'],'ordinal':0},'stride':8,
               'fields':[{'name':'price','offset':32,'size':4}],'evidence':{'source':'fixture'}}
        result=accesses(f,[model]);read=result['accesses'][0]
        self.assertEqual({'parameter/0':1,'parameter/1':8},read['expressions'][0]['terms'])
        self.assertEqual('price',read['modelAssociations'][0]['field'])
    def test_absolute_array_root_and_partial_field(self):
        model={'id':'global','binaryHash':'hash','root':{'kind':'absolute-inline','rva':'0x2000'},'stride':16,
               'fields':[{'name':'word','offset':8,'size':4}],'evidence':{}}
        expression={'constant':0x180002009,'terms':{'index':16},'unknowns':[]}
        result=model_associations(fixture([]),expression,2,[model]);self.assertEqual(1,result[0]['withinField'])
        self.assertFalse(model_associations(fixture([]),expression,4,[model]))
    def test_unknown_extent_is_not_invented(self):
        model={'id':'field','binaryHash':'hash','root':{'kind':'parameter','functions':['0x1000'],'ordinal':0},
               'fields':[{'name':'x','offset':8,'size':None}],'evidence':{}}
        result=model_associations(fixture([]),{'constant':8,'terms':{'parameter/0':1},'unknowns':[]},2,[model])
        self.assertEqual('unverified',result[0]['fieldExtent'])
    def test_wrong_binary_and_stride_rejected(self):
        model={'id':'table','binaryHash':'other','root':{'kind':'parameter','functions':['0x1000'],'ordinal':0},'stride':8,
               'fields':[{'name':'x','offset':0,'size':4}],'evidence':{}}
        self.assertFalse(model_associations(fixture([]),{'constant':0,'terms':{'parameter/0':1,'index':16},'unknowns':[]},4,[model]))
    def test_pointer_add_and_sub_are_address_arithmetic(self):
        f=fixture([op('a','PTRADD',[val(parameter=0),val(parameter=1),val(n=16)]),op('b','PTRSUB',[val('a'),val(n=4)])])
        expression=Addresses(f).resolve(val('b'))[0];self.assertEqual(4,expression['constant']);self.assertEqual(16,expression['terms']['parameter/1'])
    def test_memory_load_does_not_become_pointer_alias(self):
        f=fixture([op('p','LOAD',[val(n=0),val(n=0x180002000)]),op('a','INT_ADD',[val('p'),val(n=8)])])
        expression=Addresses(f).resolve(val('a'))[0];self.assertTrue(expression['unknowns']);self.assertIn('value/p',expression['terms'])
    def test_phi_alternatives_and_budget(self):
        f=fixture([op('p','MULTIEQUAL',[val(n=1),val(n=2),val(n=3)])])
        self.assertEqual(3,len(Addresses(f).resolve(val('p'))))
        result=Addresses(f,max_alternatives=2).resolve(val('p'));self.assertIn('budget',result[0]['unknowns'][0])
    def test_cycle_retains_unknown(self):
        f=fixture([op('p','INT_ADD',[val('p'),val(n=1)])])
        self.assertTrue(Addresses(f).resolve(val('p'))[0]['unknowns'])
    def test_nonlinear_and_truncation_not_assumed_affine(self):
        f=fixture([op('mul','INT_MULT',[val(parameter=0),val(parameter=1)]),op('short','CAST',[val('mul')],size=4)])
        self.assertTrue(Addresses(f).resolve(val('mul'))[0]['unknowns'])
        self.assertTrue(Addresses(f).resolve(val('short',size=4))[0]['unknowns'])
    def test_integer_wrap_is_explicit(self):
        f=fixture([op('sum','INT_ADD',[val(n=255,size=1),val(n=2,size=1)],size=1)])
        result=Addresses(f).resolve(val('sum',size=1))[0];self.assertEqual(1,result['constant']);self.assertEqual(1,result['width'])
    def test_sign_extension_keeps_signed_index_distinct(self):
        f=fixture([op('s','INT_SEXT',[val(parameter=1,size=4)]),op('m','INT_MULT',[val('s'),val(n=16)])])
        term=Addresses(f).resolve(val('m'))[0]['terms'];self.assertEqual([16],list(term.values()));self.assertIn('int_sext32',next(iter(term)))
    def test_unselected_flow_is_unavailable_not_empty_success(self):
        f=fixture([]);f['flow']['status']='not-selected';self.assertEqual('unavailable',accesses(f)['status'])
    def test_nested_decompiler_type_is_candidate(self):
        f=fixture([op('a','INT_ADD',[val(parameter=0),val(n=8)]),op('read','LOAD',[val(n=0),val('a')],size=4)])
        f['flow']['parameters']=[{'ordinal':0,'pointerTargetType':'/outer'}]
        layouts={'/outer':{'kind':'StructureDB','fields':[{'name':'nested','offset':4,'size':8,'typePath':'/inner','ordinal':0}]},
                 '/inner':{'kind':'StructureDB','fields':[{'name':'value','offset':4,'size':4,'typePath':'/int','ordinal':0,'bitField':False}]}}
        associations=accesses(f,layouts=layouts)['accesses'][0]['typeAssociations']
        self.assertTrue(any(a['fieldPath']==['nested','value'] for a in associations))
        self.assertTrue(all('candidate' in a['authority'] for a in associations))
    def test_typed_array_retains_index_and_bounds_unknown(self):
        f=fixture([op('i','INT_MULT',[val(parameter=1),val(n=4)]),op('a','INT_ADD',[val(parameter=0),val('i')]),
                   op('read','LOAD',[val(n=0),val('a')],size=4)])
        f['flow']['parameters']=[{'ordinal':0,'pointerTargetType':'/record'}]
        layouts={'/record':{'fields':[{'name':'array','offset':0,'size':40,'typePath':'/array','ordinal':0}]},
                 '/array':{'array':{'count':10,'elementSize':4,'typePath':'/int'}}}
        result=accesses(f,layouts=layouts)['accesses'][0]
        self.assertEqual({'parameter/1':4},result['typeAssociations'][0]['indexTerms'])


if __name__=='__main__':unittest.main()
