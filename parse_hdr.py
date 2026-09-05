import struct, sys

def analyze(path):
    with open(path, 'rb') as f:
        data = f.read(4096)
    print(f"=== {path.split('/')[-1]} (read={len(data)}) ===")
    be = lambda o: struct.unpack('>i', data[o:o+4])[0]
    le = lambda o: struct.unpack('<i', data[o:o+4])[0]
    print(f"  metadataSize  BE={be(0):>12}  LE={le(0):>12}")
    print(f"  fileSize      BE={be(4):>12}  LE={le(4):>12}")
    print(f"  generation    BE={be(8):>12}  LE={le(8):>12}")
    print(f"  dataOffset    BE={be(12):>12}  LE={le(12):>12}")
    print(f"  bytes 0x8-0xF: {data[8:16].hex()}")
    idx = data.find(b'2019.4.25f1')
    print(f"  version string at offset: {idx}")
    print(f"  byte@0x10 (endian?): {data[16]:#04x}")

for p in sys.argv[1:]:
    analyze(p)
