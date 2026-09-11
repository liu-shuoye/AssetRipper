using System.Collections.Concurrent;
using System.Reflection;
using AssetRipper.Assets;
using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Assets.Metadata;

namespace AssetRipper.Diagnostics.Memory;

/// <summary>
/// 通过反射遍历对象图，估算一个托管对象在 GC 堆上占用的近似字节数。
/// 用于内存诊断时按资源类型统计"真实托管堆字节数"（而非序列化数据字节数）。
/// </summary>
/// <remarks>
/// 这是基于反射的近似值（含对象头、实例字段、数组、字符串及引用对象），并非 GC 的精确值。
/// 为性能与正确性，遍历到集合/包/结构等共享基础设施类型时只计其指针、不再向下递归，
/// 避免把整个 GameBundle 图算进某一个资产导致统计失真与巨量开销。
/// 调用方应在一次完整统计内共享同一个 visited 集合，以保证被多个资产共享的引用对象只被计入一次
/// （符合"堆中只存在一份"的实情，从而让各类型累加值的总和接近真实托管堆占用）。
/// </remarks>
public static class ManagedSizeCalculator
{
	// 按类型缓存反射结果，避免对大量同类型对象重复 GetFields
	private static readonly ConcurrentDictionary<Type, long> ValueTypeSizeCache = new();
	private static readonly ConcurrentDictionary<Type, FieldInfo[]> FieldCache = new();
	// 值类型是否含引用字段（类/数组/字符串），用于决定值类型数组元素是否需要逐元素递归
	private static readonly ConcurrentDictionary<Type, bool> HasRefFieldCache = new();

	/// <summary>
	/// 计算 root 对象（及其引用图，排除共享基础设施）的近似托管堆字节数。
	/// </summary>
	/// <param name="root">待估算的对象（通常为 IUnityObjectBase）。</param>
	/// <param name="visited">一次完整统计内共享的已访问集合，按引用相等去重。</param>
	public static long ComputeSize(object? root, HashSet<object> visited)
	{
		if (root is null)
		{
			return 0;
		}
		return Walk(root, visited);
	}

	private static long Walk(object obj, HashSet<object> visited)
	{
		if (obj is null)
		{
			return 0;
		}
		Type type = obj.GetType();

		// 数组单独处理（数组本身是引用类型，需记录已访问）
		if (type.IsArray)
		{
			return WalkArray((Array)obj, type, visited);
		}

		// 字符串：对象头 + 长度(int) + UTF-16 字符（字符已全含，无需递归）
		if (obj is string s)
		{
			return IntPtr.Size * 2 + 4 + s.Length * 2;
		}

		// 引用类型：同一对象只计一次，避免共享引用被重复累加
		if (!type.IsValueType)
		{
			if (visited.Contains(obj))
			{
				return 0;
			}
			visited.Add(obj);
		}

		return WalkFields(obj, type, visited, type.IsValueType);
	}

	private static long WalkArray(Array array, Type arrayType, HashSet<object> visited)
	{
		if (visited.Contains(array))
		{
			return 0;
		}
		visited.Add(array);

		Type elemType = arrayType.GetElementType()!;
		long elemCount = 1;
		for (int r = 0; r < array.Rank; r++)
		{
			elemCount *= array.GetLength(r);
		}

		// 数组对象头：对象头(2*IntPtr) + 秩与长度信息(约 IntPtr*(1+rank))
		long size = IntPtr.Size * (3 + array.Rank);
		if (elemType.IsValueType)
		{
			if (elemType.IsPrimitive || elemType.IsEnum)
			{
				// 基础类型元素：直接按大小 × 元素数，最快路径（如 byte[]/int[]）
				size += elemCount * PrimitiveSize(elemType);
			}
			else if (HasReferenceField(elemType))
			{
				// 含引用字段的值类型（如 ObjectInfo 持有 SmartStream/Type）：逐元素按值递归，
				// 以计入其引用对象。共享实例经 visited 只计一次，故首次之后均为命中 visited 的快速路径。
				foreach (object? item in array)
				{
					if (item is not null)
					{
						size += WalkFields(item, elemType, visited, true);
					}
				}
			}
			else
			{
				// 纯值类型（如 Vector3，仅含基础字段）：按浅层大小 × 元素数，不逐个迭代以免大量装箱
				size += elemCount * ValueTypeShallowSize(elemType);
			}
		}
		else
		{
			size += elemCount * IntPtr.Size; // 引用类型元素的指针
			foreach (object? item in array)
			{
				if (item is not null)
				{
					size += Walk(item, visited);
				}
			}
		}
		return size;
	}

