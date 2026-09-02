# -*- coding: utf-8 -*-
"""dump bundle 内 Shader 对象的原始字节，并解析头部结构。"""
import sys, struct
import UnityPy

path = sys.argv[1] if len(sys.argv) > 1 else r"D:\UserData\閃耀暖暖_4.1.2328503\assets\art\spine\face\bq_skeletondata.asset_locale.chinesesimplified"
env = UnityPy.load(path)

for obj in env.objects:
    if obj.type.name != "Shader":
        continue
    raw = obj.get_raw_data()
    print(f"PathID={obj.path_id} raw length={len(raw)}")
    out = r"D:\Project\AssetRipper\.workbuddy\tmp\shader_raw.bin"
    open(out, "wb").write(raw)
    print(f"saved -> {out}")
    # 前几个字节按十六进制打印
    head = raw[:128]
    print("head hex:", head.hex(" "))
    # 尝试按 "align 字符串" 解析开头：int32 长度 + 字符串
    for start in (0,):
        n, = struct.unpack_from("<i", raw, start)
        print(f"@{start}: int32={n}")
        if 0 <= n < 200:
            s = raw[start+4:start+4+n]
            print(f"  string({n}) = {s!r}")
    # 版本信息（Header 中的 stripped 版本在文件级，不在对象里）
    # 对象数据开头一般是 m_ObjectHideFlags(int16/int32), PPtr PrefabParentObject 等
