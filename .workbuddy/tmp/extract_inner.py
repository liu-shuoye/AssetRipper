# -*- coding: utf-8 -*-
"""解出序列化文件并手动解析 type table / object info。"""
import struct
import UnityPy

path = r"D:\UserData\閃耀暖暖_4.1.2328503\assets\art\spine\face\bq_skeletondata.asset_locale.chinesesimplified"
env = UnityPy.load(path)
out_dir = r"D:\Project\AssetRipper\.workbuddy\tmp"

for name, f in env.files.items():
    for cname, child in f.files.items():
        # child 是 SerializedFile
        sf = child
        print(f"SerializedFile: {cname}")
        print(f"  unity_version={sf.unity_version} version_engine={getattr(sf,'version_engine',None)}")
        print(f"  target_platform={sf.target_platform}")
        data = sf.save()
        out = f"{out_dir}\\inner_{cname}.bin"
        open(out, 'wb').write(data)
        print(f"  saved {out} len={len(data)}")
        # 遍历 types
        for i, t in enumerate(sf.unity.types if hasattr(sf, 'unity') else sf.types):
            tt = getattr(t, 'type_tree', None)
            print(f"  type[{i}] class_id={t.class_id} stripped={getattr(t,'stripped',None)} script_type={getattr(t,'script_type_index',None)} type_id={getattr(t,'type_id',None)}")
