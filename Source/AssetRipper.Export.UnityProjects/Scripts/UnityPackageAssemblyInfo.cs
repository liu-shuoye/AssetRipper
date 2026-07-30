using AssetRipper.Primitives;

namespace AssetRipper.Export.UnityProjects.Scripts;

/// <summary>
/// 表示一个 UPM 包程序集的元信息。
/// 用于让 AssetRipper 跳过 UPM 包程序集的导出，改由 Packages/manifest.json 引用对应包。
/// </summary>
/// <param name="PackageName">UPM 包名，例如 com.unity.mathematics，对应 manifest.json 中的 dependencies 键。</param>
/// <param name="Guid">该程序集 .asmdef.meta 中的真实 GUID，用于保证预制体引用与 Unity Package Manager 安装后的资产 GUID 一致。</param>
/// <param name="Version">UPM 包版本，例如 1.0.1，用于写入 manifest.json 的 dependencies 值。</param>
public record struct UnityPackageAssemblyInfo(string PackageName, UnityGuid Guid, string Version);
