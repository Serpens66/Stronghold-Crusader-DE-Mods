from pathlib import Path
import re
root=Path.cwd()
skip={'_inspect','shcde-script-extender','obj','bin','.git','APIShared'}
types={'MapStartEventArgs':'start','MapLoadEventArgs':'load','LoadSaveGameEventArgs':'save','MapUnloadEventArgs':'end'}
def write(p,s): p.write_bytes(s.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8'))
def end_group(s,pos,left='(',right=')'):
    depth=0; quote=None; i=pos
    while i<len(s):
        c=s[i]
        if quote:
            if c=='\\': i+=2; continue
            if c==quote: quote=None
        elif c in ['"',"'"]: quote=c
        elif c==left: depth+=1
        elif c==right:
            depth-=1
            if depth==0:return i+1
        i+=1
    raise ValueError(s[pos:pos+100])
def convert(s,kind):
    # Only called inside an identified lifecycle handler/subscription, never other native callbacks.
    for var in ['args','a','e','x']:
        s=s.replace(var+'.bMultiplayerSave != 0',var+'.Context.IsSave && '+var+'.Context.Mode.IsRealMultiplayer')
        s=s.replace(var+'.bMultiplayerSave == 0','!'+var+'.Context.IsSave')
        s=s.replace(var+'.LoadingEditorMap', '('+var+'.Context.StartKind == APIShared.MissionStartKind.EditorLoaded)')
        for old,new in [('FileName','FilePath'),('MapName','MapName')]: s=s.replace(var+'.'+old,var+'.Context.'+new)
        s=s.replace(var+'.CampaignMapId',var+'.Context.Mode.CampaignMapId').replace(var+'.CampaignMapID',var+'.Context.Mode.CampaignMapId')
        s=s.replace(var+'.bMultiplayerSave', '('+var+'.Context.IsSave ? (byte)1 : (byte)0)')
        s=s.replace(var+'.ReturnValue > 0','!'+var+'.IsBeforeInitialization')
        s=s.replace(var+'.ReturnValue == 0',var+'.IsBeforeInitialization')
        for eq,phase in [('==','Pre'),('!=','Post')]:
            s=s.replace(var+'.Phase '+eq+' EventHookPhase.'+phase, 'true' if kind=='end' else var+'.IsBeforeInitialization')
        for eq,phase in [('==','Post'),('!=','Pre')]:
            s=s.replace(var+'.Phase '+eq+' EventHookPhase.'+phase, ('true' if eq=='==' else 'false') if kind=='end' else '!'+var+'.IsBeforeInitialization')
        s=s.replace('Shared.GameModeHelper.Capture('+var+')',var+'.Context.Mode')
    return s

for p in root.rglob('*.cs'):
    if any(x.startswith('.') or x.startswith('_') for x in p.relative_to(root).parts) or skip.intersection(p.relative_to(root).parts) or any(x.lower()=='tests' or x.endswith('.Tests') for x in p.parts):continue
    s=p.read_text(encoding='utf-8-sig'); old=s
    # Convert method parameters and just their bodies.
    matches=list(re.finditer(r'\b('+ '|'.join(types)+r')\s+(\w+)',s))
    for m in reversed(matches):
        tail=m.end(); brace=s.find('{',tail); semi=s.find(';',tail)
        if brace>=0 and (semi<0 or brace<semi): end=end_group(s,brace,'{','}')
        else:end=semi+1
        body=s[m.start():end]
        body=convert(body,types[m.group(1)]).replace(m.group(1),'APIShared.MissionLifecycleNotification')
        s=s[:m.start()]+body+s[end:]
    # Convert each subscription chain independently, including its inline callback.
    pat=r'MapLoaderR3EventHooks\.(OnStartMap|OnLoadMap|OnLoadSave|OnUnloadMap)\.Observable'
    for m in reversed(list(re.finditer(pat,s))):
        subscribe=s.find('.Subscribe(',m.end())
        if subscribe<0:raise ValueError(str(p))
        end=end_group(s,subscribe+len('.Subscribe'))
        chunk=s[m.start():end]
        kind={'OnStartMap':'start','OnLoadMap':'load','OnLoadSave':'save','OnUnloadMap':'end'}[m.group(1)]
        stream={'start':'NativeStart','load':'Loading','save':'SaveLoading','end':'Ended'}[kind]
        chunk=convert(chunk,kind)
        chunk=re.sub(pat,'Shared.MissionEvents.'+stream,chunk,count=1)
        if kind=='end': chunk=re.sub(r'\s*\.Where\(\w+ => true\)','',chunk)
        s=s[:m.start()]+chunk+s[end:]
    # Session consumers receive the actual common notification rather than fabricated native args.
    s=s.replace('context.MapStart','context.Notification').replace('context.SaveLoad','context.Notification').replace('context.EditorSessionId','context.SessionId')
    s=s.replace('context.Notification.bMultiplayerSave != 0','context.Notification.Context.IsSave && context.Mode.IsRealMultiplayer')
    if s!=old:write(p,s)

# Native-only diagnostic pointers have no place in a public mission context.
p=root/'CastlePlanner/src/CastlePlannerRuntime.cs'; s=p.read_text(encoding='utf-8')
s=re.sub(r'^.*Unknown[13] = args\.Unknown[13],\n','',s,flags=re.M)
s=re.sub(r'^.*\$"unknown1=0x.*\n','',s,flags=re.M)
s=re.sub(r'^.*public (IntPtr|ulong) Unknown[13] \{ get; set; \}\n','',s,flags=re.M)
write(p,s)