	private static long WalkFields(object obj, Type type, HashSet<object> visited, bool isValueType)
	{
		// 引用类型有对象头(sync block + 方法表)，值类型无对象头（内联在父对象中）
		long size = isValueType ? 0 : IntPtr.Size * 2;

		Type? cur = type;
		while (cur is not null && cur != typeof(object))
		{
			FieldInfo[] fields = FieldCache.GetOrAdd(cur, c => c.GetFields(
				BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
			foreach (FieldInfo field in fields)
			{
				if (field.IsStatic || field.IsLiteral)
				{
					continue;
				}
				Type ft = field.FieldType;
				// 指针/byref 字段无法安全取值，按一个指针占位
				if (ft.IsPointer || ft.IsByRef)
				{
					size += IntPtr.Size;
					continue;
				}
				object? value;
				try
				{
					value = field.GetValue(obj);
				}
				catch
				{
					// ref struct、init-only 等无法读取的字段：按指针占位忽略
					size += IntPtr.Size;
					continue;
				}
				if (ft.IsValueType)
				{
					if (ft.IsPrimitive || ft.IsEnum)
					{
						size += PrimitiveSize(ft);
					}
					else if (value is not null)
					{
						// 值类型按值内联在父对象中，按值递归（不计入 visited，因为按值复制各有独立存储）
						size += WalkFields(value, ft, visited, true);
					}
				}
				else
				{
					// 引用字段本身占一个指针；仅在允许递归的范围内展开引用对象
					size += IntPtr.Size;
					if (value is not null && ShouldRecurse(value))
					{
						size += Walk(value, visited);
					}
					// 共享基础设施（集合/包等）只计指针，不向下展开，防止牵出整个 GameBundle
				}
			}
			cur = cur.BaseType;
		}
		return size;
	}

	/// <summary>
	/// 是否继续向下递归该引用对象。集合、包、结构等共享基础设施一旦展开会牵出整个 GameBundle，
	/// 因此只计其指针、不再深入，避免统计失真与巨量开销。
	/// </summary>
	private static bool ShouldRecurse(object obj)
	{
		Type t = obj.GetType();
		string? ns = t.Namespace;
		if (ns is null)
		{
			return true;
		}
		if (ns.StartsWith("AssetRipper.Assets.Collections", StringComparison.Ordinal))
		{
			return false;
		}
		if (ns.StartsWith("AssetRipper.Assets.Bundles", StringComparison.Ordinal))
		{
			return false;
		}
		if (ns.StartsWith("AssetRipper.Import.Structure", StringComparison.Ordinal))
		{
			return false;
		}
		if (t == typeof(AssetInfo))
		{
			return false;
		}
		return true;
	}

	/// <summary>
	/// 判断值类型（含嵌套值类型）是否包含引用类型字段（类/数组/字符串）。
	/// 用于决定值类型数组元素是否需要逐元素递归：只有含引用字段的值类型（如 ObjectInfo）才需要，
	/// 纯值类型（如 Vector3）只需浅层大小 × 元素数，避免对海量元素逐个装箱递归。
	/// </summary>
	private static bool HasReferenceField(Type vt)
	{
		if (HasRefFieldCache.TryGetValue(vt, out bool cached))
		{
			return cached;
		}
		bool result = false;
		Type? cur = vt;
		while (cur is not null && cur != typeof(object) && cur != typeof(ValueType) && !result)
		{
			foreach (FieldInfo field in cur.GetFields(
				BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
			{
				if (field.IsStatic || field.IsLiteral)
				{
					continue;
				}
				Type ft = field.FieldType;
				if (!ft.IsValueType)
				{
					// 引用类型、数组、字符串、接口等均为引用语义，算作含引用
					result = true;
					break;
				}
				if (ft.IsPrimitive || ft.IsEnum)
				{
					continue; // 基础类型不含引用
				}
				// 嵌套值类型：递归判断其内部是否含引用
				if (HasReferenceField(ft))
				{
					result = true;
					break;
				}
			}
			cur = cur.BaseType;
		}
		HasRefFieldCache[vt] = result;
		return result;
	}

	/// <summary>
	/// 计算值类型（含嵌套值类型）的浅层大小，引用字段按指针占位、不递归其内部。
	/// 用于值类型数组的元素内联大小估算与值类型字段的递归。
	/// </summary>
	private static long ValueTypeShallowSize(Type vt)
	{
		if (ValueTypeSizeCache.TryGetValue(vt, out long cached))
		{
			return cached;
		}
		long size = 0;
		Type? cur = vt;
		while (cur is not null && cur != typeof(object) && cur != typeof(ValueType))
		{
			foreach (FieldInfo field in cur.GetFields(
				BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
			{
				if (field.IsStatic || field.IsLiteral)
				{
					continue;
				}
				Type ft = field.FieldType;
				if (ft.IsValueType)
				{
					if (ft.IsPrimitive || ft.IsEnum)
					{
						size += PrimitiveSize(ft);
					}
					else
					{
						size += ValueTypeShallowSize(ft);
					}
				}
				else
				{
					size += IntPtr.Size;
				}
			}
			cur = cur.BaseType;
		}
		ValueTypeSizeCache[vt] = size;
		return size;
	}

	/// <summary>
	/// 基础类型的托管大小（字节）。
	/// </summary>
	private static long PrimitiveSize(Type t)
	{
		if (t == typeof(int) || t == typeof(uint) || t == typeof(float)) return 4;
		if (t == typeof(long) || t == typeof(ulong) || t == typeof(double)) return 8;
		if (t == typeof(short) || t == typeof(ushort) || t == typeof(char)) return 2;
		if (t == typeof(byte) || t == typeof(sbyte) || t == typeof(bool)) return 1;
		if (t == typeof(IntPtr) || t == typeof(UIntPtr)) return IntPtr.Size;
		return 4; // 其余枚举/特殊基础类型近似为 4 字节
	}
}
