using AssetRipper.Export.Configuration;
using AssetRipper.GUI.Web.Pages.Settings.DropDown;
using AssetRipper.GUI.Web.Paths;
using AssetRipper.Import.Configuration;
using AssetRipper.Primitives;
using Microsoft.AspNetCore.Http;

namespace AssetRipper.GUI.Web.Pages.Settings;

public sealed partial class SettingsPage : DefaultPage
{
	public static SettingsPage Instance { get; } = new();

	private static FullConfiguration Configuration => GameFileLoader.Settings;

	public override string GetTitle() => Localization.Settings;

	public override void WriteInnerContent(TextWriter writer)
	{
		new H1(writer).WithClass("text-center").Close(Localization.ConfigOptions);
		if (GameFileLoader.IsLoaded)
		{
			using (new Div(writer).WithClass("text-center").End())
			{
				new P(writer).Close(Localization.SettingsCanOnlyBeChangedBeforeLoadingFiles);
			}
		}
		else
		{
			using (new Form(writer).WithAction("/Settings/Update").WithMethod("post").End())
			{
				using (new Div(writer).WithClass("form-group").End())
				{
					using (new Div(writer).WithClass("border rounded p-3 m-2").End())
					{
						new H2(writer).Close(Localization.MenuImport);

						using (new Div(writer).End())
						{
							using (new Div(writer).WithClass("row").End())
							{
								using (new Div(writer).WithClass("col").End())
								{
									WriteCheckBoxForIgnoreStreamingAssets(writer, Localization.SkipStreamingAssets);
								}
								using (new Div(writer).WithClass("col").End())
								{
									WriteCheckBoxForEnableStaticMeshSeparation(writer, Localization.EnableStaticMeshSeparation, !GameFileLoader.Premium);
								}
							}

							using (new Div(writer).WithClass("row").End())
							{
								using (new Div(writer).WithClass("col").End())
								{
									WriteCheckBoxForRemoveNullableAttributes(writer, Localization.RemoveNullableAttributes);
								}
								using (new Div(writer).WithClass("col").End())
								{
									WriteCheckBoxForPublicizeAssemblies(writer, Localization.PublicizeAssemblies);
								}
							}

						using (new Div(writer).WithClass("row").End())
						{
							using (new Div(writer).WithClass("col").End())
							{
								WriteTextAreaForDefaultVersion(writer);
							}
						}

						// 游戏类型下拉选择，用于针对特定游戏启用专属解析方式
						using (new Div(writer).WithClass("row").End())
						{
							using (new Div(writer).WithClass("col").End())
							{
								WriteDropDownForGameType(writer);
							}
						}

						// 依赖关系文件：勾选后加载时解析打开文件夹之外的依赖，路径指向扫描命令生成的文件
						using (new Div(writer).WithClass("row").End())
						{
							using (new Div(writer).WithClass("col").End())
							{
								WriteCheckBoxForLoadDependencyMap(writer, Localization.LoadDependencyMap);
							}
						}

						// 导入类型白名单：只解析/导出选中的资源大类，未选中的类型在加载阶段直接跳过
						using (new Div(writer).WithClass("row").End())
						{
							using (new Div(writer).WithClass("col").End())
							{
								WriteCheckBoxForEnableImportAssetTypeFilter(writer, Localization.EnableImportAssetTypeFilter);
								WriteCheckBoxGroupForImportAssetTypes(writer);
							}
						}

						using (new Div(writer).WithClass("row").End())
						{
							using (new Div(writer).WithClass("col").End())
							{
								WriteTextInputForDependencyMapPath(writer);
							}
						}

						// IL2Cpp dump 目录：Cpp2IL 无法解析（如加密游戏）时，改为加载外部工具导出的 DummyDll
						using (new Div(writer).WithClass("row").End())
						{
							using (new Div(writer).WithClass("col").End())
							{
								WriteTextInputForIl2CppDumpPath(writer);
							}
						}

						// 文件扫描缓存：大型项目（数十万文件）重复导入时复用上一次的扫描结果
						using (new Div(writer).WithClass("row").End())
						{
							using (new Div(writer).WithClass("col").End())
							{
								WriteCheckBoxForEnableFileScanCache(writer, Localization.EnableFileScanCache);
								new P(writer).WithClass("form-text").Close(Localization.EnableFileScanCacheDescription);
							}
						}

						using (new Div(writer).WithClass("row").End())
						{
							using (new Div(writer).WithClass("col").End())
							{
								WriteTextInputForFileScanCachePath(writer);
							}
						}

						// 内存拆解口径：控制加载/处理阶段资源占用拆解日志的统计方式
						using (new Div(writer).WithClass("row").End())
						{
							using (new Div(writer).WithClass("col").End())
							{
								WriteDropDownForMemoryBreakdownMode(writer);
							}
						}

						using (new Div(writer).WithClass("row").End())
						{
							using (new Div(writer).WithClass("col").End())
							{
								WriteDropDownForBundledAssetsExportMode(writer);
							}
								using (new Div(writer).WithClass("col").End())
								{
									WriteDropDownForScriptContentLevel(writer);
								}
							}
						}

						new Hr(writer).Close();

						using (new Div(writer).End())
						{
							new H3(writer).Close(Localization.Experimental);

							using (new Div(writer).WithClass("row").End())
							{
								using (new Div(writer).WithClass("col").End())
								{
									WriteCheckBoxForEnablePrefabOutlining(writer, Localization.EnablePrefabOutlining);
								}
								using (new Div(writer).WithClass("col").End())
								{
									WriteCheckBoxForEnableAssetDeduplication(writer, Localization.EnableAssetDeduplication);
								}
								using (new Div(writer).WithClass("col").End())
								{
									WriteCheckBoxForEnableDeterministicGuids(writer, Localization.EnableDeterministicGuids);
								}
							}

							using (new Div(writer).WithClass("row").End())
							{
								using (new Div(writer).WithClass("col").End())
								{
									WriteTextAreaForTargetVersion(writer);
								}
							}
						}
					}

					using (new Div(writer).WithClass("border rounded p-3 m-2").End())
					{
						new H2(writer).Close(Localization.MenuExport);

						using (new Div(writer).End())
						{
							using (new Div(writer).WithClass("row").End())
							{
								using (new Div(writer).WithClass("col").End())
								{
									WriteDropDownForAudioExportFormat(writer);
								}
								using (new Div(writer).WithClass("col").End())
								{
									WriteDropDownForImageExportFormat(writer);
								}
								using (new Div(writer).WithClass("col").End())
								{
									WriteDropDownForLightmapTextureExportFormat(writer);
								}
							}

							using (new Div(writer).WithClass("row").End())
							{
								using (new Div(writer).WithClass("col").End())
								{
									WriteDropDownForSpriteExportMode(writer);
								}
								using (new Div(writer).WithClass("col").End())
								{
									WriteDropDownForShaderExportMode(writer);
								}
								using (new Div(writer).WithClass("col").End())
								{
									WriteDropDownForTextExportMode(writer);
								}
							}

							using (new Div(writer).WithClass("row").End())
							{
								using (new Div(writer).WithClass("col").End())
								{
									WriteDropDownForScriptLanguageVersion(writer);
								}
								using (new Div(writer).WithClass("col").End())
								{
									WriteDropDownForScriptExportMode(writer);
								}
								using (new Div(writer).WithClass("col").End())
								{
									WriteDropDownForComputeShaderExportMode(writer);
								}
							}

							using (new Div(writer).WithClass("row").End())
							{
								using (new Div(writer).WithClass("col").End())
								{
									WriteCheckBoxForScriptTypesFullyQualified(writer, Localization.ScriptsUseFullyQualifiedTypeNames);
								}
								using (new Div(writer).WithClass("col").End())
								{
									WriteCheckBoxForSaveSettingsToDisk(writer, Localization.SaveSettingsToDisk);
								}
								using (new Div(writer).WithClass("col").End())
								{
									WriteCheckBoxForExportUnreadableAssets(writer, Localization.ExportUnreadableAssets);
								}
							}

							using (new Div(writer).WithClass("row").End())
							{
								using (new Div(writer).WithClass("col").End())
								{
									WriteCheckBoxForStripTexture2DData(writer, Localization.StripTexture2dData);
								}
								using (new Div(writer).WithClass("col").End())
								{
									WriteCheckBoxForStripMeshData(writer, Localization.StripMeshData);
								}
								using (new Div(writer).WithClass("col").End())
								{
									WriteCheckBoxForStripAudioClipData(writer, Localization.StripAudioClipData);
								}
								using (new Div(writer).WithClass("col").End())
								{
									WriteCheckBoxForStripMonoBehaviourData(writer, Localization.StripMonoBehaviourData);
								}
							}

							using (new Div(writer).WithClass("row").End())
							{
								using (new Div(writer).WithClass("col").End())
								{
									WriteTextInputForCustomProjectPath(writer);
								}
							}
						}
					}

					using (new Div(writer).WithClass("text-center").End())
					{
						new Input(writer).WithType("submit").WithValue(Localization.Save).Close();
					}
				}
			}
		}
	}

