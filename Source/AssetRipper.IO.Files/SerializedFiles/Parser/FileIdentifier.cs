using AssetRipper.IO.Files.SerializedFiles.IO;

namespace AssetRipper.IO.Files.SerializedFiles.Parser;

/// <summary>
/// 一个序列化文件可以与其他序列化文件链接，以创建共享依赖关系。
/// </summary>
public struct FileIdentifier
{
	/// <summary>
	/// 2.1.0 及以上
	/// </summary>
	public static bool HasAssetPath(FormatVersion generation) => generation >= FormatVersion.Unknown_6;
	/// <summary>
	/// 1.2.0 及以上
	/// </summary>
	public static bool HasHash(FormatVersion generation) => generation >= FormatVersion.Unknown_5;

	public readonly bool IsFile([NotNullWhen(true)] SerializedFile? file)
	{
		return file is not null && file.NameFixed == PathName;
	}

	internal void Read(SerializedReader reader)
	{
		if (HasAssetPath(reader.Generation))
		{
			AssetPath = reader.ReadStringZeroTerm();
		}
		if (HasHash(reader.Generation))
		{
			Guid = reader.ReadUnityGuid();
			Type = (AssetType)reader.ReadInt32();
		}
		PathNameOrigin = reader.ReadStringZeroTerm();
		PathName = SpecialFileNames.FixFileIdentifier(PathNameOrigin);
	}

	internal readonly void Write(SerializedWriter writer)
	{
		if (HasAssetPath(writer.Generation))
		{
			writer.WriteStringZeroTerm(AssetPath);
		}
		if (HasHash(writer.Generation))
		{
			writer.Write(Guid);
			writer.Write((int)Type);
		}
		writer.WriteStringZeroTerm(PathNameOrigin);
	}

	public readonly string GetFilePath()
	{
		if (Type == AssetType.Meta)
		{
			return Guid.ToString();
		}
		return PathName;
	}

	public override readonly string? ToString()
	{
		if (Type == AssetType.Meta)
		{
			return Guid.ToString();
		}
		return PathNameOrigin ?? base.ToString();
	}

	/// <summary>
	/// 文件路径，不包含如 archive:/directory/fileName 这样的前缀
	/// </summary>
	public string PathName { get; set; }

	/// <summary>
	/// 虚拟资产路径。用于缓存文件，否则为空。
	/// 该路径下的文件通常不存在，所以这很可能是别名。
	/// </summary>
	public Utf8String AssetPath { get; set; }
	/// <summary>
	/// 文件类型
	/// </summary>
	public AssetType Type { get; set; }
	/// <summary>
	/// 实际文件路径。此路径是相对于当前文件路径的。
	/// 文件夹 "library" 通常需要翻译成 "resources" 才能在文件系统中找到文件。
	/// </summary>
	public string PathNameOrigin { get; set; }

	public UnityGuid Guid { get; set; }
}
