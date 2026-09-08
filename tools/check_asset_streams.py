#!/usr/bin/env python3
"""扫描文件夹下所有 .asset（YAML 文本），检查 VertexData.m_Channels 是否存在越界顶点流（stream > max_stream）。

背景：闪耀暖暖（Nikki4）魔改引擎序列化的 Mesh 允许 5 个及以上顶点流（stream 索引
可达 4、5...），而标准 Unity 只允许 0-3。这样的资源导入 Unity 会直接崩溃
（"Vertex stream out of range: 4 (max 3)"）。导出修复后应保证不再有越界流，
本工具用于批量验收导出产物。

用法：
    python check_asset_streams.py <根目录> [--max-stream 3] [--json] [--quiet]

退出码：0 = 未发现越界流；1 = 发现越界流；2 = 参数/IO 错误。
"""

from __future__ import annotations

import argparse
import json
import shutil
import sys
from dataclasses import dataclass, field
from pathlib import Path

# Unity 标准允许的最大顶点流索引（流索引 0-3）
DEFAULT_MAX_STREAM = 3


@dataclass
class BadAsset:
    """单个违规文件：记录了越界的 channel 序号与其 stream 值。"""

    path: Path
    violations: list[tuple[int, int]] = field(default_factory=list)

    @property
    def max_stream(self) -> int:
        return max(v[1] for v in self.violations)


def find_bad_streams(path: Path, max_stream: int) -> list[tuple[int, int]]:
    """解析一个 .asset，返回所有 stream > max_stream 的 (channel 索引, stream 值)。

    仅统计 VertexData.m_Channels 块内的 "stream:" 键，避免把其它结构
    （如 AnimationClip 的 m_Streams 等）里的同名字段误判为顶点流。
    """
    bad: list[tuple[int, int]] = []
    channels_indent: int | None = None  # m_Channels: 行的缩进；None 表示当前不在该块内
    channel_index = 0

    lines = path.read_text(encoding="utf-8-sig").splitlines()
    for line in lines:
        stripped = line.strip()
        if not stripped:
            continue
        indent = len(line) - len(line.lstrip(" "))

        if channels_indent is not None:
            # 块结束条件：缩进小于等于 m_Channels 行，且不是列表项「- xxx」
            if indent <= channels_indent and not stripped.startswith("-"):
                channels_indent = None
                continue
            # 只认列表项的 stream 键；子字段（offset/format/dimension）与 `- stream:` 同层次
            if stripped.startswith("- stream:"):
                raw = stripped.split(":", 1)[1].strip()
                try:
                    stream = int(raw)
                except ValueError:
                    stream = -1
                if stream > max_stream:
                    bad.append((channel_index, stream))
                channel_index += 1
            continue

        # 未进入块：m_Channels 可能带内联空数组（m_Channels: ），仅处理换行展开形式
        if stripped in ("m_Channels:", "m_Channels: []"):
            channels_indent = indent
            channel_index = 0
            if stripped == "m_Channels: []":
                channels_indent = None

    return bad


def scan(root: Path, max_stream: int) -> list[BadAsset]:
    """递归扫描根目录下全部 .asset。"""
    results: list[BadAsset] = []
    for path in sorted(root.rglob("*.asset")):
        try:
            violations = find_bad_streams(path, max_stream)
        except (OSError, UnicodeDecodeError) as exc:
            print(f"警告：跳过无法读取的文件 {path}：{exc}", file=sys.stderr)
            continue
        if violations:
            results.append(BadAsset(path, violations))
    return results


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="检查导出产物中是否有 Unity 不允许的越界顶点流（stream > max_stream）。",
        formatter_class=argparse.ArgumentDefaultsHelpFormatter,
    )
    parser.add_argument("root", type=Path, help="要扫描的目录（递归）。")
    parser.add_argument(
        "--max-stream",
        type=int,
        default=DEFAULT_MAX_STREAM,
        help="允许的最大 stream 索引（Unity 标准为 3）。",
    )
    parser.add_argument("--json", action="store_true", help="以 JSON 输出机器可读的完整结果。")
    parser.add_argument("--quiet", action="store_true", help="只输出汇总行，不打印每个违规文件。")
    parser.add_argument(
        "--move-out",
        type=Path,
        metavar="DIR",
        help="把越界文件移动到 DIR（保留相对路径），避免 Unity 再次导入崩溃；.meta 一并移走。",
    )
    return parser.parse_args()


