import pathlib
import sys

from inspect_swf import ABC, Reader, swf


MN1 = {
    4: 'getsuper', 5: 'setsuper', 0x59: 'getdescendants', 0x5d: 'findpropstrict',
    0x5e: 'findproperty', 0x5f: 'finddef', 0x60: 'getlex', 0x66: 'getproperty',
    0x61: 'setproperty', 0x68: 'initproperty', 0x6a: 'deleteproperty',
    0x80: 'coerce', 0x86: 'astype', 0xb2: 'istype',
}
MN2 = {
    0x45: 'callsuper', 0x46: 'callproperty', 0x4a: 'constructprop',
    0x4c: 'callproplex', 0x4e: 'callsupervoid', 0x4f: 'callpropvoid',
}
VAR1 = {
    6: 'dxns', 8: 'kill', 0x25: 'pushshort', 0x2c: 'pushstring', 0x2d: 'pushint',
    0x2e: 'pushuint', 0x2f: 'pushdouble', 0x31: 'pushnamespace', 0x40: 'newfunction',
    0x41: 'call', 0x42: 'construct', 0x49: 'constructsuper', 0x53: 'applytype',
    0x55: 'newobject', 0x56: 'newarray', 0x58: 'newclass', 0x5a: 'newcatch',
    0x62: 'getlocal', 0x63: 'setlocal', 0x67: 'getouterscope', 0x6c: 'getslot',
    0x6d: 'setslot', 0x6e: 'getglobalslot', 0x6f: 'setglobalslot', 0x92: 'inclocal',
    0x94: 'declocal', 0xc2: 'inclocal_i', 0xc3: 'declocal_i', 0xf0: 'debugline',
    0xf1: 'debugfile', 0xf2: 'bkptline',
}
BRANCH = dict(enumerate(
    ('ifnlt', 'ifnle', 'ifngt', 'ifnge', 'jump', 'iftrue', 'iffalse', 'ifeq', 'ifne',
     'iflt', 'ifle', 'ifgt', 'ifge', 'ifstricteq', 'ifstrictne'),
    0x0c,
))
NO_ARGS = {
    2: 'nop', 3: 'throw', 9: 'label', 0x20: 'pushnull', 0x21: 'pushundefined',
    0x26: 'pushtrue', 0x27: 'pushfalse', 0x28: 'pushnan', 0x29: 'pop', 0x2a: 'dup',
    0x2b: 'swap', 0x30: 'pushscope', 0x47: 'returnvoid', 0x48: 'returnvalue',
    0x57: 'newactivation', 0x64: 'getglobalscope', 0x70: 'convert_s', 0x73: 'convert_i',
    0x75: 'convert_d', 0x76: 'convert_b', 0x82: 'coerce_a', 0x85: 'coerce_s',
    0x96: 'not', 0xa0: 'add', 0xab: 'equals', 0xac: 'strictequals', 0xc0: 'increment_i',
    0xc5: 'add_i', 0xc6: 'subtract_i',
    **{0xd0 + i: f'getlocal_{i}' for i in range(4)},
    **{0xd4 + i: f'setlocal_{i}' for i in range(4)},
}


def insns(abc, method):
    reader = Reader(abc.bodies[method])
    while reader.pos < len(reader.data):
        pos = reader.pos
        opcode = reader.u8()
        if opcode in MN1 or opcode in MN2:
            name = abc.name(reader.var()) or '[key]'
            op, args = (MN1[opcode], name) if opcode in MN1 else (MN2[opcode], f'{name} ({reader.var()})')
        elif opcode in VAR1:
            arg = reader.var()
            pool = {
                0x2c: abc.strings, 0x2d: abc.ints, 0x2e: abc.uints, 0x2f: abc.doubles,
                0xf1: abc.strings,
            }.get(opcode)
            op, args = VAR1[opcode], str(arg) if pool is None else repr(pool[arg])
        elif opcode in BRANCH:
            delta = int.from_bytes(reader.take(3), 'little', signed=True)
            op, args = BRANCH[opcode], str(reader.pos + delta)
        elif opcode == 0x1b:
            default = pos + int.from_bytes(reader.take(3), 'little', signed=True)
            targets = [pos + int.from_bytes(reader.take(3), 'little', signed=True) for _ in range(reader.var()+1)]
            op, args = 'lookupswitch', f'{default}, {targets}'
        elif opcode in (0x24, 0x65):
            op, args = ('pushbyte' if opcode == 0x24 else 'getscopeobject'), str(reader.u8())
        elif opcode in (0x32, 0x43, 0x44):
            op, args = f'op{opcode:02x}', f'{reader.var()} {reader.var()}'
        elif opcode == 0xef:
            reader.u8(); reader.var(); reader.u8(); reader.var()
            op, args = 'debug', ''
        else:
            op, args = NO_ARGS.get(opcode, f'op{opcode:02x}'), ''
        yield pos, reader.pos, opcode, op, args


def blocks(path):
    for tag, data in swf(path)[-1]:
        if tag == 82:
            reader = Reader(data)
            reader.num('<I')
            name = reader.cstr()
            yield name, data[:reader.pos], ABC(data[reader.pos:]).init()


def class_methods(abc, name):
    for index, (label, _, constructor, traits) in enumerate(abc.classes):
        if abc.name(label).split('::')[-1] != name:
            continue
        static, static_traits = abc.statics[index]
        yield 'constructor', constructor
        yield 'static_constructor', static
        for name, kind, target in [*traits, *static_traits]:
            if kind in (1, 2, 3):
                yield abc.name(name).split('::')[-1], target


if __name__ == '__main__':
    path = pathlib.Path(sys.argv[1])
    cls = sys.argv[2]
    for _, _, abc in blocks(path):
        for name, index in class_methods(abc, cls):
            if name not in sys.argv[3:]:
                continue
            print(f'[{cls}.{name} #{index}]')
            for start, _, _, op, args in insns(abc, index):
                if not op.startswith('debug'):
                    print(f'{start:4} {op:19} {args}')
