"""Context-sensitive static value slices; no execution or inferred memory aliasing."""
from __future__ import annotations
from collections import defaultdict, deque
import hashlib


def binding_supported(connection, callee, call):
    if callee.get('variadic'):return False
    claim = connection.execute(
        "SELECT abi_confidence FROM function_claims WHERE binary_hash=? AND function_rva=? AND abi_confidence='confirmed'",
        (callee['binaryHash'], callee['functionRva'])).fetchone()
    parameters = callee['flow'].get('parameters', [])
    contract=False
    if connection.execute("SELECT 1 FROM sqlite_master WHERE type='table' AND name='analysis_contracts'").fetchone():
        import json
        row=connection.execute('SELECT payload FROM analysis_contracts WHERE binary_hash=? AND rva=?',
                               (callee['binaryHash'],callee['functionRva'])).fetchone()
        if row:contract=any(c['validation']=='matched' for c in json.loads(row[0])['documentedContracts'])
    return (bool(claim) or contract or callee.get('signatureSource') in {'USER_DEFINED', 'IMPORTED'}) and \
        len(parameters) == len(call.get('inputs', [])) - 1 and \
        sorted(p['ordinal'] for p in parameters) == list(range(len(parameters)))


class ValueGraph:
    def __init__(self, connection, load, max_functions=128, max_nodes=10000, max_edges=20000, include_control=True):
        if max_functions < 1 or max_nodes < 1 or max_edges < 1:
            raise ValueError('Positive graph budgets required')
        self.connection, self.load = connection, load
        self.max_functions, self.max_nodes = max_functions, max_nodes
        self.max_edges,self.include_control,self.omitted_edges=max_edges,include_control,0
        self.nodes, self.edges, self.gaps, self.instances = {}, [], [], []
        self.frontier, self.cache = [], {}

    def function(self, value):
        if value not in self.cache:
            self.cache[value] = self.load(self.connection, value)
        return self.cache[value]

    def add(self, source, target, kind, **details):
        if source is not None:
            if len(self.edges)>=self.max_edges:
                self.omitted_edges+=1
                if self.omitted_edges==1:self.frontier.append({'reason':'edge budget','from':source,'to':target})
                return
            self.edges.append({'from': source, 'to': target, 'kind': kind, **details})

    @staticmethod
    def source(value, prefix):
        if value.get('definition'):
            return prefix + value['definition']
        if value.get('parameterOrdinal') is not None:
            return prefix + 'parameter/' + str(value['parameterOrdinal'])
        return None

    def expand(self, value, context='root', ancestors=()):
        f = self.function(value)
        rva = f['functionRva']
        if rva in ancestors:
            self.frontier.append({'function': rva, 'context': context, 'reason': 'recursive call'})
            return None
        ops, parameters = f['flow'].get('operations', []), f['flow'].get('parameters', [])
        calls = sum(o['opcode'] in {'CALL', 'CALLIND'} for o in ops)
        if len(self.instances) >= self.max_functions or len(self.nodes) + len(ops) + len(parameters) + calls > self.max_nodes:
            self.frontier.append({'function': rva, 'context': context, 'reason': 'graph budget'})
            return None
        if f['flow']['status'] != 'complete':
            self.gaps.append({'function': rva, 'context': context, 'reason': f['flow']['status']})
            return None
        prefix = rva + '@' + context + '/'
        self.instances.append({'function': rva, 'context': context, 'prefix': prefix})
        if f['flow'].get('diagnostics'):
            self.gaps.append({'function':rva,'context':context,'reason':'decompiler diagnostics','messages':f['flow']['diagnostics']})
        for param in parameters:
            key = prefix + 'parameter/' + str(param['ordinal'])
            self.nodes[key] = {'id': key, 'function': rva, 'context': context, 'parameter': param}
        for op in ops:
            key = prefix + op['id']
            self.nodes[key] = {'id': key, 'function': rva, 'context': context, 'operation': op}
            if op['opcode'] in {'CALL', 'CALLIND'}:
                invoke = key + '/invoke'
                self.nodes[invoke] = {'id': invoke, 'function': rva, 'context': context,
                                      'operation': op, 'role': 'call-input-observation'}
        for op in ops:
            key = prefix + op['id']
            if op['opcode'] not in {'CALL', 'CALLIND'}:
                for i, val in enumerate(op.get('inputs', [])):
                    self.add(self.source(val, prefix), key, 'value-use', input=i)
            if op['opcode'] in {'LOAD', 'STORE'}:
                self.gaps.append({'node': key, 'reason': 'memory reaching definitions/aliasing not established'})
            if op['opcode'] in {'CALLIND', 'BRANCHIND'}:
                self.gaps.append({'node': key, 'reason': 'indirect control-flow not exhaustive'})
            if op['opcode'] not in {'CALL', 'CALLIND'}:
                continue
            invoke = key + '/invoke'
            for i, val in enumerate(op.get('inputs', [])[1:], 1):
                self.add(self.source(val, prefix), invoke, 'call-input', input=i)
            self.gaps.append({'node': invoke, 'reason': 'call memory side effects not modeled'})
            target = op.get('callTarget')
            callee = None
            if target:
                try:
                    callee = self.function(target)
                except ValueError:
                    pass
            if callee is None or not binding_supported(self.connection, callee, op):
                # Arguments reaching the call are facts; dependence of its result is unknown.
                for i, val in enumerate(op.get('inputs', [])[1:], 1):
                    self.add(self.source(val, prefix), key, 'call-input-unknown-result', input=i,
                             authority='conservative candidate, no established result binding')
                self.gaps.append({'node': key, 'reason': 'unverified call binding/side effects'})
                continue
            child_context = hashlib.sha256((prefix + op['id']).encode()).hexdigest()[:16]
            child = self.expand(target, child_context, ancestors + (rva,))
            if child is None:
                self.gaps.append({'node': key, 'reason': 'callee analysis boundary'})
                continue
            for param, arg in zip(callee['flow'].get('parameters', []), op['inputs'][1:]):
                source = self.source(arg, prefix)
                destination = child + 'parameter/' + str(param['ordinal'])
                self.add(source, destination, 'call-argument', call=key, ordinal=param['ordinal'])
                if arg.get('constant'):
                    self.nodes[destination]['boundConstant'] = dict(arg)
                elif source is None:
                    self.gaps.append({'node': destination, 'reason': 'argument has no recovered definition'})
            if op.get('output'):
                returns = [r for r in callee['flow'].get('operations', []) if r['opcode'] == 'RETURN']
                for ret in returns:
                    for val in ret.get('inputs', [])[1:]:
                        self.add(self.source(val, child), key, 'return-value', call=key)
                        if val.get('constant'):
                            self.nodes[key].setdefault('possibleReturnConstants', []).append(dict(val))
                if not returns:
                    self.gaps.append({'node': key, 'reason': 'callee return unavailable'})
            self.gaps.append({'node': key, 'reason': 'call memory side effects not modeled'})
        if self.include_control:self.control(f, prefix)
        return prefix

    def control(self, f, prefix):
        """Control dependence from a terminating CFG. Infinite/unresolved regions stay unknown."""
        blocks = f['flow'].get('blocks')
        if blocks is None:
            self.gaps.append({'function': f['functionRva'], 'context': prefix, 'reason': 'high-pcode CFG unavailable'})
            return
        by = {b['id']: b for b in blocks}
        successors = {i: set(b['successors']) for i, b in by.items()}
        exit_id = '__exit__'
        reverse = defaultdict(set)
        for i, targets in successors.items():
            if not targets:
                targets.add(exit_id)
            for t in targets:
                reverse[t].add(i)
        terminating, queue = {exit_id}, deque([exit_id])
        while queue:
            for parent in reverse[queue.popleft()]:
                if parent not in terminating:
                    terminating.add(parent); queue.append(parent)
        if any(i not in terminating or any(t not in by and t != exit_id for t in ts)
               for i, ts in successors.items()):
            self.gaps.append({'function': f['functionRva'], 'context': prefix, 'reason': 'nonterminating or unresolved CFG; control dependence withheld'})
            return
        post = {i: set(terminating) for i in by}; post[exit_id] = {exit_id}
        changed = True
        while changed:
            changed = False
            for i, ts in successors.items():
                new = {i} | set.intersection(*(post[t] for t in ts))
                if new != post[i]: post[i] = new; changed = True
        operations = f['flow'].get('operations', [])
        by_block = defaultdict(list)
        for op in operations:
            if op.get('blockMembership', op.get('block') is not None):
                by_block[op.get('block')].append(op)
        for i, targets in successors.items():
            branch = next((o for o in reversed(by_block[i]) if o['opcode'] == 'CBRANCH'), None)
            if branch is None or len(targets) < 2:
                continue
            for t in targets:
                # Standard edge-control relation: postdom(t) minus postdom(branch block).
                for dependent in post[t] - post[i]:
                    for op in by_block[dependent]:
                        self.add(prefix + branch['id'], prefix + op['id'], 'control-dependence', successor=t)