	private static void WriteTextAreaForDefaultVersion(TextWriter writer)
	{
		new Label(writer).WithClass("form-label").WithFor(nameof(Configuration.ImportSettings.DefaultVersion)).Close(Localization.DefaultVersion);
		new Input(writer)
			.WithType("text")
			.WithClass("form-control")
			.WithId(nameof(Configuration.ImportSettings.DefaultVersion))
			.WithName(nameof(Configuration.ImportSettings.DefaultVersion))
			.WithValue(Configuration.ImportSettings.DefaultVersion.ToString())
			.Close();
	}

	private static void WriteTextAreaForTargetVersion(TextWriter writer)
	{
		new Label(writer).WithClass("form-label").WithFor(nameof(Configuration.ImportSettings.TargetVersion)).Close(Localization.TargetVersionForVersionChanging);
		new Input(writer)
			.WithType("text")
			.WithClass("form-control")
			.WithId(nameof(Configuration.ImportSettings.TargetVersion))
			.WithName(nameof(Configuration.ImportSettings.TargetVersion))
			.WithValue(Configuration.ImportSettings.TargetVersion.ToString())
			.Close();
	}

	/// <summary>
	/// 自定义 Unity 项目路径的文本输入框，附带"选择文件夹"按钮（调用全局 JS 打开本机目录选择对话框）。
	/// </summary>
	private static void WriteTextInputForCustomProjectPath(TextWriter writer)
	{
		string id = nameof(Configuration.ExportSettings.CustomProjectPath);
		new Label(writer).WithClass("form-label").WithFor(id).Close(Localization.CustomProjectPath);
		using (new Div(writer).WithClass("input-group").End())
		{
			new Input(writer)
				.WithType("text")
				.WithClass("form-control")
				.WithId(id)
				.WithName(id)
				.WithValue(Configuration.ExportSettings.CustomProjectPath ?? "")
				.Close();
			new Button(writer)
				.WithType("button")
				.WithClass("btn btn-outline-secondary")
				.WithCustomAttribute("onclick", $"browseForFolder('{id}')")
				.Close(Localization.SelectFolder);
		}
	}

