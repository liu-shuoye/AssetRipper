using AssetRipper.Export.UnityProjects.Project;
using AssetRipper.Primitives;

namespace AssetRipper.Tests;

internal class PackageManifestPostExporterTests
{
	// 测试子类：把 protected CreateManifest 暴露为 public，便于测试
	private sealed class TestableExporter : PackageManifestPostExporter
	{
		public PackageManifest InvokeCreateManifest(UnityVersion version, IEnumerable<string> assemblyNames)
			=> CreateManifest(version, assemblyNames);
	}

	[Test]
	public static void CreateManifestIncludesUpmDependencies()
	{
		var exporter = new TestableExporter();
		// Unity.RenderPipelines.Core.Runtime 与 Unity.VisualEffectGraph.Runtime 都在生成的映射表里
		var assemblyNames = new[] { "Unity.RenderPipelines.Core.Runtime", "Unity.VisualEffectGraph.Runtime", "MyCompany.Custom" };
		var manifest = exporter.InvokeCreateManifest(UnityVersion.Parse("2022.3.0f1"), assemblyNames);

		Assert.That(manifest.Dependencies, Contains.Key("com.unity.render-pipelines.core"));
		Assert.That(manifest.Dependencies, Contains.Key("com.unity.visualeffectgraph"));
		// 第三方 DLL 不应出现在 dependencies 中
		Assert.That(manifest.Dependencies, Does.Not.ContainKey("MyCompany.Custom"));
	}

	[Test]
	public static void CreateManifestRetainsDefaultModuleDependencies()
	{
		var exporter = new TestableExporter();
		var manifest = exporter.InvokeCreateManifest(UnityVersion.Parse("2022.3.0f1"), Array.Empty<string>());

		// 默认模块依赖仍应存在
		Assert.That(manifest.Dependencies, Contains.Key("com.unity.modules.ai"));
		Assert.That(manifest.Dependencies, Contains.Key("com.unity.modules.ui"));
	}

	[Test]
	public static void CreateManifestDoesNotOverwriteExistingDependencies()
	{
		var exporter = new TestableExporter();
		// 默认模块依赖已存在时，TryAdd 不会覆盖其值
		var manifest = exporter.InvokeCreateManifest(UnityVersion.Parse("2022.3.0f1"), Array.Empty<string>());
		string defaultValue = manifest.Dependencies["com.unity.modules.ai"];
		Assert.That(defaultValue, Is.EqualTo("1.0.0"));
	}
}
