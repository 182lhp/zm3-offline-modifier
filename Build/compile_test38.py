import marshal,types
def ports(v):
    if isinstance(v,types.CodeType):return v.replace(co_consts=tuple(ports(x) for x in v.co_consts))
    if isinstance(v,bytes) and len(v)>300 and v[0]&127==ord('c'):return marshal.dumps(ports(marshal.loads(v)))
    if isinstance(v,tuple):return tuple(ports(x) for x in v)
    if isinstance(v,int) and v==43999:return 44049
    if isinstance(v,str):return v.replace('43999','44049')
    return v
marshal.dump(ports(marshal.load(open('server.marshal','rb'))),open('server-test.marshal','wb'))
