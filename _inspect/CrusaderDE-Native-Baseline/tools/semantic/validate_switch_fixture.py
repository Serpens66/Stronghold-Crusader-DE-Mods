"""Validate Ghidra case observations against our PE bytes and source-owned expectations."""
import json
from pathlib import Path
import struct
import sys
from native_analysis import sha


def validate(directory,profile):
    root=Path(directory);expected=json.loads((root/'expected.json').read_text())
    function=json.loads((root/'work/1000.json').read_text())
    complete=json.loads((root/'work/COMPLETE.json').read_text())
    assert complete['profile']==profile and complete['binaryHash']==expected['binaryHash']
    assert function['profile']==profile and function['binaryHash']==sha(root/'switch.exe')==expected['binaryHash']
    assert function['flow']['status']=='complete' and function['flow']['parameters'][0]['type']=='int'
    assert len(function['flow']['switches'])==1
    switch=function['flow']['switches'][0]
    actual={(m['value'],m['target'].lower()) for m in switch['mappings']}
    wanted={(m['value'],m['target']) for m in expected['cases']}
    assert actual==wanted,(actual,wanted,switch['unknownLabels'])
    assert switch['defaultTargets']==[expected['defaultTarget'].upper().replace('0X','0x')]
    assert len(switch['rawLabels'])!=len(switch['destinations']) and not switch['rawMappings']
    assert any('counts differ' in u['reason'] for u in switch['unknownLabels'])
    assert {d['entryCount'] for d in switch['dataSources']}=={5}
    pe=(root/'switch.exe').read_bytes()
    for index,case in enumerate(expected['cases']):
        relative=struct.unpack_from('<i',pe,512+64+index*4)[0]
        assert hex(0x1040+relative)==case['target']
        position=512+int(case['target'],16)-0x1000
        assert pe[position]==0xB8 and struct.unpack_from('<I',pe,position+1)[0]==case['result'] and pe[position+5]==0xC3
    # The source-owned values share the exact same target, but retain separate case observations.
    assert next(m['target'] for m in switch['mappings'] if m['value']==-2)==next(m['target'] for m in switch['mappings'] if m['value']==-1)
    print(json.dumps({'status':'ok','fixtureHash':expected['binaryHash'],'signedCases':5,
                      'sharedCaseBody':True,'defaultObserved':True,'unequalLabelArraysNotZipped':True,'executed':False}))


if __name__=='__main__':validate(sys.argv[1],sys.argv[2])
