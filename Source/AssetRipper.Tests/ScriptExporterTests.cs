using AssetRipper.Export.UnityProjects.Scripts;
using AssetRipper.Primitives;

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
}
