"""Bounded affine address reconstruction and evidence-qualified field associations."""
from __future__ import annotations
from collections import defaultdict


class Addresses:
    def __init__(self, function, max_terms=16, max_alternatives=8, max_steps=100000):
        self.function = function['functionRva']
        self.ops = {o['id']: o for o in function['flow'].get('operations', [])}
        self.max_terms, self.max_alternatives, self.max_steps = max_terms, max_alternatives, max_steps
        self.cache, self.steps = {}, 0

    def opaque(self, symbol, reason, size=8):
        return [{'constant': 0, 'terms': {symbol: 1}, 'width': size, 'unknowns': [reason]}]

    def resolve(self, value, visiting=frozenset()):
        size = value.get('size', 8)
        if value.get('constant'):
            return [{'constant': int(value['offset'], 16), 'terms': {}, 'width': size, 'unknowns': []}]
        definition = value.get('definition')
        if definition is None:
            if value.get('parameterOrdinal') is not None:
                symbol = 'parameter/' + str(value['parameterOrdinal'])
                return [{'constant': 0, 'terms': {symbol: 1}, 'width': size, 'unknowns': []}]
            return self.opaque('input/' + value.get('space', '?') + '/' + value.get('offset', '?'), 'undefined address input', size)
        if definition in self.cache:
            return self.cache[definition]
        self.steps += 1
        if definition in visiting or self.steps > self.max_steps or len(visiting) >= 128:
            return self.opaque('value/' + definition, 'cyclic address or evaluation budget', size)
        operation = self.ops.get(definition)
        if operation is None:
            return self.opaque('value/' + definition, 'address definition not exported', size)
        visiting = visiting | {definition}
        code, inputs = operation['opcode'], operation.get('inputs', [])
        arguments = [self.resolve(i, visiting) for i in inputs] if code in {
            'COPY', 'CAST', 'INT_ADD', 'INT_SUB', 'INT_MULT', 'INT_LEFT', 'PTRADD', 'PTRSUB', 'MULTIEQUAL', 'INT_SEXT', 'INT_ZEXT'} else []
        if code in {'COPY', 'CAST'} and len(arguments) == 1 and inputs[0].get('size', size) == size:
            result = arguments[0]
        elif code in {'INT_SEXT', 'INT_ZEXT'} and len(arguments) == 1:
            result = []
            for item in arguments[0]:
                if not item['terms']:
                    n = item['constant']; bits = inputs[0].get('size', size) * 8
                    if code == 'INT_SEXT' and n & (1 << (bits - 1)): n -= 1 << bits
                    result.append({'constant': n, 'terms': {}, 'width': size, 'unknowns': item['unknowns']})
                else:
                    # Widening preserves the named index, not its signed/unsigned range.
                    symbol = code.lower() + str(inputs[0].get('size', size)*8) + '(value/' + definition + ')'
                    result.append({'constant': 0, 'terms': {symbol: 1}, 'width': size,
                                   'unknowns': item['unknowns'], 'transform': {'opcode': code, 'input': inputs[0]}})
        elif code == 'MULTIEQUAL':
            result = [x for choices in arguments for x in choices]
        elif code in {'INT_ADD', 'INT_SUB', 'PTRSUB'} and len(arguments) == 2:
            factor = -1 if code == 'INT_SUB' else 1
            result = [self.add(a, b, factor, size) for a in arguments[0] for b in arguments[1]]
        elif code in {'INT_MULT', 'INT_LEFT'} and len(arguments) == 2:
            result = []
            for a in arguments[0]:
                for b in arguments[1]:
                    if not b['terms']:
                        scale = b['constant'] if code == 'INT_MULT' else 1 << b['constant'] if 0 <= b['constant'] < size*8 else None
                        result.extend([self.scale(a, scale, size)] if scale is not None else self.opaque('value/'+definition, 'invalid shift', size))
                    elif code == 'INT_MULT' and not a['terms']:
                        result.append(self.scale(b, a['constant'], size))
                    else:
                        result.extend(self.opaque('value/'+definition, 'non-affine multiplication/shift', size))
        elif code == 'PTRADD' and len(arguments) == 3:
            result = []
            for base in arguments[0]:
                for index in arguments[1]:
                    for stride in arguments[2]:
                        if not stride['terms']:
                            result.append(self.add(base, self.scale(index, stride['constant'], size), 1, size))
                        else:
                            result.extend(self.opaque('value/'+definition, 'nonconstant pointer stride', size))
        else:
            result = self.opaque('value/'+definition, 'unsupported address operation '+code, size)
        if len(result) > self.max_alternatives or any(len(x['terms']) > self.max_terms for x in result):
            result = self.opaque('value/'+definition, 'address alternative/term budget', size)
        # P-code integer arithmetic is modular. Keep the modulus explicit.
        for item in result:
            item['constant'] %= 1 << (size*8); item['width'] = size
        self.cache[definition] = result
        return result

    @staticmethod
    def add(a, b, factor, size):
        terms = defaultdict(int, a['terms'])
        for key, value in b['terms'].items(): terms[key] += factor * value
        return {'constant': a['constant'] + factor*b['constant'], 'terms': {k:v for k,v in terms.items() if v},
                'width': size, 'unknowns': sorted(set(a['unknowns'] + b['unknowns']))}

    @staticmethod
    def scale(a, factor, size):
        return {'constant': a['constant']*factor, 'terms': {k:v*factor for k,v in a['terms'].items() if v*factor},
                'width': size, 'unknowns': list(a['unknowns'])}


