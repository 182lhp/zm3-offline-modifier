"""Read the declared CArchive TOC boundary; payload sizes must not change it."""
import struct
MAGIC=b'MEI\x0c\x0b\x0a\x0b\x0e'
def archive(data):
    cookie=data.rindex(MAGIC)
    magic,length,offset,table_size,version,library=struct.unpack_from('!8sIIII64s',data,cookie)
    start=cookie+88-length;at=start+offset;end=at+table_size;entries=[]
    while at<end:
        row_size,position,packed_size,plain,compressed,kind=struct.unpack_from('!IIIIBc',data,at)
        assert row_size>=18 and at+row_size<=end
        raw_name=data[at+18:at+row_size];name=raw_name.split(b'\0')[0].decode()
        entries.append((name,raw_name,data[start+position:start+position+packed_size],plain,compressed,kind));at+=row_size
    assert at==end
    return data[:start],entries,version,library