	/// <summary>
	/// 依赖关系文件路径的文本输入框，附带"选择文件"按钮（调用全局 JS 打开本机文件选择对话框）。
	/// </summary>
	private static void WriteTextInputForDependencyMapPath(TextWriter writer)
	{
		string id = nameof(Configuration.ImportSettings.DependencyMapPath);
		new Label(writer).WithClass("form-label").WithFor(id).Close(Localization.DependencyMapPath);
		using (new Div(writer).WithClass("input-group").End())
		{
			new Input(writer)
				.WithType("text")
				.WithClass("form-control")
				.WithId(id)
				.WithName(id)
				.WithValue(Configuration.ImportSettings.DependencyMapPath ?? "")
				.Close();
			new Button(writer)
				.WithType("button")
				.WithClass("btn btn-outline-secondary")
				.WithCustomAttribute("onclick", $"browseForFile('{id}')")
				.Close(Localization.SelectFile);
		}
	}

	/// <summary>
	/// IL2Cpp dump 目录的文本输入框，附带"选择文件夹"按钮（调用全局 JS 打开本机目录选择对话框）。
	/// 指向 Il2CppDumper 输出目录（自动识别其中的 DummyDll 子目录），用于加密游戏等 Cpp2IL 无法解析的场景。
	/// </summary>
	private static void WriteTextInputForIl2CppDumpPath(TextWriter writer)
	{
		string id = nameof(Configuration.ImportSettings.Il2CppDumpPath);
		new Label(writer).WithClass("form-label").WithFor(id).Close(Localization.Il2cppDumpPath);
		using (new Div(writer).WithClass("input-group").End())
		{
			new Input(writer)
				.WithType("text")
				.WithClass("form-control")
				.WithId(id)
				.WithName(id)
				.WithValue(Configuration.ImportSettings.Il2CppDumpPath ?? "")
				.Close();
			new Button(writer)
				.WithType("button")
				.WithClass("btn btn-outline-secondary")
				.WithCustomAttribute("onclick", $"browseForFolder('{id}')")
				.Close(Localization.SelectFolder);
		}
	}