def model_associations(function, expression, access_size, models):
    matches = []
    for model in models:
        if model['binaryHash'] != function['binaryHash']: continue
        root = model['root']; terms = dict(expression['terms']); offset = expression['constant']
        if root['kind'] == 'parameter':
            if function['functionRva'] not in root['functions']: continue
            symbol = 'parameter/' + str(root['ordinal'])
            if terms.pop(symbol, 0) != 1: continue
        elif root['kind'] == 'absolute-inline':
            offset -= int(root['rva'], 16) + int(model.get('imageBase', '0x180000000'), 16)
            # Address constants in High P-code are VAs. Do not guess RVA/VA interpretation.
        else:
            continue
        stride = model.get('stride')
        if stride is not None:
            if len(terms) != 1 or next(iter(terms.values())) != stride: continue
        elif terms:
            continue
        for field in model.get('fields', []):
            field_size = field.get('size')
            matched = field['offset'] == offset if field_size is None else field['offset'] <= offset and offset + access_size <= field['offset'] + field_size
            if access_size > 0 and matched:
                matches.append({'model': model['id'], 'field': field['name'], 'fieldOffset': field['offset'],
                                'withinField': offset - field['offset'], 'indexTerms': terms,
                                'fieldExtent': 'unverified' if field_size is None else 'audit-established',
                                'authority': 'address reconstruction linked to separately audited model',
                                'evidence': model['evidence'], 'unknowns': list(expression['unknowns'])})
    return matches


def typed_associations(address, size, layouts):
    """Only explicit pointer types can seed type-relative associations."""
    path = address.get('pointerTargetType')
    if not path or path not in layouts: return []
    result = []
    for field in layouts[path].get('fields', []):
        if field['offset'] == 0 and size <= field['size']:
            result.append({'typePath': path, 'field': field['name'], 'fieldOffset': 0,
                           'authority': 'decompiler pointer type + database layout; semantic candidate',
                           'bitField': field.get('bitField', False)})
    return result


def relative_type_associations(function,expression,size,layouts):
    matches=[]
    for parameter in function['flow'].get('parameters',[]):
        path=parameter.get('pointerTargetType')
        root='parameter/'+str(parameter['ordinal'])
        terms=dict(expression['terms'])
        if path is None or terms.pop(root,0)!=1:continue
        index_terms=dict(terms);offset=expression['constant']
        def fields(type_path,displacement,prefix,seen=()):
            if type_path in seen or len(seen)>=8 or type_path not in layouts:return
            layout=layouts[type_path];seen=seen+(type_path,)
            if layout.get('target') and 'TypeDef' in layout.get('kind',''):
                fields(layout['target'],displacement,prefix,seen);return
            for field in layout.get('fields',[]):
                start=field['offset'];end=start+field['size']
                if not start<=displacement or displacement+size>end:continue
                names=prefix+[field['name'] or '#'+str(field['ordinal'])]
                child=layouts.get(field['typePath'],{});relative=displacement-start
                child_array=child.get('array')
                if terms:
                    if child_array is None or len(terms)!=1 or next(iter(terms.values()))!=child_array['elementSize']:continue
                    if relative+size>child_array['elementSize']:continue
                matches.append({'rootParameter':parameter['ordinal'],'typePath':type_path,'fieldPath':names,
                    'fieldOffset':start,'withinField':relative,'indexTerms':index_terms,'bitField':field.get('bitField',False),
                    'layoutTruncated':layout.get('truncated',False),'authority':'decompiler pointer root + database layout; semantic candidate',
                    'unknowns':expression['unknowns']})
                if not terms:fields(field['typePath'],relative,names,seen)
        fields(path,offset,[])
    return matches


def accesses(function, models=(), layouts=None):
    engine = Addresses(function); layouts = layouts or {}
    results = []
    for operation in function['flow'].get('operations', []):
        if operation['opcode'] not in {'LOAD', 'STORE'}: continue
        inputs = operation.get('inputs', [])
        if len(inputs) < 2: continue
        width = (operation.get('output') or {}).get('size', 0) if operation['opcode'] == 'LOAD' else inputs[2].get('size', 0) if len(inputs)>2 else 0
        address = inputs[1]
        expressions = engine.resolve(address)
        candidates = []
        type_candidates=typed_associations(address,width,layouts)
        for expression in expressions:
            candidates.extend(model_associations(function, expression, width, models))
            type_candidates.extend(relative_type_associations(function,expression,width,layouts))
        results.append({'function': function['functionRva'], 'operation': operation['id'], 'site': operation['site'],
                        'access': 'read' if operation['opcode']=='LOAD' else 'write', 'size': width,
                        'memorySpace': inputs[0], 'addressValue': address, 'expressions': expressions,
                        'modelAssociations': candidates, 'typeAssociations': type_candidates,
                        'value': operation.get('output') if operation['opcode']=='LOAD' else inputs[2] if len(inputs)>2 else None,
                        'authority': 'ghidra-high-pcode address reconstruction',
                        'decompilerDiagnostics':function['flow'].get('diagnostics',[]),
                        'aliasing': 'not established; same field may reference different records or executions'})
    return {'status': 'complete-static-observations' if function['flow']['status']=='complete' else 'unavailable',
            'function': function['functionRva'], 'accesses': results,
            'evaluationSteps': engine.steps,
            'limitations': ['Only exported LOAD/STORE address expressions, not every possible machine access.',
                           'SSA alternatives are possibilities; constraints and aliasing are not solved.',
                           'Unknown arithmetic and recursive definitions retain opaque terms.']}
