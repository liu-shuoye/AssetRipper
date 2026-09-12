using AssetRipper.IO.Endian;
using AssetRipper.IO.Files.SerializedFiles.IO;
using AssetRipper.IO.Files.SerializedFiles.Parser;
using AssetRipper.IO.Files.Streams.Smart;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace AssetRipper.IO.Files.SerializedFiles;

/// <summary>
/// 序列化文件包含二进制序列化的对象以及可选的运行时类型信息。  
/// 它们的文件扩展名通常为 .asset、.assets、.sharedAssets，但也可能完全不带扩展名。
/// </summary>
public sealed class SerializedFile : FileBase
{
	/// <summary> 依赖项是此文件所必需的外部资源。 </summary>
	private FileIdentifier[]? m_dependencies;

	/// <summary> 文件中的对象信息。 </summary>
	private ObjectInfo[]? m_objects;

	/// <summary> 文件中定义的类型信息。 </summary>
	private SerializedType[]? m_types;

	/// <summary> 脚本类型标识符。 </summary>
	private LocalSerializedObjectIdentifier[]? m_scriptTypes;

	/// <summary> 类型引用信息。 </summary>
	private SerializedTypeReference[]? m_refTypes;

	/// <summary> 文件的格式版本。 </summary>
	public FormatVersion Generation { get; private set; }

	/// <summary> 文件的 Unity 版本。 </summary>
	public UnityVersion Version { get; private set; }

	/// <summary> 文件的目标平台。 </summary>
	public BuildTarget Platform { get; private set; }

	/// <summary> 文件的传输指令标志。 </summary>
	public TransferInstructionFlags Flags
	{
		get
		{
			TransferInstructionFlags flags;
			if (SerializedFileMetadata.HasPlatform(Generation) && Platform == BuildTarget.NoTarget)
			{
				if (FilePath.EndsWith(".unity", StringComparison.Ordinal))
				{
					flags = TransferInstructionFlags.SerializeEditorMinimalScene;
				}
				else
				{
					flags = TransferInstructionFlags.NoTransferInstructionFlags;
				}
			}
			else
			{
				flags = TransferInstructionFlags.SerializeGameRelease;
			}

			if (SpecialFileNames.IsEngineResource(Name) || (Generation < FormatVersion.Unknown_10 && SpecialFileNames.IsBuiltinExtra(Name)))
			{
				flags |= TransferInstructionFlags.IsBuiltinResourcesFile;
			}

			if (EndianType is EndianType.BigEndian)
			{
				flags |= TransferInstructionFlags.SwapEndianess;
			}

			return flags;
		}
	}

	public EndianType EndianType { get; private set; }
	public ReadOnlySpan<FileIdentifier> Dependencies => m_dependencies;
	public ReadOnlySpan<ObjectInfo> Objects => m_objects;
	public ReadOnlySpan<SerializedType> Types => m_types;
	public ReadOnlySpan<LocalSerializedObjectIdentifier> ScriptTypes => m_scriptTypes;
	public ReadOnlySpan<SerializedTypeReference> RefTypes => m_refTypes;
	public bool HasTypeTree { get; private set; }
	public Utf8String UserInformation { get; private set; } = Utf8String.Empty;

	private static EndianType GetEndianType(SerializedFileHeader header, SerializedFileMetadata metadata)
	{
		bool swapEndianess = SerializedFileHeader.HasEndianess(header.Version) ? header.Endianess : metadata.SwapEndianess;
		return swapEndianess ? EndianType.BigEndian : EndianType.LittleEndian;
	}

	public static bool IsSerializedFile(Stream stream)
	{
		using EndianReader reader = new EndianReader(stream, EndianType.BigEndian);
		return SerializedFileHeader.IsSerializedFileHeader(reader, stream.Length);
	}

	public static bool IsSerializedFile(string filePath, FileSystem fileSystem)
	{
		using Stream stream = fileSystem.File.OpenRead(filePath);
		return IsSerializedFile(stream);
	}

	public override string ToString()
	{
		return NameFixed;
	}