	/// <summary>
	/// 文件扫描缓存文件的路径输入框，附带"选择文件"按钮。
	/// 仅当启用扫描缓存时才会读写该文件，留空则视为不启用。
	/// </summary>
	private static void WriteTextInputForFileScanCachePath(TextWriter writer)
	{
		string id = nameof(Configuration.ImportSettings.FileScanCachePath);
		new Label(writer).WithClass("form-label").WithFor(id).Close(Localization.FileScanCachePath);
		using (new Div(writer).WithClass("input-group").End())
		{
			new Input(writer)
				.WithType("text")
				.WithClass("form-control")
				.WithId(id)
				.WithName(id)
				.WithValue(Configuration.ImportSettings.FileScanCachePath ?? "")
				.Close();
			new Button(writer)
				.WithType("button")
				.WithClass("btn btn-outline-secondary")
				.WithCustomAttribute("onclick", $"browseForFile('{id}')")
				.Close(Localization.SelectFile);
		}
	}

	private static void WriteCheckBox(TextWriter writer, string label, bool @checked, string id, bool disabled = false)
	{
		using (new Div(writer).WithClass("form-check").End())
		{
			new Input(writer)
				.WithClass("form-check-input")
				.WithType("checkbox")
				.WithValue()
				.WithId(id)
				.WithName(id)
				.MaybeWithChecked(disabled ? false : @checked)
				.MaybeWithDisabled(disabled)
				.Close();
			new Label(writer)
				.WithClass("form-check-label" + (disabled ? " text-muted" : ""))
				.WithFor(id)
				.Close(label + (disabled ? $" ({Localization.PremiumFeatureNotice})" : ""));
		}
	}

	/// <summary>
	/// 导入类型多选控件：一组同名复选框，外加「全选 / 全不选」快捷按钮。
	/// </summary>
	/// <remarks>
	/// 所有复选框共用 <see cref="ImportAssetTypesFieldName"/> 作为 name，
	/// 由 <see cref="ApplyImportAssetTypes"/> 在提交时整体读出，因此这里只需保证
	/// value 使用枚举名（而非本地化文案），否则切换语言会让已保存的选择失效。
	/// id 仍按类型名唯一化，以便 label 的 for 属性正确关联。
	/// </remarks>
	private static void WriteCheckBoxGroupForImportAssetTypes(TextWriter writer)
	{
		new Label(writer).WithClass("form-label").Close(Localization.ImportAssetTypesTitle);
		new P(writer).WithClass("form-text").Close(Localization.ImportAssetTypesDescription);

		using (new Div(writer).WithClass("mb-2").End())
		{
			new Button(writer)
				.WithType("button")
				.WithClass("btn btn-sm btn-outline-secondary me-2")
				.WithCustomAttribute("onclick", $"setCheckBoxGroup('{ImportAssetTypesFieldName}', true)")
				.Close(Localization.SelectAll);
			new Button(writer)
				.WithType("button")
				.WithClass("btn btn-sm btn-outline-secondary")
				.WithCustomAttribute("onclick", $"setCheckBoxGroup('{ImportAssetTypesFieldName}', false)")
				.Close(Localization.ClearAll);
		}

		// 三列排布，避免十几个选项把设置页拉得过长
		using (new Div(writer).WithClass("row row-cols-3 g-1").End())
		{
			foreach (ImportAssetType type in Enum.GetValues<ImportAssetType>())
			{
				bool isChecked = Configuration.ImportSettings.ImportAssetTypes.Contains(type);
				using (new Div(writer).WithClass("col").End())
				{
					WriteImportAssetTypeCheckBox(writer, type, isChecked);
				}
			}
		}
	}

	/// <summary>
	/// 单个导入类型复选框。name 固定为字段名、value 为类型枚举名，供后端整体解析。
	/// </summary>
	private static void WriteImportAssetTypeCheckBox(TextWriter writer, ImportAssetType type, bool @checked)
	{
		string id = $"{ImportAssetTypesFieldName}_{type}";
		using (new Div(writer).WithClass("form-check").End())
		{
			new Input(writer)
				.WithClass("form-check-input")
				.WithType("checkbox")
				.WithValue(type.ToString())
				.WithId(id)
				.WithName(ImportAssetTypesFieldName)
				.MaybeWithChecked(@checked)
				.Close();
			new Label(writer).WithClass("form-check-label").WithFor(id).Close(GetImportAssetTypeDisplayName(type));
		}
	}

