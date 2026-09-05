# -*- coding: utf-8 -*-
"""快速解析 UnityFS bundle，列出内部序列化文件的类型表与对象表。"""
import struct, sys, io
import lz4.block

path = sys.argv[1]

data = open(path, 'rb').read()
assert data[:8] == b'UnityFS\x00', data[:8]

pos = 8
def read_null_str(d, p):
    end = d.index(b'\x00', p)
    return d[p:end].decode('utf-8'), end + 1

sig_end = data.index(b'\x00', pos)
version_str = data[pos:sig_end].decode()
pos = sig_end + 1
unity_versions = data[pos:sig_end+1] # not used
# 重新按 UnityFS 结构读
pos = 8
ver, = struct.unpack_from('>I', data, pos); pos += 4
unity_ver, pos = read_null_str(data, pos)
unity_rev, pos = read_null_str(data, pos)
size, csize, udata = struct.unpack_from('>III', data, pos); pos += 12
comp_type = data[pos]; pos += 1
print(f'ver={ver} unity={unity_ver} rev={unity_rev} size={size} csize={csize} udata={udata} comp={comp_type}')
# crypthash
pos += 16
node_count, = struct.unpack_from('>I', data, pos); pos += 4
# (v6+) block info & node info flags; Unity 2019.4 uses version 6 with compressed blockinfo
has_blocks_info = 0x40
offset = 0
flags = 0
if ver >= 6:
    # node_count contains flags in high bits
    flags = node_count >> 4
    node_count &= 0xF  # not exactly; actually node_count & 0x3FFFFFFF and flags bits 30-31
# rebuild per official format:
pos -= 4
nodeinfo_and_flags, = struct.unpack_from('>I', data, pos); pos += 4
node_count = nodeinfo_and_flags & 0x3F
blocks_info_flags = (nodeinfo_and_flags >> 6) & 0x3F
print(f'node_count={node_count} blocks_info_flags={blocks_info_flags:x}')

for i in range(node_count):
    offset, ssize, flags = struct.unpack_from('>III', data, pos); pos += 12
    name, pos = read_null_str(data, pos)
    print(f'node[{i}] name={name} offset={offset} size={ssize} flags={flags}')

# blocks info may be compressed
blocks_info_pos = pos
binfo_data = data[pos:]
if blocks_info_flags & 0x3F == 0x42 or (blocks_info_flags & 0x3F):  # compressed with lz4?
    # try decompress lz4
    try:
        dec = lz4.block.decompress(binfo_data, uncompressed_size=1024*1024)
        binfo_data = dec
        bpos = 0
    except Exception as e:
        print('blockinfo lz4 fail', e); bpos = 0
else:
    bpos = 0

block_count, = struct.unpack_from('>I', binfo_data, bpos); bpos += 4
print(f'block_count={block_count}')
buf = bytearray()
for i in range(block_count):
    u, c, fl = struct.unpack_from('>IIH', binfo_data, bpos); bpos += 10
    comp = fl & 0x3F
    chunk = data[blocks_info_pos + bpos - bpos:]  # placeholder
# 重新按顺序读块数据：块数据紧跟 blocksinfo（若压缩则紧跟压缩数据之后）
# 实际结构：header + nodeinfo + blocksinfo + blocksdata
# 若 blocksinfo 压缩,块数据紧跟压缩 blocksinfo 之后
data_start = pos
if blocks_info_flags & 0x3F:
    data_start = blocks_info_pos + bpos_decompressed_len  # need real compressed length
print('NOTE: using alternative approach')
