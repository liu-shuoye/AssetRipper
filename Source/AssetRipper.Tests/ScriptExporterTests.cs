using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Export.UnityProjects;
using AssetRipper.Export.UnityProjects.Scripts;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.IO.Files;
using AssetRipper.Primitives;
using AssetRipper.Processing;
using AssetRipper.SourceGenerated.Classes.ClassID_115;
using AssetRipper.SourceGenerated.Extensions;

namespace AssetRipper.Tests;

internal class ScriptExporterTests
{
	[TestCase("Unity.RenderPipelines.Core.Runtime")]
	[TestCase("Unity.VisualEffectGraph.Runtime")]
	[TestCase("UnityEngine.UI")]
	public static void KnownUpmAssemblyReturnsTrue(string assemblyName)
	{
		bool result = UnityPackageAssemblyMap.TryGetInfo(assemblyName, out UnityPackageAssemblyInfo info);
		Assert.That(result, Is.True);
		Assert.That(info.Guid, Is.Not.EqualTo(default(UnityGuid)));
		Assert.That(info.PackageName, Is.Not.Empty);
		Assert.That(info.Version, Is.Not.Empty);
	}

	[TestCase("MyCompany.Custom")]
	[TestCase("Assembly-CSharp")]
	[TestCase("NonExistent.Assembly")]
	public static void UnknownAssemblyReturnsFalse(string assemblyName)
	{
		bool result = UnityPackageAssemblyMap.TryGetInfo(assemblyName, out _);
		Assert.That(result, Is.False);
	}

	/// <summary>
	/// 验证 UPM 包脚本的 .cs.meta GUID 能被正确查询。
	/// Text.cs.meta 的 GUID 是 5f7201a12d95ffc409449d95f23cf332，
	/// 这是 Unity 预制体中 MonoBehaviour 引用 Text 脚本时使用的真实 GUID。
	/// 用 UnityGuid.Parse 构造期望值，确保 ToString() 输出与原始 GUID 字符串一致。
	/// </summary>
	[Test]
	public static void TryGetScriptGuidReturnsTrueForKnownScript()
	{
		bool result = UnityPackageAssemblyMap.TryGetScriptGuid("UnityEngine.UI", "UnityEngine.UI", "Text", out UnityGuid guid);
		Assert.That(result, Is.True);
		Assert.That(guid, Is.EqualTo(UnityGuid.Parse("5f7201a12d95ffc409449d95f23cf332")));
	}

	/// <summary>
	/// 验证 Image 脚本的 .cs.meta GUID。
	/// Image.cs.meta 的 GUID 是 fe87c0e1cc204ed48ad3b37840f39efc。
	/// </summary>
	[Test]
	public static void TryGetScriptGuidReturnsTrueForImage()
	{
		bool result = UnityPackageAssemblyMap.TryGetScriptGuid("UnityEngine.UI", "UnityEngine.UI", "Image", out UnityGuid guid);
		Assert.That(result, Is.True);
		Assert.That(guid, Is.EqualTo(UnityGuid.Parse("fe87c0e1cc204ed48ad3b37840f39efc")));
	}

	/// <summary>
	/// 验证未知的脚本组合返回 false。
	/// </summary>
	[Test]
	public static void TryGetScriptGuidReturnsFalseForUnknownScript()
	{
		bool result = UnityPackageAssemblyMap.TryGetScriptGuid("Unknown.Asm", "Unknown.Ns", "UnknownClass", out _);
		Assert.That(result, Is.False);
	}

	/// <summary>
	/// 采集到 .cs.meta 原始 GUID 的脚本必须返回该 GUID，而不是历史哈希值。
	/// Text.cs.meta 的 GUID 为 5f7201a12d95ffc409449d95f23cf332。
	/// </summary>
	[Test]
	public static void ResolveScriptGuidReturnsCollectedGuidForKnownScript()
	{
		IMonoScript script = CreateScript("UnityEngine.UI", "UnityEngine.UI", "Text");
		Assert.That(ScriptExporter.ResolveScriptGuid(script), Is.EqualTo(UnityGuid.Parse("5f7201a12d95ffc409449d95f23cf332")));
	}

	/// <summary>
	/// 未采集到 GUID 的脚本必须回退到历史哈希算法，保证既有工程的导出结果不发生变化。
	/// </summary>
	[Test]
	public static void ResolveScriptGuidFallsBackToHashForUnknownScript()
	{
		IMonoScript script = CreateScript("Unknown.Asm", "Unknown.Ns", "UnknownClass");
		Assert.That(ScriptExporter.ResolveScriptGuid(script), Is.EqualTo(ScriptHashing.CalculateScriptGuid(script)));
	}

	/// <summary>
	/// 采集到原始 GUID 的脚本，导出出的 .cs.meta 必须使用同一个 GUID，
	/// 否则引用指针（CreateExportPointer）会指向一个不存在的资源。
	/// </summary>
	[Test]
	public static void ExportedScriptMetaUsesCollectedGuid()
	{
		ProcessedAssetCollection collection = AssetCreator.CreateCollection(UnityVersion.V_2022);
		IMonoScript script = CreateScript(collection, "UnityEngine.UI", "UnityEngine.UI", "Text");
		var monoBehaviour = collection.CreateMonoBehaviour();
		monoBehaviour.ScriptP = script;
		monoBehaviour.Name = "TestBehaviour";

		VirtualFileSystem fileSystem = Export(collection);
		const string MetaPath = "/output/ExportedProject/Assets/Scripts/UnityEngine.UI/UnityEngine/UI/Text.cs.meta";
		Assert.That(fileSystem.File.Exists(MetaPath), Is.True, "脚本 .cs.meta 未被导出");
		Assert.That(fileSystem.File.ReadAllText(MetaPath), Does.Contain("5f7201a12d95ffc409449d95f23cf332"));
		Assert.That(fileSystem.File.ReadAllText("/output/ExportedProject/Assets/MonoBehaviour/TestBehaviour.asset"),
			Does.Contain("guid: 5f7201a12d95ffc409449d95f23cf332"));
	}

	private static IMonoScript CreateScript(string assemblyName, string @namespace, string className)
	{
		return CreateScript(AssetCreator.CreateCollection(UnityVersion.V_2022), assemblyName, @namespace, className);
	}

	private static IMonoScript CreateScript(ProcessedAssetCollection collection, string assemblyName, string @namespace, string className)
	{
		IMonoScript script = collection.CreateMonoScript();
		script.AssemblyName = assemblyName;
		script.Namespace = @namespace;
		script.ClassName_R = className;
		return script;
	}

	private static VirtualFileSystem Export(ProcessedAssetCollection collection)
	{
		VirtualFileSystem fileSystem = new();
		GameData gameData = new((GameBundle)collection.Bundle, collection.Version, new BaseManager(_ => { }), null);
		new ExportHandler(new()).Export(gameData, "output", fileSystem);
		return fileSystem;
	}
}