	/// <summary> 从指定流中读取文件。 </summary>
	public override void Read(SmartStream stream)
	{
		SerializedFileHeader header = new();
		header.Read(stream);
		if (SerializedFileMetadata.IsMetadataAtTheEnd(header.Version))
		{
			stream.Position = header.FileSize - header.MetadataSize;
		}

		SerializedFileMetadata metadata = new();
		metadata.Read(stream, header);

		SetProperties(header, metadata);
	}

	private void SetProperties(SerializedFileHeader header, SerializedFileMetadata metadata)
	{
		Generation = header.Version;
		Version = metadata.UnityVersion;
		Platform = metadata.TargetPlatform;
		EndianType = GetEndianType(header, metadata);
		m_dependencies = metadata.Externals;
		m_objects = metadata.Object;
		m_types = metadata.Types;
		m_scriptTypes = metadata.ScriptTypes;
		m_refTypes = metadata.RefTypes;
		HasTypeTree = metadata.EnableTypeTree;
		UserInformation = metadata.UserInformation;

		// 解析完成后，非 MonoBehaviour 的类型树（绝大多数引擎类型）在反序列化阶段永不被读取
		// （原生资产由 SourceGenerated 类按硬编码布局反序列化，只有 MonoBehaviour 经 GameAssetFactory 重建字段结构才用 OldType）。
		// 立即释放它们可大幅降低加载期内存峰值；MonoBehaviour 树随后由 ReleaseTypeTrees 在反序列化完成后释放。
		if (ReleaseTypeTreesAfterDeserialization)
		{
			ReleaseUnusedTypeTrees();
		}
	}

	/// <summary>
	/// 释放所有"非 MonoBehaviour"类型的类型树（TypeTree）节点与字符串缓冲。
	/// 这些类型树在文件解析后不会被任何产品代码读取，可安全立即释放以降低加载期内存峰值。
	/// </summary>
	private void ReleaseUnusedTypeTrees()
	{
		if (m_types is null)
		{
			return;
		}
		foreach (SerializedType type in m_types)
		{
			// 仅保留 MonoBehaviour 的类型树：序列化层以 RawTypeID == -1 或 ScriptTypeIndex >= 0 标记脚本类型。
			if (type.RawTypeID != -1 && type.ScriptTypeIndex < 0)
			{
				type.OldType.Nodes.Clear();
				type.OldType.StringBuffer = [];
			}
		}
	}