def slice_values(connection, load, function, *, direction, parameter=None, operation=None,
                 instruction=None, returns=False, caller=None, call_site=None,
                 include_control=True, max_functions=128, max_nodes=10000, max_slice=5000, max_edges=20000):
    if direction not in {'forward', 'backward'} or max_slice < 1:
        raise ValueError('Valid direction and positive slice budget required')
    if sum([parameter is not None, operation is not None, instruction is not None, returns]) != 1:
        raise ValueError('Choose exactly one parameter, operation, instruction or return seed')
    graph = ValueGraph(connection, load, max_functions, max_nodes,max_edges,include_control)
    target = graph.function(function)['functionRva']
    graph.expand(caller or function)
    instances = [i for i in graph.instances if i['function'] == target]
    if caller:
        if call_site is None:
            raise ValueError('Caller context requires an explicit call-site RVA')
        calls = {n['id'] for n in graph.nodes.values() if n.get('operation', {}).get('site') == call_site
                 and n.get('operation', {}).get('callTarget') == target
                 and n['function'] == graph.function(caller)['functionRva'] and n['context'] == 'root'}
        prefixes = {e['to'].split('parameter/')[0] for e in graph.edges if e.get('call') in calls and e['kind'] == 'call-argument'}
        # Constant arguments have no value-use edge, so use the deterministic context as well.
        contexts = {hashlib.sha256(c.encode()).hexdigest()[:16] for c in calls}
        instances = [i for i in instances if i['prefix'] in prefixes or i['context'] in contexts]
    if len(instances) != 1:
        raise ValueError('Seed function has no unique recovered call context; select a supported caller/call-site or use the function as root')
    prefix = instances[0]['prefix']
    seeds = []
    for key, node in graph.nodes.items():
        if not key.startswith(prefix): continue
        op = node.get('operation', {})
        if parameter is not None and node.get('parameter', {}).get('ordinal') == parameter or \
           operation is not None and op.get('id') == operation and node.get('role')!='call-input-observation' or \
           instruction is not None and op.get('site') == instruction or returns and op.get('opcode') == 'RETURN':
            seeds.append(key)
    if not seeds: raise ValueError('Selected seed is not present in the recovered flow')
    adjacency = defaultdict(set)
    for e in graph.edges:
        if include_control or e['kind'] != 'control-dependence':
            source, destination = (e['from'], e['to']) if direction == 'forward' else (e['to'], e['from'])
            adjacency[source].add(destination)
    selected, queue, frontier = set(), deque(sorted(seeds)), []
    while queue:
        current = queue.popleft()
        if current in selected: continue
        if len(selected) >= max_slice:
            frontier = sorted(set([current, *queue]) - selected); break
        selected.add(current); queue.extend(sorted(adjacency[current] - selected))
    gaps = [g for g in graph.gaps if g.get('node') in selected or 'node' not in g]
    absent = sorted(selected - graph.nodes.keys())
    if absent: gaps.append({'reason': 'value definitions outside exported operations', 'nodes': absent})
    return {'status': 'partial' if gaps or frontier or graph.frontier else 'static-slice-complete',
            'function': target, 'context': instances[0]['context'], 'direction': direction, 'seedIds': sorted(seeds),
            'nodes': [graph.nodes[k] for k in sorted(selected) if k in graph.nodes],
            'edges': [e for e in graph.edges if e['from'] in selected and e['to'] in selected
                      and (include_control or e['kind'] != 'control-dependence')],
            'frontier': frontier, 'graphFrontier': graph.frontier, 'unknowns': gaps,
            'contexts': graph.instances, 'omittedGraphEdges':graph.omitted_edges,
            'budgets': {'functions': max_functions, 'graphNodes': max_nodes, 'sliceNodes': max_slice,'graphEdges':max_edges},
            'limitations': ['Static possible dependence, not execution or path feasibility.',
                           'Memory contents and call side effects require separate evidence.',
                           'Caller origins require an explicitly selected caller and call-site.',
                           'Each call context is separate; recursive expansion remains a boundary.']}
