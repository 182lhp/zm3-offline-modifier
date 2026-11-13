"""Stage a narrow launcher patch. Does not write to the input game directory."""
import argparse
import hashlib
import json
from pathlib import Path
import zlib

from avm2_listing import blocks, insns

BASELINE = 'fe83129401be9e5c2f1bbf5f0156930dcddadc79d49b2a6418f37e8e9e67adc0'
SITES = {181: (52, 56), 250: (251, 255)}


def patch_launcher(path):
    original = path.read_bytes()
    assert hashlib.sha256(original).hexdigest() == BASELINE, 'Unexpected launcher baseline'
    assert original[:3] == b'CWS'
    _, _, before = next(blocks(path))
    calls = [(method, row[:2]) for method in before.bodies for row in insns(before, method)
             if row[3] in ('callproperty', 'callpropvoid') and 'ensureSpecialPets' in row[4]]
    assert calls == list(SITES.items()), calls
    data = bytearray(before.data)
    for method, (start, end) in SITES.items():
        instruction = next(row for row in insns(before, method) if row[0] == start)
        assert instruction[3:] == ('callpropvoid', 'ensureSpecialPets (1)') and end - start == 4
        base, _ = before.body_ranges[method]
        # callpropvoid consumed the function receiver and one argument. Consume
        # exactly those operands, retain offsets and exception regions, and skip
        # the optional four-beast repair/gift operation.
        data[base + start:base + end] = b'\x29\x29\x02\x02'
    body = zlib.decompress(original[8:])
    assert body.count(before.data) == 1
    return original[:8] + zlib.compress(body.replace(before.data, data, 1), 9)


def stage(root, out):
    out.mkdir(parents=True, exist_ok=False)
    relative = Path('game/launcher.swf')
    original = (root / relative).read_bytes()
    updated = patch_launcher(root / relative)
    (out / relative).parent.mkdir()
    (out / relative).write_bytes(updated)
    _, _, before = next(blocks(root / relative))
    _, _, after = next(blocks(out / relative))
    assert before.methods == after.methods and before.classes == after.classes and before.strings == after.strings
    assert [m for m in before.bodies if before.bodies[m] != after.bodies[m]] == list(SITES)
    for method, (start, end) in SITES.items():
        assert after.bodies[method][start:end] == b'\x29\x29\x02\x02'
        assert before.bodies[method][:start] == after.bodies[method][:start]
        assert before.bodies[method][end:] == after.bodies[method][end:]
    files = []
    def record(name, old, new):
        files.append({'path': name, 'before': hashlib.sha256(old).hexdigest(), 'after': hashlib.sha256(new).hexdigest()})
    record(relative.as_posix(), original, updated)
    for algorithm in ('sha256', 'sha512'):
        name = 'manifest.' + algorithm
        old = (root / name).read_bytes()
        marker = hashlib.new(algorithm, original).hexdigest().upper().encode() + b'  game/launcher.swf'
        assert old.count(marker) == 1
        new = old.replace(marker, hashlib.new(algorithm, updated).hexdigest().upper().encode() + b'  game/launcher.swf')
        (out / name).write_bytes(new)
        record(name, old, new)
    (out / 'patch.json').write_text(json.dumps({'files': files, 'methods': SITES,
        'effect': 'Disable automatic four-beast gifts on load and save; preserve all existing pets and manual GM grants.'}, indent=2), encoding='utf-8')
    print('Verified two automatic gift call sites; other method bodies, metadata and templates are unchanged.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('root', type=Path)
    parser.add_argument('out', type=Path)
    args = parser.parse_args()
    stage(args.root.resolve(), args.out.resolve())
