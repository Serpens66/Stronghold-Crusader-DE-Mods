"""Read appended Bridge traces; delivery, capture and torn files are separate facts."""
from pathlib import Path
import argparse, collections, re, sys

START = re.compile(r'\[Message:\s*BepInEx\] BepInEx .* - Stronghold Crusader Definitive Edition')
FIELD = re.compile(r'(?:^|,)([A-Za-z][A-Za-z0-9]*)=')

def fields(text):
    matches = list(FIELD.finditer(text))
    result = {}
    for i, match in enumerate(matches):
        result.setdefault(match[1], text[match.end():matches[i+1].start() if i+1<len(matches) else len(text)])
    return result

def analyze(raw):
    text = raw.decode('utf-8-sig', errors='replace')
    starts = list(START.finditer(text))
    if starts: text = text[starts[-1].start():]
    records, torn = [], []
    for line in text.splitlines():
        if 'bridge trace seq=' not in line: continue
        item = fields(line.split('bridge trace ',1)[1])
        if 'kind' not in item or any(token in line and ']' not in line.split(token,1)[1] for token in ('rows=[','rawRows=[','fragments=[')):
            torn.append(line); continue
        records.append((line,item))
    definitions = collections.defaultdict(set)
    categories = {'bridge-physical':'physical','bridge-planning-fields':'planning','live-state':'state',
                  'candidate-table':'candidate','text-definition':'text','player-group-definition':'groups'}
    for _, f in records:
        if f['kind'] in categories: definitions[categories[f['kind']]].add(int(f.get('definition',f.get('state','0'))))
    missing=[]
    def require(category,value,f):
        if value and int(value)!=0 and int(value) not in definitions[category]: missing.append((f['seq'],category,int(value)))
    for _,f in records:
        kind=f['kind']
        if 'contextDefinition' in f: require('text',f['contextDefinition'],f)
        if 'state' in f and kind!='live-state': require('state',f['state'],f)
        if kind=='player-groups' and 'definition' in f: require('groups',f['definition'],f)
        if kind=='candidate-table' and 'baseDefinition' in f: require('candidate',f['baseDefinition'],f)
        if kind in ('task-selection',): require('candidate',f.get('candidateDefinition','0'),f)
        if kind=='live-state':
            for row in f['fragments'].strip('[]').split(';'):
                if row:
                    _,_,physical,planning=map(int,row.split('/'));require('physical',physical,f);require('planning',planning,f)
        if kind=='candidate-table-reference-batch':
            for row in f['rows'].strip('[]').split(';'):
                if row: require('candidate',row.split('/')[3],f)
    ends=[f for _,f in records if f['kind']=='session-end']
    delivered={f['session']:f for _,f in records if f['kind']=='session-delivered'}
    capture = bool(ends) and all(f.get('captureComplete')=='True' and ('entered' not in f or f['entered']==f.get('exited')) and ('commandPre' not in f or f['commandPre']==f.get('commandPost')) for f in ends)
    delivery = bool(ends) and all(f['session'] in delivered and delivered[f['session']].get('deliveryComplete')=='True' for f in ends)
    file_complete=not torn and (not raw or raw.endswith(b'\n')) and '\ufffd' not in text
    volumes=collections.Counter()
    for line,f in records: volumes[f['kind']]+=len((line+'\r\n').encode('utf-8'))
    return dict(records=records,torn=torn,missing=missing,captureComplete=capture,deliveryComplete=delivery,
                fileComplete=file_complete,complete=capture and delivery and file_complete and not missing,
                volumes=volumes,definitions={k:len(v) for k,v in definitions.items()})

def self_test():
    prefix='[Info : Bridge] bridge trace seq='
    raw=(prefix+'1,session=1,kind=session-end,captureComplete=True,deliveryComplete=False\r\n'+
         prefix+'2,session=1,kind=session-delivered,captureComplete=True,deliveryComplete=True\r\n').encode()
    assert analyze(raw)['complete']
    assert not analyze(raw+prefix.encode()+b'3,session=1,kin')['fileComplete']
    assert not analyze(raw.splitlines(keepends=True)[0])['deliveryComplete']
    assert not analyze(raw+ (prefix+'3,session=1,kind=native-enter,contextDefinition=99\r\n').encode())['complete']
    assert not analyze(raw.replace(b'captureComplete=True',b'captureComplete=False'))['captureComplete']
    print('PASS analyzer: bounded completion, missing definitions and torn exit detected')

def main():
    parser=argparse.ArgumentParser();parser.add_argument('log',nargs='?');parser.add_argument('--self-test',action='store_true');args=parser.parse_args()
    if args.self_test:self_test()
    if not args.log:return
    result=analyze(Path(args.log).read_bytes())
    for key in ('captureComplete','deliveryComplete','fileComplete','complete','definitions','missing'):print(key,result[key])
    print('records',len(result['records']),'torn',len(result['torn']),'traceBytesWithPrefixes',sum(result['volumes'].values()))
    print('volumeByKind',result['volumes'].most_common(12))
    for _,f in result['records']:
        if f['kind'] in ('session-end','session-delivered','stored-route','bridge-movement'):print(f)

if __name__=='__main__':main()
