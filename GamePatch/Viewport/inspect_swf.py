import hashlib
import lzma
import pathlib
import re
import struct
import sys
import zlib
from collections import Counter


class Reader:
    def __init__(self, data):
        self.data = data
        self.pos = 0

    def take(self, count):
        start = self.pos
        self.pos += count
        assert self.pos <= len(self.data)
        return self.data[start:self.pos]

    def num(self, fmt):
        return struct.unpack(fmt, self.take(struct.calcsize(fmt)))[0]

    def u8(self):
        return self.num('B')

    def var(self):
        value = 0
        for shift in range(0, 35, 7):
            byte = self.u8()
            value |= (byte & 0x7f) << shift
            if byte < 128 or shift == 28:
                return value

    def cstr(self):
        end = self.data.index(0, self.pos)
        return self.take(end + 1 - self.pos)[:-1].decode('utf-8', 'replace')


def swf(path):
    data = path.read_bytes()
    sig, version, expected = struct.unpack_from('<3sBI', data)
    if sig == b'FWS':
        body = data[8:]
    elif sig == b'CWS':
        body = zlib.decompress(data[8:])
    elif sig == b'ZWS':
        body = lzma.decompress(data[12:17] + struct.pack('<Q', expected - 8) + data[17:])
    else:
        raise ValueError(sig)
    assert len(body) == expected - 8
    nbits = body[0] >> 3
    end = (5 + nbits * 4 + 7) // 8
    rect = int.from_bytes(body[:end], 'big')
    coords = []
    for i in range(4):
        x = rect >> (end * 8 - 5 - nbits * (i + 1)) & ((1 << nbits) - 1)
        coords.append(x - (1 << nbits) if x >> (nbits - 1) else x)
    fps, frames = struct.unpack_from('<HH', body, end)
    r = Reader(body)
    r.pos = end + 4
    tags = []
    while r.pos < len(body):
        header = r.num('<H')
        code, size = header >> 6, header & 63
        if size == 63:
            size = r.num('<I')
        tags.append((code, r.take(size)))
    return sig.decode(), version, tuple(x / 20 for x in coords), fps / 256, frames, tags


class ABC(Reader):
    def pool(self, read, empty):
        return [empty] + [read() for _ in range(self.var() - 1)]

    def init(self):
        self.version = (self.num('<H'), self.num('<H'))
        self.ints = self.pool(self.var, 0)
        self.uints = self.pool(self.var, 0)
        self.doubles = self.pool(lambda: self.num('<d'), 0)
        self.strings = self.pool(lambda: self.take(self.var()).decode('utf-8', 'replace'), '')
        self.namespaces = self.pool(lambda: (self.u8(), self.var()), (0, 0))
        self.sets = self.pool(lambda: [self.var() for _ in range(self.var())], [])
        self.multinames = self.pool(self.multiname, (0,))
        self.methods = []
        for _ in range(self.var()):
            count, ret = self.var(), self.var()
            args = [self.var() for _ in range(count)]
            name, flags = self.var(), self.u8()
            if flags & 8:
                for _ in range(self.var()):
                    self.var()
                    self.u8()
            if flags & 0x80:
                for _ in range(count):
                    self.var()
            self.methods.append((ret, args, name, flags))
        for _ in range(self.var()):
            self.var()
            for _ in range(self.var() * 2):
                self.var()
        count = self.var()
        self.classes = []
        for _ in range(count):
            name, parent, flags = self.var(), self.var(), self.u8()
            if flags & 8:
                self.var()
            for _ in range(self.var()):
                self.var()
            constructor = self.var()
            self.classes.append((name, parent, constructor, self.traits()))
        self.statics = [(self.var(), self.traits()) for _ in range(count)]
        self.scripts = [(self.var(), self.traits()) for _ in range(self.var())]
        self.bodies = {}
        self.body_ranges = {}
        for _ in range(self.var()):
            method = self.var()
            for _ in range(4):
                self.var()
            length = self.var()
            self.body_ranges[method] = (self.pos, self.pos + length)
            self.bodies[method] = self.take(length)
            for _ in range(self.var() * 5):
                self.var()
            self.traits()
        assert self.pos == len(self.data), (self.pos, len(self.data))
        return self

    def multiname(self):
        kind = self.u8()
        if kind == 0x1d:
            base = self.var()
            return kind, base, *[self.var() for _ in range(self.var())]
        count = {
            0x07: 2, 0x0d: 2, 0x0f: 1, 0x10: 1, 0x11: 0, 0x12: 0,
            0x09: 2, 0x0e: 2, 0x1b: 1, 0x1c: 1,
        }[kind]
        return kind, *[self.var() for _ in range(count)]

    def name(self, index):
        kind, *args = self.multinames[index]
        if kind in (7, 13):
            namespace = self.strings[self.namespaces[args[0]][1]]
            local = self.strings[args[1]]
            return f'{namespace}::{local}' if namespace else local
        if kind in (9, 14, 15, 16):
            return self.strings[args[0]]
        if kind == 29:
            return self.name(args[0])
        return ''

    def traits(self):
        traits = []
        for _ in range(self.var()):
            name, flags = self.var(), self.u8()
            kind = flags & 15
            self.var()
            target = self.var()
            if kind in (0, 6) and self.var():
                self.u8()
            if flags & 0x40:
                for _ in range(self.var()):
                    self.var()
            traits.append((name, kind, target))
        return traits


