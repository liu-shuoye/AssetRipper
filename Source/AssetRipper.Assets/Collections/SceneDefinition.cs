namespace AssetRipper.Assets.Collections;

public sealed class SceneDefinition
{
	private readonly List<AssetCollection> collections = new();

	private SceneDefinition()
	{
	}

	/// <summary>
	/// 根据给定的名称和GUID创建一个新的<see cref="SceneDefinition"/>。
	/// </summary>
	/// <param name="name">场景的名称。</param>
	/// <param name="guid">场景的预定义<see cref="UnityGuid"/>。如果为默认值，则分配一个随机值。</param>
	/// <returns></returns>
	public static SceneDefinition FromName(string name, UnityGuid guid = default)
	{
		return new()
		{
			Name = name,
			Path = $"Assets/Scenes/{name}",
			GUID = guid.IsZero ? UnityGuid.NewGuid() : guid,
		};
	}

	/// <summary>
	/// 根据给定的路径和GUID创建一个新的<see cref="SceneDefinition"/>。
	/// </summary>
	/// <param name="path">场景的相对路径。</param>
	/// <param name="guid">场景的预定义<see cref="UnityGuid"/>。如果为默认值，则分配一个随机值。</param>
	/// <returns></returns>
	public static SceneDefinition FromPath(string path, UnityGuid guid = default)
	{
		return new()
		{
			Name = System.IO.Path.GetFileName(path),
			Path = path,
			GUID = guid.IsZero ? UnityGuid.NewGuid() : guid,
		};
	}

	/// <summary>
	/// 场景的名称，不带任何文件扩展名。
	/// </summary>
	public required string Name { get; init; }

	/// <summary>
	/// 场景路径，不带任何文件扩展名，相对项目根目录。
	/// </summary>
	public required string Path { get; init; }

	/// <summary>
	/// 该场景的GUID。它用于场景的元数据文件。这将不会是<see cref="UnityGuid.Zero"/>。
	/// </summary>
	public required UnityGuid GUID { get; init; }

	/// <summary>
	/// 构成此场景的<see cref="AssetCollection"/>的所有<see cref="AssetCollection"/>。
	/// </summary>
	public IReadOnlyList<AssetCollection> Collections => collections;

	/// <summary>
	/// 构成此场景的<see cref="Collections"/>中的所有资产。
	/// </summary>
	public IEnumerable<IUnityObjectBase> Assets => collections.SelectMany(c => c);

	/// <summary>
	/// 将<see cref="AssetCollection"/>添加到此<see cref="SceneDefinition"/>并设置其<see cref="AssetCollection.Scene"/>属性。
	/// </summary>
	/// <param name="collection">要添加的集合。</param>
	public void AddCollection(AssetCollection collection)
	{
		ThrowIfAlreadyPartOfAScene(collection);
		collections.Add(collection);
		collection.Scene = this;
	}

	public void RemoveCollection(AssetCollection collection)
	{
		if (collections.Remove(collection))
		{
			collection.Scene = null;
		}
		else
		{
			throw new ArgumentException($"{collection} is not part of this scene.", nameof(collection));
		}
	}

	private static void ThrowIfAlreadyPartOfAScene(AssetCollection collection)
	{
		if (collection.Scene is not null)
		{
			throw new InvalidOperationException($"{collection} is already part of a scene.");
		}
	}

	public override string ToString() => Name;
}