	/// <summary>
	/// 取导入类型的本地化显示名；未登记文案时回退到枚举名，保证新增类型不会空白。
	/// </summary>
	private static string GetImportAssetTypeDisplayName(ImportAssetType type) => type switch
	{
		ImportAssetType.Mesh => Localization.ImportAssetTypeMesh,
		ImportAssetType.Texture => Localization.ImportAssetTypeTexture,
		ImportAssetType.Material => Localization.ImportAssetTypeMaterial,
		ImportAssetType.Shader => Localization.ImportAssetTypeShader,
		ImportAssetType.AnimationClip => Localization.ImportAssetTypeAnimationClip,
		ImportAssetType.AnimatorController => Localization.ImportAssetTypeAnimatorController,
		ImportAssetType.AudioClip => Localization.ImportAssetTypeAudioClip,
		ImportAssetType.Font => Localization.ImportAssetTypeFont,
		ImportAssetType.TextAsset => Localization.ImportAssetTypeTextAsset,
		ImportAssetType.Sprite => Localization.ImportAssetTypeSprite,
		ImportAssetType.MonoBehaviour => Localization.ImportAssetTypeMonoBehaviour,
		ImportAssetType.ScriptableObject => Localization.ImportAssetTypeScriptableObject,
		ImportAssetType.GameObject => Localization.ImportAssetTypeGameObject,
		ImportAssetType.VideoClip => Localization.ImportAssetTypeVideoClip,
		_ => type.ToString(),
	};

	private static void WriteDropDown<T>(TextWriter writer, DropDownSetting<T> setting, T value, string id) where T : struct, Enum
	{
		IReadOnlyList<DropDownItem<T>> items = setting.GetValues();
		new Label(writer).WithClass("form-label").WithFor(id).Close(setting.Title);
		using (new Select(writer).WithClass("form-select").WithName(id).End())
		{
			for (int i = 0; i < items.Count; i++)
			{
				DropDownItem<T> item = items[i];
				new Option(writer)
					.WithValue(item.Value.ToString().ToHtml())
					.MaybeWithSelected(EqualityComparer<T>.Default.Equals(item.Value, value))
					.WithCustomAttribute("option-description", CreateUniqueID(id, i))
					.Close(item.DisplayName);
			}
		}

		for (int i = 0; i < items.Count; i++)
		{
			DropDownItem<T> item = items[i];
			new P(writer)
				.WithClass("dropdown-description")//Used for CSS selecting
				.WithId(CreateUniqueID(id, i))
				.Close(item.Description);
		}

		static string CreateUniqueID(string selectID, int index)
		{
			return $"{selectID}_description_{index}";
		}
	}

	private static UnityVersion TryParseUnityVersion(string? version)
	{
		if (string.IsNullOrEmpty(version))
		{
			return default;
		}
		try
		{
			return UnityVersion.Parse(version);
		}
		catch
		{
			return default;
		}
	}

	private static T TryParseEnum<T>(string? s) where T : struct, Enum
	{
		if (Enum.TryParse(s, out T result))
		{
			return result;
		}
		return default;
	}

	public static Task HandlePostRequest(HttpContext context)
	{
		IFormCollection form = context.Request.Form;
		foreach ((string key, Action<bool> action) in booleanProperties)
		{
			action.Invoke(form.ContainsKey(key));
		}
		foreach ((string key, string? value) in form.Select(pair => (pair.Key, (string?)pair.Value)))
		{
			SetProperty(key, value);
		}

		// 多选类型白名单：同名前缀的一组复选框，值为类型名。
		// 不能放进上面的循环，因为多选需要整体覆盖集合，
		// 且单个键的解析会与 checkbox 的 value 语义冲突。
		ApplyImportAssetTypes(form);

		if (Configuration.SaveSettingsToDisk)
		{
			Configuration.SaveToDefaultPath();
		}
		else
		{
			SerializedSettings.DeleteDefaultPath();
		}

		context.Response.Redirect("/Settings/Edit");
		return Task.CompletedTask;
	}

	/// <summary>
	/// 从表单读取多选的导入类型复选框，整体覆盖 <see cref="ImportSettings.ImportAssetTypes"/>。
	/// </summary>
	/// <remarks>
	/// 这里刻意不做增量合并：多选控件的语义就是「表单里勾了哪些，当前就是哪些」，
	/// 增量合并会让取消勾选的项无法被移除。空结果同样写入（表示不限制类型），
	/// 是否真正启用过滤由 <see cref="ImportSettings.EnableImportAssetTypeFilter"/> 决定。
	/// </remarks>
	private static void ApplyImportAssetTypes(IFormCollection form)
	{
		HashSet<ImportAssetType> selected = new();
		foreach (string? value in form[ImportAssetTypesFieldName])
		{
			// 选项值用枚举名，避免本地化文案变动导致解析失效。
			if (Enum.TryParse(value, out ImportAssetType type))
			{
				selected.Add(type);
			}
		}

		Configuration.ImportSettings.ImportAssetTypes = selected;
	}

	/// <summary>
	/// 类型复选框的字段名。用固定名字而非枚举名，是为了让所有选项共用同一组表单键。
	/// </summary>
	public const string ImportAssetTypesFieldName = nameof(ImportSettings.ImportAssetTypes);
}
