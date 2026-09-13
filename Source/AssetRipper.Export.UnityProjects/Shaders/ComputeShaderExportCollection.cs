using AssetRipper.Assets;
using AssetRipper.Assets.Metadata;
using AssetRipper.Primitives;
using AssetRipper.SourceGenerated;
using AssetRipper.SourceGenerated.Classes.ClassID_1008;
using AssetRipper.SourceGenerated.Classes.ClassID_72;

namespace AssetRipper.Export.UnityProjects.Shaders;

/// <summary>
/// ComputeShader 源码还原的导出集合：产物为 .compute 文本文件，meta 使用 ComputeShaderImporter。
/// </summary>
public sealed class ComputeShaderExportCollection : AssetExportCollection<IComputeShader>
{
	public ComputeShaderExportCollection(IAssetExporter assetExporter, IComputeShader asset) : base(assetExporter, asset)
	{
	}

	protected override string GetExportExtension(IUnityObjectBase asset) => "compute";

	protected override IUnityObjectBase CreateImporter(IExportContainer container)
	{
		// .compute 文件在 Unity 中由 ComputeShaderImporter 接管，meta 需要对应类型的 importer。
		// 与 ShaderImporter.Create 的惯例一致：pathID 用 0，仅用于 meta 序列化，不落盘。
		AssetInfo info = new(container.File, 0, (int)ClassIDType.ComputeShaderImporter);
		IComputeShaderImporter importer = CreateVersionedImporter(info, container.ExportVersion);
		if (importer.Has_AssetBundleName_R() && Asset.AssetBundleName is not null)
		{
			importer.AssetBundleName_R = Asset.AssetBundleName;
		}

		return importer;
	}

	/// <summary>
	/// 按导出版本选择匹配的 ComputeShaderImporter 生成类。
	/// 各版本序列化字段基本一致，此切换只为让 meta 字段形状与目标版本对齐。
	/// </summary>
	private static IComputeShaderImporter CreateVersionedImporter(AssetInfo info, UnityVersion version)
	{
		if (version.LessThan(4, 3))
		{
			return new ComputeShaderImporter_4(info);
		}
		if (version.LessThan(5))
		{
			return new ComputeShaderImporter_4_3(info);
		}
		if (version.LessThan(5, 3, 2))
		{
			return new ComputeShaderImporter_5(info);
		}
		if (version.LessThan(5, 5))
		{
			return new ComputeShaderImporter_5_3_2(info);
		}
		if (version.LessThan(2017))
		{
			return new ComputeShaderImporter_5_5(info);
		}
		if (version.LessThan(2017, 2))
		{
			return new ComputeShaderImporter_2017(info);
		}
		if (version.LessThan(2017, 3))
		{
			return new ComputeShaderImporter_2017_2(info);
		}
		if (version.LessThan(2018, 2))
		{
			return new ComputeShaderImporter_2017_3(info);
		}
		if (version.LessThan(2018, 3))
		{
			return new ComputeShaderImporter_2018_2(info);
		}
		if (version.LessThan(2019))
		{
			return new ComputeShaderImporter_2018_3(info);
		}
		if (version.LessThan(2019, 1, 4))
		{
			return new ComputeShaderImporter_2019(info);
		}
		if (version.LessThan(2019, 4, 27))
		{
			return new ComputeShaderImporter_2019_1_4(info);
		}
		if (version.LessThan(2020))
		{
			return new ComputeShaderImporter_2019_4_27(info);
		}
		if (version.LessThan(2020, 2))
		{
			return new ComputeShaderImporter_2020(info);
		}
		if (version.LessThan(2020, 3, 4))
		{
			return new ComputeShaderImporter_2020_2(info);
		}
		if (version.LessThan(2021))
		{
			return new ComputeShaderImporter_2020_3_4(info);
		}
		if (version.LessThan(2021, 1, 1))
		{
			return new ComputeShaderImporter_2021(info);
		}
		if (version.LessThan(2022, 1, 0, UnityVersionType.Alpha, 10))
		{
			return new ComputeShaderImporter_2021_1_1(info);
		}

		return new ComputeShaderImporter_2022_1_0_a10(info);
	}
}
