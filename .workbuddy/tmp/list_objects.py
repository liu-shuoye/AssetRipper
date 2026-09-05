# -*- coding: utf-8 -*-
"""用 UnityPy 列出 bundle 内所有对象：PathID、ClassID、类名、名字。"""
import sys
import UnityPy

path = sys.argv[1] if len(sys.argv) > 1 else r"D:\UserData\閃耀暖暖_4.1.2328503\assets\art\spine\face\bq_skeletondata.asset_locale.chinesesimplified"
env = UnityPy.load(path)

for obj in env.objects:
    name = ""
    try:
        d = obj.read(check_read=False)
        name = getattr(d, "m_Name", "") or ""
    except Exception as e:
        name = f"<read fail: {type(e).__name__}: {e}>"
    print(f"PathID={obj.path_id} ClassID={obj.type.name} ({obj.type_id}) name={name!r}")
