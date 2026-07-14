using AssetRipper.Assets.Cloning;
using AssetRipper.Assets.Collections;
using AssetRipper.Assets.Metadata;
using AssetRipper.IO.Endian;

namespace AssetRipper.Assets;

public interface IUnityObjectBase : IUnityAssetBase
{
	/// <summary>
	/// 该资产位置的关键信息。
	/// </summary>
	AssetInfo AssetInfo { get; }
	/// <summary>
	/// 此对象的本地类ID编号。
	/// </summary>
	int ClassID { get; }
	/// <summary>
	/// 此对象的本地类名。
	/// </summary>
	string ClassName { get; }
	/// <summary>
	/// 此对象所属的<see cref="AssetCollection"/>。
	/// </summary>
	AssetCollection Collection { get; }
	/// <summary>
	/// 此对象在<see cref="Collection"/>中的<see cref="AssetInfo.PathID"/>。
	/// </summary>
	long PathID { get; }
	/// <summary>
	/// 此对象的原始路径，如果已知。
	/// </summary>
	/// <remarks>
	/// 路径是相对于项目根目录的，并可能使用正斜杠或反斜杠。
	/// This will never be the empty string.
	/// </remarks>
	string? OriginalPath { get; set; }
	/// <summary>
	/// 此对象的原始目录，如果已知。
	/// </summary>
	/// <remarks>
	/// 路径是相对于项目根目录的，并可能使用正斜杠或反斜杠。
	/// This will never be the empty string.
	/// </remarks>
	string? OriginalDirectory { get; set; }
	/// <summary>
	/// 此对象的原始文件名，如果已知。
	/// </summary>
	/// <remarks>
	/// This will never be the empty string.
	/// </remarks>
	string? OriginalName { get; set; }
	/// <summary>
	/// 此对象的原始文件扩展名，如果已知。
	/// </summary>
	/// <remarks>
	/// This will never be the empty string.
	/// </remarks>
	string? OriginalExtension { get; set; }
	/// <summary>
	/// 此对象的覆盖路径，如果已知。
	/// </summary>
	/// <remarks>
	/// 路径是相对于项目根目录的，并可能使用正斜杠或反斜杠。
	/// This will never be the empty string.
	/// </remarks>
	string? OverridePath { get; set; }
	/// <summary>
	/// 此对象的覆盖目录，如果已知。
	/// </summary>
	/// <remarks>
	/// 路径是相对于项目根目录的，并可能使用正斜杠或反斜杠。
	/// This will never be the empty string.
	/// </remarks>
	string? OverrideDirectory { get; set; }
	/// <summary>
	/// 此对象的覆盖文件名，如果已知。
	/// </summary>
	/// <remarks>
	/// This will never be the empty string.
	/// </remarks>
	string? OverrideName { get; set; }
	/// <summary>
	/// 此对象的覆盖文件扩展名，如果已知。
	/// </summary>
	/// <remarks>
	/// This will never be the empty string.
	/// </remarks>
	string? OverrideExtension { get; set; }
	/// <summary>
	/// 此对象所属的资源包名称，如果已知。
	/// </summary>
	/// <remarks>
	/// This will never be the empty string.
	/// </remarks>
	string? AssetBundleName { get; set; }
	/// <summary>
	/// 此对象关联的主要资产，如果有的话。
	/// </summary>
	IUnityObjectBase? MainAsset { get; set; }

	/// <summary>
	/// 获取此对象的最佳目录，相对项目根目录。
	/// </summary>
	/// <remarks>
	/// In order of preference:<br/>
	/// 1. <see cref="OverrideDirectory"/><br/>
	/// 2. <see cref="OriginalDirectory"/><br/>
	/// 3. <see cref="ClassName"/>
	/// </remarks>
	/// <returns>A non-empty string.</returns>
	public sealed string GetBestDirectory()
	{
		if (OverrideDirectory is not null || OverrideName is not null)
		{
			return OverrideDirectory ?? "Assets";
		}
		else if (OriginalDirectory is not null || OriginalName is not null)
		{
			return OriginalDirectory ?? "Assets";
		}
		else
		{
			return "Assets/" + ClassName;
		}
	}

	/// <summary>
	/// 获取此对象的最佳名称。
	/// </summary>
	/// <remarks>
	/// In order of preference:<br/>
	/// 1. <see cref="OverrideName"/><br/>
	/// 2. <see cref="INamed.Name"/><br/>
	/// 3. <see cref="OriginalName"/><br/>
	/// 4. <see cref="ClassName"/><br/>
	/// <see cref="OriginalName"/> has secondary preference because file importers can create assets with a different name from the file.
	/// </remarks>
	/// <returns>A nonempty string.</returns>
	public sealed string GetBestName()
	{
		if (OverrideName is not null)
		{
			return OverrideName;
		}
		Utf8String? name = (this as INamed)?.Name;
		if (!Utf8String.IsNullOrEmpty(name))
		{
			return name;
		}
		else if (OriginalName is not null)
		{
			return OriginalName;
		}
		else
		{
			return ClassName;
		}
	}

	/// <summary>
	/// 获取此对象的最佳扩展名，如果存在的话。
	/// </summary>
	/// <remarks>
	/// In order of preference:<br/>
	/// 1. <see cref="OverrideExtension"/><br/>
	/// 2. <see cref="OriginalExtension"/>
	/// </remarks>
	/// <returns>A nonempty string or null.</returns>
	public sealed string? GetBestExtension() => OverrideExtension ?? OriginalExtension;

	public sealed void CopyValues(IUnityObjectBase? source)
	{
		if (source is null)
		{
			Reset();
		}
		else
		{
			CopyValues(source, new PPtrConverter(source, this));
		}
	}
}
public static class UnityObjectBaseExtensions
{
	public static void Read(this IUnityObjectBase asset, ref EndianSpanReader reader) => asset.Read(ref reader, asset.Collection.Flags);
}