def inspect(path):
    if path.read_bytes()[:3] not in (b'FWS', b'CWS', b'ZWS'):
        print(path.name, 'not a directly readable SWF', path.read_bytes()[:12].hex())
        return []
    fmt, version, rect, fps, frames, tags = swf(path)
    types = Counter(kind for kind, _ in tags)
    abcs = []
    all_urls = []
    docs = {'GMain', 'MainGame', 'PetInterface', 'BackPack', 'BaseMonster', 'LevelInfo', 'StageInfo', 'DropEquip', 'MainResource', 'Role1', 'LevelConfig'}
    print(path.name, fmt, version, rect, 'fps', fps, 'frames', frames, 'tags', types.most_common(8))
    for kind, value in tags:
        if kind == 76:
            r = Reader(value)
            symbols = [(r.num('<H'), r.cstr()) for _ in range(r.num('<H'))]
            print('  symbols:', len(symbols), [x for x in symbols if x[0] == 0])
        if kind != 82:
            continue
        r = Reader(value)
        r.num('<I')
        abc_name = r.cstr()
        abc = ABC(value[r.pos:]).init()
        abcs.append(abc)
        for index, (name, _, _, traits) in enumerate(abc.classes):
            cls = abc.name(name)
            simple = cls.split('::')[-1]
            match = simple in docs or re.fullmatch(r'Main|Launcher|LocalSave|Config|User|BaseLevelListenering|SavedBuff|Drop\w*|PetInfo|Strength|SutraInterface|MyEquipObj|PetEquipObj|SelectPLace|GameVip|SuperLoader|SingleLoader', simple, re.I)
            if not match and path.name != 'launcher.swf':
                continue
            methods = [(abc.name(n).split('::')[-1], t) for n, k, t in [*traits, *abc.statics[index][1]] if k in (1, 2, 3)]
            if path.name == 'launcher.swf':
                if re.search('^(?:haxe|flash|sys|_std|cpp)', cls):
                    continue
                keys = [m for m, _ in methods if re.search('save|load|pet|union|vip|diag|hook|inject|cfg|name|sync|start|force', m, re.I)]
            else:
                keys = [m for m, _ in methods if re.search('save|drop|topLevel|webUid|Limit|Abnormal|Equipment|nickName|^start$|^addBuff$|^setFromMemory$', m, re.I)]
            print('   ', cls, keys[:50])
            for method, target in methods:
                if method == 'getTotalTopLevel':
                    print('      ', method, abc.bodies.get(target,b'').hex())
        all_urls.extend((abc_name, x) for x in abc.strings if x.startswith(('http://', 'https://')) and 'AS3/2006/builtin' not in x)
    print('  ABC blocks:', len(abcs), 'classes:', sum(len(abc.classes) for abc in abcs), 'methods:', sum(len(abc.methods) for abc in abcs))
    off_host = [(name, url) for name, url in all_urls if not re.match(r'https?://(?:127\.[\d.]+|localhost)(?:[:/]|$)', url)]
    print('  HTTP literals:', len(all_urls), 'loopback:', len(all_urls) - len(off_host), 'external examples:', off_host[:8])
    if path.name == 'launcher.swf':
        print('  endpoints and filenames:', [x for abc in abcs for x in abc.strings if x.endswith('.swf') or re.match(r'^/(?:save|vip|union|cfg|diag|cmd)',x)])
    return abcs


if __name__ == '__main__':
    for name in sys.argv[1:]:
        inspect(pathlib.Path(name))
