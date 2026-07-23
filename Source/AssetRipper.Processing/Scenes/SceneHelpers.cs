using AssetRipper.Assets;
using AssetRipper.Assets.Collections;
using AssetRipper.SourceGenerated.Classes.ClassID_1;
using AssetRipper.SourceGenerated.Classes.ClassID_1001;
using AssetRipper.SourceGenerated.Classes.ClassID_114;
using AssetRipper.SourceGenerated.Classes.ClassID_141;
using AssetRipper.SourceGenerated.Classes.ClassID_2;
using AssetRipper.SourceGenerated.Classes.ClassID_3;
using AssetRipper.SourceGenerated.Extensions;
using System.Text.RegularExpressions;

namespace AssetRipper.Processing.Scenes;

public static partial class SceneHelpers
{
	private const string AssetsName = "Assets/";
	private const string LibaryPackageCacheName = "Library/PackageCache/";
	private const string LevelName = "level";
	private const string MainSceneName = "maindata";

	public static bool TryGetFileNameToSceneIndex(string name, UnityVersion version, out int index)
	{
		if (HasMainData(version))
		{
			if (name == MainSceneName)
			{
				index = 0;
				return true;
			}

			if (SceneNameFormat.IsMatch(name))
			{
				index = int.Parse(name.AsSpan(LevelName.Length)) + 1;
				return true;
			}
		}
		else
		{
			if (SceneNameFormat.IsMatch(name))
			{
				index = int.Parse(name.AsSpan(LevelName.Length));
				return true;
			}
		}

		index = -1;
		return false;
	}

	/// <summary>
	/// Less than 5.3.0
	/// </summary>
	public static bool HasMainData(UnityVersion version) => version.LessThan(5, 3);

	/// <summary>
	/// GameObjects, Classes inheriting from LevelGameManager, MonoBehaviours with GameObjects, Components, and PrefabInstances
	/// </summary>
	public static bool IsSceneCompatible(IUnityObjectBase asset)
	{
		return asset switch
		{
			IGameObject => true,
			ILevelGameManager => true,
			IMonoBehaviour monoBeh => monoBeh.IsComponentOnGameObject(),
			IComponent => true,
			IPrefabInstance => true,
			_ => false,
		};
	}

	public static string SceneIndexToFileName(int index, UnityVersion version)
	{
		if (HasMainData(version))
		{
			if (index == 0)
			{
				return MainSceneName;
			}
			return $"{LevelName}{index - 1}";
		}
		return $"{LevelName}{index}";
	}

	/// <summary>
	/// 尝试从构建设置中获取场景路径。
	/// </summary>
	/// <param name="collection"></param>
	/// <param name="buildSettings"></param>
	/// <param name="result"></param>
	/// <returns></returns>
	public static bool TryGetScenePath(AssetCollection collection, [NotNullWhen(true)] IBuildSettings? buildSettings, [NotNullWhen(true)] out string? result)
	{
		if (buildSettings is not null && TryGetFileNameToSceneIndex(collection.Name, collection.OriginalVersion, out int index))
		{
			if (index >= buildSettings.Scenes.Count)
			{
				// 这种情况可能出现在以下情形中：
				// 1. 游戏包含 N 个场景，并发布到发行平台。
				// 2. 因各种原因，项目中的某个场景被删除。
				// 3. 游戏重新构建，使用更新后的场景列表。
				// 4. 在更新游戏时，开发者忘记删除第 N 个场景文件。
				// 5. 此时 BuildSettings 中显示 N-1 个场景，但 AssetRipper 需要查找 N 个场景文件。
				result = null;
				return false;
			}
			string scenePath = buildSettings.Scenes[index].String;
			string extension = Path.GetExtension(scenePath);
			if (scenePath.StartsWith(AssetsName, StringComparison.Ordinal))
			{
				result = scenePath[..^extension.Length];
				return true;
			}
			else if (scenePath.StartsWith(LibaryPackageCacheName, StringComparison.Ordinal))
			{
				result = scenePath[..^extension.Length];
				return true;
			}
			else if (Path.IsPathRooted(scenePath))
			{
				// pull/uTiny 617
				// 注意：绝对项目路径中可能包含 Assets/，因此在这种情况下会得到错误的场景路径，但目前无法绕过此问题。
				int startIndex = scenePath.IndexOf(AssetsName);
				if (startIndex < 0)
				{
					startIndex = scenePath.IndexOf(LibaryPackageCacheName);
				}
				if (startIndex < 0)
				{
					result = null;
					return false;
				}
				result = scenePath[startIndex..^extension.Length];
				return true;
			}
			else if (scenePath.Length == 0)
			{
				// 如果游戏在没有包含场景的情况下构建，Unity 会创建一个名为空的场景。
				result = null;
				return false;
			}
			else
			{
				result = Path.Join("Assets", "Scenes", scenePath);
				return true;
			}
		}
		result = null;
		return false;
	}

	public static bool IsSceneDuplicate(int sceneIndex, IBuildSettings? buildSettings)
	{
		if (buildSettings == null)
		{
			return false;
		}

		string sceneName = buildSettings.Scenes[sceneIndex].String;
		for (int i = 0; i < buildSettings.Scenes.Count; i++)
		{
			if (buildSettings.Scenes[i] == sceneName)
			{
				if (i != sceneIndex)
				{
					return true;
				}
			}
		}
		return false;
	}

	[GeneratedRegex("^level(0|([1-9][0-9]*))$")]
	private static partial Regex SceneNameFormat { get; }
}