def move_bad_assets(root: Path, bad_assets: list[BadAsset], dest: Path) -> tuple[int, list[Path]]:
    """把越界 asset（及其 .meta）移动到 dest 下，保留相对 root 的目录结构。

    返回 (成功移动数, 失败文件列表)：
    - 移动前先确认目标目录可用，避免 Unity 扫描备份内容
    - 同目标已存在同名文件时追加时间戳后缀，防止误覆盖
    """
    moved = 0
    failed: list[Path] = []
    for asset in bad_assets:
        try:
            rel = asset.path.relative_to(root)
            target = dest / rel
            target.parent.mkdir(parents=True, exist_ok=True)
            if target.exists():
                target = target.with_name(f"{target.stem}_{asset.path.stat().st_mtime_ns}{target.suffix}")
            # replace 不支持跨磁盘，备份目录可能在别的盘，统一用 shutil.move
            shutil.move(str(asset.path), str(target))
            moved += 1
            # Unity 资源元数据一并移走，避免原目录残留 meta 触发重新导入
            meta = asset.path.with_suffix(asset.path.suffix + ".meta")
            if meta.exists():
                meta_target = target.with_suffix(target.suffix + ".meta")
                try:
                    shutil.move(str(meta), str(meta_target))
                except OSError:
                    failed.append(meta)
        except (OSError, ValueError) as exc:
            print(f"警告：移动失败 {asset.path}：{exc}", file=sys.stderr)
            failed.append(asset.path)
    return moved, failed


def main() -> int:
    args = parse_args()
    root: Path = args.root
    if not root.is_dir():
        print(f"错误：目录不存在：{root}", file=sys.stderr)
        return 2

    bad_assets = scan(root, args.max_stream)

    total_assets = sum(1 for f in root.rglob("*.asset"))
    bad_count = len(bad_assets)

    # 先执行移出（会改变根目录内容），再输出结果，保证报告基于最终状态
    moved = 0
    failed: list[Path] = []
    if args.move_out is not None:
        if bad_count == 0:
            print(f"无需移动：未发现越界流。")
        else:
            args.move_out.mkdir(parents=True, exist_ok=True)
            moved, failed = move_bad_assets(root, bad_assets, args.move_out)
            # 移出改变了目录内容，重扫以输出准确的最终状态
            bad_assets = scan(root, args.max_stream)
            bad_count = len(bad_assets)
            total_assets = sum(1 for f in root.rglob("*.asset"))

    if args.json:
        payload = {
            "root": str(root),
            "max_stream": args.max_stream,
            "total_assets": total_assets,
            "bad_assets": [
                {
                    "path": str(a.path),
                    "violations": [{"channel": c, "stream": s} for c, s in a.violations],
                    "max_stream_used": a.max_stream,
                }
                for a in bad_assets
            ],
            "moved_to": str(args.move_out) if args.move_out else None,
            "moved_count": moved,
            "move_failed": [str(p) for p in failed],
        }
        print(json.dumps(payload, ensure_ascii=False, indent=2))
        return 0 if bad_count == 0 and not failed else 1

    print(f"扫描目录：{root}")
    print(f"asset 总数：{total_assets} | 越界流文件数：{bad_count}")
    if args.move_out and moved:
        print(f"已移出 {moved} 个越界文件到：{args.move_out}")
    if failed:
        print(f"移出失败 {len(failed)} 个，详见上方警告。", file=sys.stderr)
    if not args.quiet:
        for asset in bad_assets:
            detail = ", ".join(f"ch{c}=stream{s}" for c, s in asset.violations)
            print(f"  BAD: {asset.path}  ({detail})")
    if bad_count:
        print("结论：仍存在越界顶点流，导入 Unity 会崩溃。")
        return 1
    print("结论：未发现越界顶点流。")
    return 0


if __name__ == "__main__":
    sys.exit(main())