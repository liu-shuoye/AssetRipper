# -*- coding: utf-8 -*-
"""Mini CIL disassembler: 打印方法的调用序列(方法名)与 ldstr(字段名)，用于理解生成 Walk 方法结构。"""
import sys, struct
import dnfile

path = r'C:\Users\Administrator\.nuget\packages\assetripper.sourcegenerated\1.3.14.2\lib\net10.0\AssetRipper.SourceGenerated.dll'
pe = dnfile.dnPE(path)
md = pe.net.mdtables

# 方法 token -> 名字缓存
def method_name_by_token(tok):
    table = tok & 0xFF000000
    idx = tok & 0xFFFFFF
    try:
        if table == 0x0A000000:  # MemberRef
            row = md.MemberRef.rows[idx-1]
            return 'MemberRef::' + str(row.Name)
        elif table == 0x06000000:  # MethodDef
            row = md.MethodDef.rows[idx-1]
            return 'MethodDef::' + str(row.Name)
        elif table == 0x2B000000:  # MethodSpec
            return 'MethodSpec'
    except Exception as e:
        return f'#{tok:x}'
    return f'?{tok:x}'

def resolve_typeref(tok):
    try:
        if (tok & 0xFF000000) == 0x01000000:
            row = md.TypeRef.rows[(tok & 0xFFFFFF)-1]
            return str(row.TypeNamespace) + '.' + str(row.TypeName)
        if (tok & 0xFF000000) == 0x02000000:
            row = md.TypeDef.rows[(tok & 0xFFFFFF)-1]
            return str(row.TypeNamespace) + '.' + str(row.TypeName)
    except Exception:
        pass
    return '?'

def read_method_calls(rva):
    # map RVA to file offset via sections
    for sec in pe.sections:
        va = sec.VirtualAddress
        vsize = max(int(getattr(sec, 'Misc_VirtualSize', 0)), sec.SizeOfRawData)
        if va <= rva < va + vsize:
            off = rva - va + sec.PointerToRawData
            data = pe.get_bytes(off, 4096)
            break
    else:
        return None
    # method header
    b0 = data[0]
    if b0 & 0x3 == 2:
        # tiny
        code_size = b0 >> 2
        body = data[1:1+code_size]
    else:
        if b0 & 0x40:
            extra = struct.unpack_from('<H', data, 1)[0]
            hdr = 12 + extra
        else:
            hdr = 12
        code_size = struct.unpack_from('<I', data, 4)[0]
        body = data[hdr:hdr+code_size]
    return body

def disasm(rva, max_ops=900):
    body = read_method_calls(rva)
    if body is None:
        print('  (no body)'); return
    ops = []
    i = 0
    calls = []
    while i < len(body) and len(calls) < max_ops:
        op = body[i]
        i += 1
        # 简单 operand 尺寸映射
        if op == 0x28 or op == 0x6F:  # call/callvirt: 4B token
            tok = struct.unpack_from('<I', body, i)[0]; i += 4
            calls.append(('call', method_name_by_token(tok)))
        elif op == 0x72:  # ldstr: 4B token
            tok = struct.unpack_from('<I', body, i)[0]; i += 4
            # resolve string from US heap
            try:
                s = pe.net.user_strings.get_heap(tok & 0xFFFFFF).value if False else ''
            except Exception:
                s = ''
            calls.append(('ldstr', f'str#{tok&0xFFFFFF:x}'))
        elif op == 0x73:  # newobj 4B
            tok = struct.unpack_from('<I', body, i)[0]; i += 4
            calls.append(('newobj', method_name_by_token(tok)))
        elif op in (0x02,0x03,0x04,0x05,0x06,0x07,0x08,0x09,0x0A,0x0B,0x0C,0x0D,0x0E,0x0F,0x10,0x11,0x12,0x13,0x14,0x15,0x16,0x17,0x18,0x19,0x1A,0x1B,0x1C,0x1D,0x1E,0x1F,0x20,0x21,0x22,0x23,0x24,0x25,0x26,0x27):
            pass
        elif op == 0x7D or op == 0x7C:  # stfld/ldfld
            tok = struct.unpack_from('<I', body, i)[0]; i += 4
            calls.append(('fld', method_name_by_token(tok)))
        elif op == 0x6E or op == 0x7A:  # ldflda / 
            tok = struct.unpack_from('<I', body, i)[0]; i += 4
            calls.append(('flda', method_name_by_token(tok)))
        elif op == 0x2A or op == 0x2B or op == 0x26 or op == 0x39 or op == 0x3A or op == 0x3B or op == 0x3C:
            pass
        else:
            # 处理操作数
            if op in (0x2C,0x2D,0x2E,0x2F,0x30,0x31,0x32,0x33,0x34,0x35,0x36,0x37,0x38):  # branches sbyte
                i += 1
            elif op in (0x3D,0x3E,0x3F,0x40,0x41,0x42,0x43,0x44):  # branch int32
                i += 4
            elif op == 0x20:  # ldc.i4.s
                i += 1
            elif op == 0x1F:  # ldc.i4
                i += 4
            elif op == 0xDE or op == 0x8F or op == 0xD0:  # 2B
                i += 2
            elif op == 0xFE:
                op2 = body[i]; i += 1
                if op2 == 0x09:  # ldsflda
                    i += 4
            elif op in (0x70,):  # tail.
                pass
    return calls

import dnfile as _df
target = None
for t in md.TypeDef.rows:
    if str(t.TypeName) == 'Shader_2019_3_0_b0' and str(t.TypeNamespace).endswith('ClassID_48'):
        target = t
for m in target.MethodList:
    row = m.row
    if str(row.Name) in ('WalkStandard','WalkRelease'):
        print('======', row.Name, 'RVA', hex(int(row.Rva)))
        calls = disasm(int(row.Rva))
        # 去重打印前 120 项
        for kind, name in (calls or [])[:120]:
            print('   ', kind, name)
