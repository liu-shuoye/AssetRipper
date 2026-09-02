"""最小化 CLI 元数据解析：只读表流头部的"每个表有多少行"，用来判断
DummyDll 里是否存在 MethodImpl（显式接口实现）表记录。"""

import struct
import sys

TABLE_NAMES = [
	"Module", "TypeRef", "TypeDef", "FieldPtr", "Field", "MethodPtr", "MethodDef",
	"ParamPtr", "Param", "InterfaceImpl", "MemberRef", "Constant", "CustomAttribute",
	"FieldMarshal", "DeclSecurity", "ClassLayout", "FieldLayout", "StandAloneSig",
	"EventMap", "EventPtr", "Event", "PropertyMap", "PropertyPtr", "Property",
	"MethodSemantics", "MethodImpl", "ModuleRef", "TypeSpec", "ImplMap", "FieldRVA",
	"EncLog", "EncMap", "Assembly", "AssemblyProcessor", "AssemblyOS", "AssemblyRef",
	"AssemblyRefProcessor", "AssemblyRefOS", "File", "ExportedType", "ManifestResource",
	"NestedClass", "GenericParam", "MethodSpec", "GenericParamConstraint",
]


def rva_to_offset(sections, rva):
	for va, vsize, praw, psize in sections:
		if va <= rva < va + max(vsize, psize):
			return praw + (rva - va)
	raise ValueError(f"RVA 0x{rva:x} 不在任何节区内")


def read_metadata_tables(path):
	data = open(path, "rb").read()

	# ---- PE 头 ----
	e_lfanew = struct.unpack_from("<I", data, 0x3C)[0]
	assert data[e_lfanew:e_lfanew + 4] == b"PE\0\0", "不是合法的 PE 文件"

	opt_start = e_lfanew + 4 + 20
	magic = struct.unpack_from("<H", data, opt_start)[0]
	pe32plus = magic == 0x20B
	num_rva_offset = opt_start + (108 if pe32plus else 92)
	num_rva = struct.unpack_from("<I", data, num_rva_offset)[0]
	dd_start = num_rva_offset + 4

	sections = []
	sec_off = opt_start + (240 if pe32plus else 224)  # 跳过标准域 + 特定域
	# 更稳妥：节表紧跟数据目录之后
	sec_off = dd_start + num_rva * 8
	for i in range(struct.unpack_from("<H", data, e_lfanew + 4 + 2)[0]):
		off = sec_off + i * 40
		name = data[off:off + 8]
		vsize, va, psize, praw = struct.unpack_from("<IIII", data, off + 8)
		sections.append((va, vsize, praw, psize))

	cli_rva = struct.unpack_from("<I", data, dd_start + 14 * 8)[0]
	cli_off = rva_to_offset(sections, cli_rva)
	md_rva = struct.unpack_from("<I", data, cli_off + 8)[0]
	md_off = rva_to_offset(sections, md_rva)

	# ---- 元数据根 ----
	assert data[md_off:md_off + 4] == b"BSJB", "不是合法的 CLI 元数据"
	ver_len = struct.unpack_from("<I", data, md_off + 12)[0]
	cur = md_off + 16 + ((ver_len + 3) & ~3)
	num_streams = struct.unpack_from("<H", data, cur + 2)[0]
	cur += 4

	streams = {}
	for _ in range(num_streams):
		offset, size = struct.unpack_from("<II", data, cur)
		cur += 8
		end = data.index(b"\0", cur)
		name = data[cur:end].decode("ascii")
		cur = end + 1
		cur = (cur - md_off + 3) // 4 * 4 + md_off  # 4 字节对齐
		streams[name] = (md_off + offset, size)

	tbl_off, _ = streams["#~"]
	valid, sorted_ = struct.unpack_from("<QQ", data, tbl_off + 8)
	cur = tbl_off + 24

	rows = {}
	for i in range(64):
		if valid & (1 << i):
			rows[i] = struct.unpack_from("<I", data, cur)[0]
			cur += 4
	return rows, streams, data


def read_strings_heap(path):
	"""读出 #Strings 堆里的全部字符串（元数据中的名字都以 \0 结尾存放）。"""
	_, streams, data = read_metadata_tables(path)
	off, size = streams["#Strings"]
	blob = data[off:off + size]
	return [s.decode("utf-8", "replace") for s in blob.split(b"\0") if s]


if __name__ == "__main__":
	path = sys.argv[1]
	rows, _, _ = read_metadata_tables(path)
	print(f"文件：{path}")
	for idx in sorted(rows):
		name = TABLE_NAMES[idx] if idx < len(TABLE_NAMES) else f"表0x{idx:02x}"
		mark = ""
		if name in ("MethodImpl", "MethodSemantics", "InterfaceImpl", "MethodDef", "TypeDef"):
			mark = "   <== 关注"
		print(f"  0x{idx:02x} {name:<24} 行数={rows[idx]}{mark}")
