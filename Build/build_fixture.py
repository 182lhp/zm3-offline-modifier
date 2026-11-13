"""Reconstruct the test-only movie by exact tag substitution against a pinned game."""
import json,zlib,struct,hashlib
def build(base,delta,output):
    raw=base.read_bytes();spec=json.loads(zlib.decompress(delta.read_bytes()))
    if hashlib.sha256(raw).hexdigest()!=spec['baseSha256']:raise ValueError('Test fixture base does not match the pinned game')
    body=zlib.decompress(raw[8:]) if raw[:3]==b'CWS' else raw[8:];at=(5+(body[0]>>3)*4+7)//8+4;rows=[]
    while at<len(body):
        start=at;head=struct.unpack_from('<H',body,at)[0];at+=2;size=head&63
        if size==63:size=struct.unpack_from('<I',body,at)[0];at+=4
        at+=size;rows.append(body[start:at])
    rebuilt=bytes.fromhex(spec['prefix'])+b''.join(rows[o] if type(o) is int else bytes.fromhex(o) for o in spec['tags'])
    if hashlib.sha256(rebuilt).hexdigest()!=spec['bodySha256']:raise ValueError('Test fixture reconstruction failed')
    output.write_bytes(bytes.fromhex(spec['targetHeader'])+zlib.compress(rebuilt,9))