	public override void Write(Stream stream)
	{
		long initialPosition = stream.Position;
		SerializedFileHeader header = new() { Version = Generation, Endianess = EndianType == EndianType.BigEndian, };
		header.Write(stream);

		using SerializedWriter writer = new(stream, EndianType, Generation, Version);
		SerializedFileMetadata metadata = new()
		{
			UnityVersion = Version,
			TargetPlatform = Platform,
			Externals = m_dependencies ?? [],
			Object = m_objects ?? [],
			Types = m_types ?? [],
			ScriptTypes = m_scriptTypes ?? [],
			RefTypes = m_refTypes ?? [],
			EnableTypeTree = HasTypeTree,
			UserInformation = UserInformation,
		};
		long metadataPosition;
		long metadataSize;
		long objectDataPosition;
		if (SerializedFileMetadata.IsMetadataAtTheEnd(Generation))
		{
			AlignStream(writer, 16); // objectDataPosition must be aligned to 16 bytes
			objectDataPosition = stream.Position;
			WriteObjectData(writer, metadata.Object);
			metadataPosition = stream.Position;
			metadata.Write(writer);
			metadataSize = stream.Position - metadataPosition;
		}
		else
		{
			metadataPosition = stream.Position;
			metadata.Write(writer);
			metadataSize = stream.Position - metadataPosition;
			AlignStream(writer, 16); // objectDataPosition must be aligned to 16 bytes
			objectDataPosition = stream.Position;
			WriteObjectData(writer, metadata.Object);
		}

		long finalPosition = stream.Position;

		stream.Position = initialPosition;
		header.FileSize = finalPosition - initialPosition;
		header.MetadataSize = metadataSize;
		header.DataOffset = objectDataPosition - initialPosition;
		header.Write(new EndianWriter(stream, EndianType.BigEndian));

		stream.Position = finalPosition;

		static void WriteObjectData(SerializedWriter writer, ObjectInfo[] objects)
		{
			foreach (ObjectInfo objectInfo in objects)
			{
				if (objectInfo.ObjectData is not null)
				{
					writer.Write(objectInfo.ObjectData);
				}

				AlignStream(writer, 8); // each object data must be aligned to 8 bytes
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static void AlignStream(SerializedWriter writer, [ConstantExpected] int alignment)
		{
			Debug.Assert(alignment > 0);
			Debug.Assert(BitOperations.IsPow2(alignment));
			long bytesSinceLastAlignment = writer.BaseStream.Position & (alignment - 1);
			if (bytesSinceLastAlignment != 0)
			{
				int padding = alignment - (int)bytesSinceLastAlignment;
				Span<byte> buffer = stackalloc byte[padding];
				buffer.Clear();
				writer.Write(buffer);
			}
		}
	}

	/// <summary>
	/// 反序列化完成后释放所有类型的类型树（TypeTree）节点与字符串缓冲。
	/// 类型树仅在反序列化阶段（GameAssetFactory 通过 <see cref="SerializedTypeBase.OldType"/>）被读取，
	/// 导出阶段不再需要；释放可省去约 1/3 托管堆（TypeTreeNode 节点及大量重复的字段名/类型名字符串）。
	/// 注意：释放后若再次反序列化（例如通过 UnloadAssets 重新加载该集合），MonoBehaviour 将丢失其结构（退回 UnloadedStructure）。
	/// </summary>
	public void ReleaseTypeTrees()
	{
		if (m_types is not null)
		{
			// Reset 会清节点、收缩底层 TypeTreeNode[] 数组并清空字符串缓冲，
			// 使 4652 万个 TypeTreeNode 及重复字符串可被 GC 回收（不只置空 Count）。
			foreach (SerializedType type in m_types)
			{
				type.OldType.Reset();
			}
		}

		if (m_refTypes is not null)
		{
			foreach (SerializedTypeReference type in m_refTypes)
			{
				type.OldType.Reset();
			}
		}
	}

	/// <summary>
	/// 反序列化完成后是否自动释放类型树（TypeTree）。默认开启。
	/// 关闭以保留类型树：例如需要原样回写 SerializedFile，或对已卸载集合重新反序列化时。
	/// </summary>
	public static bool ReleaseTypeTreesAfterDeserialization { get; set; } = true;

	public static SerializedFile FromFile(string filePath, FileSystem fileSystem)
	{
		string fileName = fileSystem.Path.GetFileName(filePath);
		SmartStream stream = SmartStream.OpenRead(filePath, fileSystem);
		return SerializedFileScheme.Default.Read(stream, filePath, fileName);
	}

	public static SerializedFile FromBuilder(SerializedFileBuilder builder)
	{
		return new()
		{
			Generation = builder.Generation,
			Version = builder.Version,
			Platform = builder.Platform,
			EndianType = builder.EndianType,
			m_dependencies = builder.Dependencies.ToArray(),
			m_objects = builder.Objects.ToArray(),
			m_types = builder.Types.ToArray(),
			m_scriptTypes = builder.ScriptTypes.ToArray(),
			m_refTypes = builder.RefTypes.ToArray(),
			HasTypeTree = builder.HasTypeTree,
			UserInformation = builder.UserInformation,
		};
	}

	// FileBase 已实现 IDisposable，这里只需重写 Dispose(bool) 释放 ObjectInfo 持有的 SmartStream 引用。
	private bool disposedValue;

	protected override void Dispose(bool disposing)
	{
		if (!disposedValue)
		{
			if (disposing)
			{
				// 释放所有 ObjectInfo 持有的 SmartStream 引用。
				// 必须通过索引访问 m_objects[i]：ObjectInfo 是 struct，foreach 会按值拷贝，
				// 无法修改原数组元素的字段；m_objects[i] 返回的是数组元素的 ref，可以原位修改。
				// 若 SerializedAssetCollection.ReadData 已通过拷贝释放过引用，此处 FreeReference
				// 会因 SmartStream.Stream 已为 null 而安全跳过，不会重复扣减引用计数。
				if (m_objects is not null)
				{
					for (int i = 0; i < m_objects.Length; i++)
					{
						m_objects[i].ReleaseDataStream();
					}
				}
			}

			disposedValue = true;
		}
	}
}
