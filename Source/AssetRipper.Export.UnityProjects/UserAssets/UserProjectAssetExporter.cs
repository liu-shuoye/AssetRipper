using AssetRipper.Assets;
using AssetRipper.Import.Logging;
using AssetRipper.SourceGenerated.Classes.ClassID_115;
using AssetRipper.SourceGenerated.Classes.ClassID_21;
using AssetRipper.SourceGenerated.Classes.ClassID_28;
using AssetRipper.SourceGenerated.Classes.ClassID_48;
using AssetRipper.SourceGenerated.Classes.ClassID_49;
using AssetRipper.SourceGenerated.Classes.ClassID_83;
using AssetRipper.SourceGenerated.Classes.ClassID_89;

namespace AssetRipper.Export.UnityProjects.UserAssets;

/// <summary>
/// 用用户项目中的源文件替换同类型同名的游戏资产。
/// </summary>
/// <remarks>
/// 命中后导出集合直接复制用户源文件与 .meta（保留用户 GUID），其他资产对它的引用指向用户 GUID。
/// 该导出器须以最高优先级注册（最后注册，位于导出器栈顶），使用户显式指定优先于引擎内置资产重定向。
/// </remarks>
public sealed class UserProjectAssetExporter : IAssetExporter
{
	private readonly UserAssetIndex index;

	public UserProjectAssetExporter(UserAssetIndex index)
	{
		this.index = index;
	}

	/// <summary>成功替换（复制）的用户资产数量，由导出集合在复制成功后回调累加。</summary>
	public int ReplacedCount { get; private set; }

	public bool TryCreateCollection(IUnityObjectBase asset, [NotNullWhen(true)] out IExportCollection? exportCollection)
	{
		exportCollection = null;

		// 仅处理独立主资产：排除一切子资产（Sprite、Prefab 内 GameObject 等），
		// 同时天然排除"带 Sprite 的 Texture2D"（其 MainAsset 为 SpriteInformationObject），
		// 因为用户 .meta 中的 sprite fileID 内表与游戏内引用无法对应。
		if (asset.MainAsset is not null)
		{
			return false;
		}

		UserAssetKind? kind = asset switch
		{
			// Cubemap 不能由普通图片源文件替换，须先于 ITexture2D 判断
			ICubemap => null,
			IShader => UserAssetKind.Shader,
			IMaterial => UserAssetKind.Material,
			IMonoScript => UserAssetKind.Script,
			ITexture2D => UserAssetKind.Texture,
			IAudioClip => UserAssetKind.Audio,
			ITextAsset => UserAssetKind.Text,
			_ => null,
		};
		if (kind is null)
		{
			return false;
		}

		// 上述六个类型都是 Unity 原生具名对象（INamed 的 m_Name）
		if (asset is not INamed named)
		{
			return false;
		}

		string name = named.Name.String;
		if (string.IsNullOrWhiteSpace(name))
		{
			return false;
		}
		name = name.Trim();

		if (!index.TryGet(kind.Value, name, out UserAssetEntry? entry))
		{
			return false;
		}

		if (entry.Consumed)
		{
			Logger.Warning(LogCategory.Export, $"资产 '{name}' 匹配的用户文件 '{entry.SourceFilePath}' 已用于替换同名资产，该资产将正常导出。");
			return false;
		}

		entry.Consumed = true;
		exportCollection = new UserAssetExportCollection(this, asset, entry);
		return true;
	}

	/// <summary>
	/// 由导出集合在复制成功后回调，用于统计替换数量。
	/// </summary>
	internal void NotifyReplaced()
	{
		ReplacedCount++;
	}

	public AssetType ToExportType(IUnityObjectBase asset) => AssetType.Meta;

	public bool ToUnknownExportType(Type type, out AssetType assetType)
	{
		// 不承接未知类型：返回 false 让栈中的其他导出器处理
		assetType = default;
		return false;
	}
}
