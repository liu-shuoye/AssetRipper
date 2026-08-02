using System.Text.Json.Serialization;

namespace AssetRipper.Export.Configuration;

/// <summary>
/// 为 <see cref="LastExportSettings"/> 提供 AOT 友好的源生成序列化上下文。
/// GUI.Free 启用了 PublishAot，必须使用源生成而非运行时反射。
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(LastExportSettings))]
internal sealed partial class LastExportSettingsContext : JsonSerializerContext
{
}
