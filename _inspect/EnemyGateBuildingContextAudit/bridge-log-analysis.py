"""Read appended Bridge traces; delivery, capture and torn files are separate facts."""
from pathlib import Path
import argparse, collections, re, sys

START = re.compile(r'\[Message:\s*BepInEx\] BepInEx .* - Stronghold Crusader Definitive Edition')
FIELD = re.compile(r'(?:^|,)([A-Za-z][A-Za-z0-9]*)=')

def fields(text):
    # Only top-level fields delimit values. A text/context definition may contain
    # fields with identical names; those are data, not outer record metadata.
    matches=[];depth=0;position=0
    for match in FIELD.finditer(text):
        for ch in text[position:match.start()]:
            if ch=='[': depth+=1
            elif ch==']': depth-=1
        position=match.start()
        if depth==0: matches.append(match)
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
    # Bounded lossless native rows preserve their original envelopes; the batch
    # envelope is delivery timing only. Missing/truncated rows remain incomplete.
    wire_transport=list(records)
    unbatched=[]
    for line,f in records:
        if f['kind']!='native-frame-batch':unbatched.append((line,f));continue
        for row in f.get('rows','[]').strip('[]').split(';'):
            if not row:continue
            try:
                tokens=[]
                for value in row.split('/'):
                    if re.fullmatch(r'z[0-9]+',value):
                        n=int(value[1:]);assert 3<=n<=37;tokens.extend(['0']*n)
                    else:tokens.append(value)
                assert len(tokens)==37 and tokens[7] in ('0','1')
                item=dict(zip(('seq','session','thread','tick','clock','physical','topology'),tokens[:7]))
                item.update(kind='native-frame',phase='pre' if tokens[7]=='0' else 'post',rva='0x'+tokens[8],site=tokens[9],contextDefinition=tokens[10],values='['+'/'.join(tokens[11:])+']')
                unbatched.append((line,item))
            except (AssertionError,ValueError):torn.append(line)
    records=unbatched
    command_contexts={f['definition']:f for _,f in records if f['kind']=='command-context'}
    expanded=[]
    for line,f in records:
        if f['kind']=='native-frame':
            v=f.get('values','[]').strip('[]').split('/')
            try:
                assert len(v)==26 and f['phase'] in ('pre','post')
                for n,value in enumerate(v):
                    if n!=18 or value!='v':int(value)
                f=dict(f,kind='native-enter' if f['phase']=='pre' else 'native-exit',op=v[0],parent=v[1],
                       args='['+'/'.join(v[2:8])+']',scopeSession=v[8],state=v[9])
                if f['phase']=='pre':
                    for name,value in zip(('global','owner','leader','state16','phase16','retainedTarget16','targetGlobal'),v[10:17]):f[name]=value
                else:f.update(completed=str(v[17]=='1'),observedReturn='void' if v[18]=='v' else v[18],forwardedReturn='unchanged',nativeCalls='1',regions=v[19])
                if f.get('rva')=='0x3C2E0':f['retainedEntryPlanStamp']='['+'/'.join(v[20:26])+']'
            except (ValueError,AssertionError,KeyError):torn.append(line);continue
        if f['kind'] in ('command-pre','command-post') and f.get('commandContext') in command_contexts:
            context=command_contexts[f['commandContext']]
            v=context.get('values','[]').strip('[]').split('/')
            try:
                assert len(v)==5 and context['session']==f['session']
                for value in v:int(value)
                f=dict(f)
                for name,value in zip(('parent','priorPlayerPlan','priorPlanPhysical','priorPlanTopology','state'),v):f.setdefault(name,value)
                f.setdefault('priorLink',context['priorLink'])
            except (ValueError,AssertionError,KeyError):torn.append(line);continue
        expanded.append((line,f))
    # Keep byte volume on the actual transport, before semantic expansion.
    actual_transport=wire_transport
    records=expanded
    # Transport batches are expanded before reference/chain validation. Volume
    # remains the actual on-disk transport, not synthetic expanded records.
    transport=list(records)
    replacements=[];background_repeats=collections.Counter()
    for line,f in transport:
        if f['kind'] in ('route-replacement-batch','route-background-repeat-batch'):
            for row in f.get('rows','[]').strip('[]').split(';'):
                if not row:continue
                v=row.split('/')
                try:
                    assert len(v)==(5 if f['kind']=='route-replacement-batch' else 2)
                    values=list(map(int,v))
                    if f['kind']=='route-replacement-batch':replacements.append(dict(session=f['session'],unit=values[0],globalId=values[1],oldCommand=values[2],newCommand=values[3],observationClock=values[4]))
                    else:background_repeats[values[0]]+=values[1]
                except (AssertionError,ValueError):torn.append(line)
        if f['kind']!='stored-route-background-batch':continue
        for row in f.get('rows','[]').strip('[]').split(';'):
            if not row:continue
            v=row.split('/')
            if len(v)!=12 or not all(re.fullmatch(r'-?\d+',value) for value in v):
                torn.append(line);continue
            p=dict(f,kind='stored-route',definition=v[0],captureClock=v[1],origin=v[2]+'/'+v[3],
                   length=v[4],cursor=v[5],flags=v[6],substep=v[7],decodedEndpoint=v[8]+'/'+v[9],
                   nativeSegmentTarget=v[10]+'/'+v[11],complete='True',bridges='[]',packedHex='',physical='-1',topology='-1',envelopeTiming='batch-flush')
            records.append((line,p))
    definitions = collections.defaultdict(set)
    categories = {'bridge-physical':'physical','bridge-planning-fields':'planning','live-state':'state',
                  'candidate-table':'candidate','text-definition':'text','player-group-definition':'groups',
                  'stored-route':'path','decision-state':'decision','command-context':'command-context'}
    for _, f in records:
        if f['kind'] in categories: definitions[categories[f['kind']]].add(int(f.get('definition',f.get('state','0'))))
    bindings={};path_records={f['definition']:f for _,f in records if f['kind']=='stored-route'}
    for _,f in records:
        if f['kind']=='route-bindings':
            for row in f['rows'].strip('[]').split(';'):
                if row:
                    values=row.split('/');definitions['binding'].add(int(values[0]));bindings[values[0]]=(f,values)
    missing=[]
    def require(category,value,f):
        if value and int(value)!=0 and int(value) not in definitions[category]: missing.append((f['seq'],category,int(value)))
    for _,f in records:
        kind=f['kind']
        if 'contextDefinition' in f: require('text',f['contextDefinition'],f)
        if 'commandContext' in f:require('command-context',f['commandContext'],f)
        if 'state' in f and kind!='live-state': require('state',f['state'],f)
        if kind=='player-groups' and 'definition' in f: require('groups',f['definition'],f)
        if kind=='candidate-table' and 'baseDefinition' in f: require('candidate',f['baseDefinition'],f)
        if kind in ('task-selection',): require('candidate',f.get('candidateDefinition','0'),f)
        if kind=='live-state':
            for row in f['fragments'].strip('[]').split(';'):
                if row:
                    _,_,physical,planning=map(int,row.split('/'));require('physical',physical,f);require('planning',planning,f)
        if kind=='route-bindings':
            for row in f['rows'].strip('[]').split(';'):
                if row: require('decision',row.split('/')[12],f)
        if kind in ('route-observations','route-repeat-batch'):
            for row in f['rows'].strip('[]').split(';'):
                if row:
                    values=row.split('/');require('binding',values[0],f);require('path',values[1],f)
                    if kind=='route-observations': require('state',values[11],f)
        if 'decisionState' in f: require('decision',f['decisionState'],f)
        if kind=='route-unbound-update': require('path',f['pathDefinition'],f)
        if kind=='candidate-table-reference-batch':
            for row in f['rows'].strip('[]').split(';'):
                if row: require('candidate',row.split('/')[3],f)
    reconstruction=[];chains=[];observations=[]
    directions=((0,-1),(1,-1),(1,0),(1,1),(0,1),(-1,1),(-1,0),(-1,-1))
    for definition,f in path_records.items():
        if not f.get('packedHex'): continue
        try:
            packed=bytes.fromhex(f['packedHex']);length=int(f['length']);x,y=map(int,f['origin'].split('/'));positions=[(x,y)]
            assert len(packed)==(length+1)//2, 'packed capacity'
            decoded=True
            for i in range(length):
                raw_direction=(packed[i//2]>>(4*(i%2)))&15
                if raw_direction>7: decoded=False;break
                dx,dy=directions[raw_direction];x+=dx;y+=dy;positions.append((x,y))
                if not (0<=x<800 and 0<=y<800): decoded=False;break
            if f.get('complete')=='True':
                assert decoded and f'{x}/{y}'==f['decodedEndpoint'], 'decoded endpoint'
                for row in f['bridges'].strip('[]').split(';'):
                    if not row:continue
                    v=row.split('/');first,last=int(v[2]),int(v[3])
                    assert positions[first]==(int(v[4]),int(v[5])), 'deck entry'
                    assert positions[min(last+2,length)]==(int(v[6]),int(v[7])), 'deck exit'
        except (ValueError,IndexError,AssertionError) as error: reconstruction.append((f['seq'],definition,str(error)))
    native_roots={f.get('op'):f for _,f in records if f['kind']=='native-enter'}
    decision_records={f['definition']:f for _,f in records if f['kind']=='decision-state'}
    commands={f.get('op'):f for _,f in records if f['kind']=='command-pre'}
    for _,f in records:
        if f['kind']=='route-observations':
            for row in f['rows'].strip('[]').split(';'):
                if row:observations.append((f,row.split('/')))
        elif f['kind']=='stored-route' and f.get('bridges')!='[]' and f.get('unit'):
            # Legacy records have direct identity but no durable decision-state reference.
            chains.append(dict(session=f['session'],unit=f['unit'],planningRoot=f.get('planningRoot'),command=f.get('commandOp'),path=f['definition'],decision=None,association='legacy-unproven'))
    for f,v in observations:
        binding=bindings.get(v[0]);path=path_records.get(v[1])
        if not binding or not path or path.get('bridges')=='[]':continue
        _,b=binding;decision=decision_records.get(b[12]);root=native_roots.get(b[9]);command=commands.get(b[6]);parent=commands.get(b[7])
        relation=[]
        for section in path['bridges'].strip('[]').split(';'):
            if section:
                values=section.split('/');cursor=int(v[5]);first,last=int(values[2]),int(values[3])
                relation.append((values[0], 'before-cursor' if cursor<=first else 'cursor-boundary-uncertain' if cursor<=last+1 else 'cursor-past-not-execution'))
        valid_decision=bool(decision and root and decision.get('player')==b[3] and decision.get('session')==f['session'] and decision.get('consumedPlan')==root.get('retainedEntryPlanStamp'))
        accesses=[row.split('/') for row in (decision or {}).get('accesses','[]').strip('[]').split(';') if row]
        positive=any(len(row)==13 and row[9]=='1' and int(row[12])>0 and int(row[8])!=0 and int(row[11])!=0 for row in accesses)
        chains.append(dict(session=f['session'],unit=b[1]+'/g'+b[2],planningRoot=b[9],command=b[6],parent=b[7],path=v[1],decision=b[12],decisionMatches=valid_decision,
                           rootRecorded=bool(root),commandRecorded=bool(command),groupRecorded=bool(parent),deckCursorRelation=relation,
                           positiveAccessObserved=valid_decision and positive,
                           accessEvidence='positive-region-present-consumed-branch-not-inferred' if valid_decision and positive else 'no-positive-region-proof',
                           association='retained-caller-state' if valid_decision else 'unresolved',position=v[3]+'/'+v[4]))
    ends=[f for _,f in records if f['kind']=='session-end']
    delivered={f['session']:f for _,f in records if f['kind']=='session-delivered'}
    capture = bool(ends) and all(f.get('captureComplete')=='True' and ('entered' not in f or f['entered']==f.get('exited')) and ('commandPre' not in f or f['commandPre']==f.get('commandPost')) for f in ends)
    delivery = bool(ends) and all(f['session'] in delivered and delivered[f['session']].get('deliveryComplete')=='True' for f in ends)
    file_complete=not torn and (not raw or raw.endswith(b'\n')) and '\ufffd' not in text
    tail=text.splitlines()[-1] if text.splitlines() else ''
    bridge_file_complete=not torn and ('bridge trace ' not in tail or raw.endswith(b'\n')) and not any('\ufffd' in line for line,_ in transport)
    chain_groups=[]
    for session,root,decision in sorted(set((c.get('session'),c.get('planningRoot'),c.get('decision')) for c in chains),key=str):
        group=[c for c in chains if c.get('session')==session and c.get('planningRoot')==root and c.get('decision')==decision]
        chain_groups.append(dict(session=session,planningRoot=root,decision=decision,observations=len(group),
                                 units=len({c['unit'] for c in group}),commands=len({c['command'] for c in group}),
                                 groups=len({c.get('parent') for c in group if c.get('parent') not in (None,'0')}),
                                 paths=len({c['path'] for c in group}),
                                 positiveAccessObservations=sum(c.get('positiveAccessObserved',False) for c in group),
                                 linkedObservations=sum(bool(c.get('decisionMatches') and c.get('rootRecorded') and c.get('commandRecorded') and c.get('groupRecorded')) for c in group)))
    movement=[f for _,f in records if f['kind']=='bridge-movement']
    movement_summary=[]
    for player in sorted({f['player'] for f in movement},key=int):
        rows=[f for f in movement if f['player']==player]
        entered={(f['session'],f['unit'],f['building']) for f in rows if f['stage']=='enter'}
        exited={(f['session'],f['unit'],f['building']) for f in rows if f['stage']=='exit'}
        movement_summary.append(dict(player=player,observations=len(rows),units=len({(f['session'],f['unit']) for f in rows}),
                                     commands=len({(f['session'],f['commandOp']) for f in rows}),
                                     unitsWithEntryAndExit=len(entered&exited),unmatchedEntries=len(entered-exited),unmatchedExits=len(exited-entered)))
    volumes=collections.Counter()
    for line,f in actual_transport: volumes[f['kind']]+=len((line+'\r\n').encode('utf-8'))
    virtual_inputs={(f['session'],f['definition']):f for _,f in records if f['kind']=='virtual-shadow-input'}
    virtual_results={(f['session'],f['definition']):f for _,f in records if f['kind']=='virtual-shadow'}
    virtual_cancelled=set()
    for _,f in records:
        if f['kind']=='virtual-shadow' and (f['session'],f.get('definition')) not in virtual_inputs:
            missing.append((f['seq'],'virtual-input',f.get('definition')))
        if f['kind']=='virtual-shadow-end':
            virtual_cancelled.update((f['session'],v) for v in f.get('cancelledDefinitions','[]').strip('[]').split(';') if v)
        if f['kind']=='virtual-shadow-reference' and (f['session'],f.get('sourceDefinition')) not in virtual_inputs:
            missing.append((f['seq'],'virtual-input',f.get('sourceDefinition')))
    virtual_pending=set(virtual_inputs)-set(virtual_results)-virtual_cancelled
    virtual_summary=dict(instrumented=bool(virtual_inputs),inputs=len(virtual_inputs),results=len(virtual_results),cancelled=len(virtual_cancelled),pending=len(virtual_pending),
                         policyUnknown=sum(f.get('policyResult')=='Unknown' for f in virtual_results.values()),
                         geometricResults=dict(collections.Counter(f.get('geometricResult') for f in virtual_results.values())))
    native_ready=bool(ends) and all(int(f.get('installedEntries','0'))>0 and f.get('installedEntries')==f.get('expectedEntries',f.get('installedEntries')) for f in ends)
    native_observed=bool(ends) and all(int(f.get('sessionNativeCalls',f.get('entered','0')))>0 for f in ends)
    native_coverage=native_ready and native_observed and capture and not missing
    shadow_captures=sum(f['kind']=='virtual-topology' for _,f in records)
    shadow_coverage=native_coverage and bool(ends) and all(
        any(f['kind']=='virtual-topology' and f['session']==end['session'] for _,f in records) and
        any(key[0]==end['session'] for key in virtual_results) and not any(key[0]==end['session'] for key in virtual_pending)
        for end in ends) and not missing
    coverage=dict(eventCaptureComplete=capture,nativeReady=native_ready,nativeCallsObserved=native_observed,nativeCoverageComplete=native_coverage,
                  coherentTopologyCaptures=shadow_captures,shadowCoverageComplete=shadow_coverage,
                  usableGeometricResults=sum(f.get('geometricResult') in ('Reachable','NoRoute') for f in virtual_results.values()))
    return dict(coverage=coverage,virtualSummary=virtual_summary,records=records,torn=torn,missing=missing,captureComplete=capture,deliveryComplete=delivery,
                bridgeFileComplete=bridge_file_complete,bridgeComplete=capture and delivery and bridge_file_complete and not missing and not reconstruction,
                fileComplete=file_complete,complete=capture and delivery and file_complete and not missing and not reconstruction,
                reconstructionErrors=reconstruction,routeChains=chains,routeChainGroups=chain_groups,transportRecords=len(wire_transport),
                movementSummary=movement_summary,uniqueBridgeCommands=len({(c['session'],c['command']) for c in chains}),
                uniqueBridgeUnits=len({(c['session'],c['unit']) for c in chains}),
                routeReplacements=replacements,backgroundRouteRepeats=dict(background_repeats),
                volumes=volumes,definitions={k:len(v) for k,v in definitions.items()})

def self_test():
    prefix='[Info : Bridge] bridge trace seq='
    raw=(prefix+'1,session=1,kind=session-end,captureComplete=True,deliveryComplete=False\r\n'+
         prefix+'2,session=1,kind=session-delivered,captureComplete=True,deliveryComplete=True\r\n').encode()
    assert analyze(raw)['complete']
    unrelated=analyze(raw+b'[Debug :UU-ImGUI API')
    assert unrelated['bridgeComplete'] and not unrelated['fileComplete']
    assert not analyze(raw+prefix.encode()+b'3,session=1,kin')['fileComplete']
    assert not analyze(raw+prefix.encode()+b'3,session=1,kin')['bridgeComplete']
    assert not analyze(raw.splitlines(keepends=True)[0])['deliveryComplete']
    assert not analyze(raw+ (prefix+'3,session=1,kind=native-enter,contextDefinition=99\r\n').encode())['complete']
    assert not analyze(raw.replace(b'captureComplete=True',b'captureComplete=False'))['captureComplete']
    nested=fields('kind=native-enter,session=1,context=[kind=wrong,session=2,rows=[1;]],state=3')
    assert nested['kind']=='native-enter' and nested['session']=='1' and nested['state']=='3' and 'rows' not in nested
    path=(prefix+'3,session=1,kind=stored-route,definition=1,origin=1/1,length=3,complete=True,decodedEndpoint=4/1,bridges=[703/1/0/1/1/1/4/1/deck-without-parent-footprint;],packedHex=2202\r\n').encode()
    assert not analyze(raw+path)['reconstructionErrors']
    assert analyze(raw+path.replace(b'2202',b'2204'))['reconstructionErrors']
    extra=(prefix+'4,session=1,kind=decision-state,definition=9,player=8,consumedPlan=[0/6/1/0/0/0]\r\n'+
           prefix+'5,session=1,kind=native-enter,op=20,retainedEntryPlanStamp=[0/6/1/0/0/0]\r\n'+
           prefix+'6,session=1,kind=command-pre,op=21,commandKind=move\r\n'+
           prefix+'7,session=1,kind=command-pre,op=22,commandKind=unit\r\n'+
           prefix+'8,session=1,kind=route-bindings,rows=[1/1/1/8/8/3/22/21/20/20/6/0/9/0;]\r\n'+
           prefix+'9,session=1,kind=route-observations,rows=[1/1/0/1/1/3/2/0/1/1/1/0/4/3/4/1;]\r\n').encode()
    linked=analyze(raw+path+extra)
    assert linked['complete'] and linked['routeChains'][0]['decisionMatches'] and linked['routeChains'][0]['groupRecorded']
    assert not linked['routeChains'][0]['positiveAccessObserved']
    positive_extra=extra.replace(b'consumedPlan=[0/6/1/0/0/0]',b'consumedPlan=[0/6/1/0/0/0],accesses=[3/0/1/1/1/2/103/1/1/1/103/103/1;]')
    assert analyze(raw+path+positive_extra)['routeChains'][0]['positiveAccessObserved']
    assert linked['routeChains'][0]['deckCursorRelation'][0][1]=='cursor-past-not-execution'
    assert not analyze(raw+path+extra.replace(b'consumedPlan=[0/6/1/0/0/0]',b'consumedPlan=[0/6/2/0/0/0]'))['routeChains'][0]['decisionMatches']
    assert not analyze(raw+path+extra.replace(b'/6/0/9/0;',b'/6/0/99/0;'))['complete']
    batch=(prefix+'10,session=1,kind=stored-route-background-batch,rows=[2/0/1/1/3/0/2/0/4/1/4/1;]\r\n').encode()
    compact=analyze(raw+path+extra+batch)
    assert compact['bridgeComplete'] and compact['definitions']['path']==2
    assert compact['routeChainGroups'][0]['commands']==1
    assert sum(compact['volumes'].values())==len(raw+path+extra+batch)
    assert not analyze(raw+batch.replace(b'/4/1;',b'/4;'))['bridgeComplete']
    bad_frame=(prefix+'11,session=1,kind=native-frame,phase=pre,values=[1/2]\r\n').encode()
    assert not analyze(raw+bad_frame)['bridgeComplete']
    bad_context=(prefix+'11,session=1,kind=command-pre,op=50,commandContext=99\r\n').encode()
    assert not analyze(raw+bad_context)['bridgeComplete']
    shadow=(prefix+'20,session=1,kind=virtual-shadow-input,definition=1,op=40,mode=0\r\n'+
            prefix+'21,session=1,kind=virtual-shadow-input,definition=2,op=40,mode=1\r\n'+
            prefix+'22,session=1,kind=virtual-shadow,definition=1,geometricResult=NoRoute,policyResult=Unknown\r\n'+
            prefix+'23,session=1,kind=virtual-shadow-reference,sourceDefinition=1,op=41\r\n'+
            prefix+'24,session=1,kind=virtual-shadow-end,cancelledDefinitions=[2]\r\n').encode()
    summary=analyze(raw+shadow)['virtualSummary']
    assert summary['inputs']==2 and summary['results']==1 and summary['cancelled']==1 and summary['pending']==0 and summary['policyUnknown']==1
    assert analyze(raw+shadow.splitlines(keepends=True)[0])['virtualSummary']['pending']==1
    assert analyze(raw+shadow.replace(b'sourceDefinition=1',b'sourceDefinition=99'))['missing']
    assert analyze(raw+shadow.replace(b'kind=virtual-shadow,definition=1',b'kind=virtual-shadow,definition=99'))['missing']
    unavailable=analyze(raw.replace(b'kind=session-end,',b'kind=session-end,installedEntries=0,entered=0,exited=0,'))
    assert unavailable['bridgeComplete'] and not unavailable['coverage']['nativeCoverageComplete'] and not unavailable['coverage']['shadowCoverageComplete']
    print('PASS analyzer: nested fields, exact low-first bytes, decision/group links, consumed-cursor boundaries, bounded completion and torn exit')

def main():
    parser=argparse.ArgumentParser();parser.add_argument('log',nargs='?');parser.add_argument('--self-test',action='store_true');args=parser.parse_args()
    if args.self_test:self_test()
    if not args.log:return
    result=analyze(Path(args.log).read_bytes())
    for key in ('captureComplete','deliveryComplete','bridgeFileComplete','bridgeComplete','fileComplete','complete','coverage','definitions','missing','reconstructionErrors','routeChainGroups','movementSummary','uniqueBridgeCommands','uniqueBridgeUnits','virtualSummary'):print(key,result[key])
    print('records',len(result['records']),'torn',len(result['torn']),'traceBytesWithPrefixes',sum(result['volumes'].values()))
    print('volumeByKind',result['volumes'].most_common(12))
    print('bridgeRouteChains',len(result['routeChains']),'withDecision',sum(c.get('decisionMatches',False) for c in result['routeChains']))
    for _,f in result['records']:
        if f['kind'] in ('session-end','session-delivered','stored-route','bridge-movement'):print(f)

if __name__=='__main__':main()
