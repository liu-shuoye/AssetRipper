using AssetRipper.Import.Logging;
using AssetRipper.Import.Structure;
using AssetRipper.IO.Files;
using AssetRipper.NativeDialogs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace AssetRipper.GUI.Web.Pages;

public static class Commands
{
	private const string RootPath = "/";
	private const string CommandsPath = "/Commands";

	/// <summary>
	/// For documentation purposes
	/// </summary>
	/// <param name="Path">The file system path.</param>
	internal record PathFormData(string Path);

	internal static RouteHandlerBuilder AcceptsFormDataContainingPath(this RouteHandlerBuilder builder)
	{
		return builder.Accepts<PathFormData>("application/x-www-form-urlencoded");
	}

	private static bool TryGetCreateSubfolder(IFormCollection form)
	{
		if (form.TryGetValue("CreateSubfolder", out StringValues values))
		{
			return values == "true";
		}

		return false;
	}

	public readonly struct LoadFile : ICommand
	{
		static async Task<string?> ICommand.Execute(HttpRequest request)
		{
			IFormCollection form = await request.ReadFormAsync();

			string[]? paths;
			if (form.TryGetValue("Path", out StringValues values))
			{
				paths = values;
			}
			else if (NativeDialog.Supported)
			{
				paths = await OpenFileDialog.OpenFiles();
			}
			else
			{
				return CommandsPath;
			}

			if (paths is { Length: > 0 })
			{
				GameFileLoader.LoadAndProcess(paths);
			}
			return null;
		}
	}

	public readonly struct LoadFolder : ICommand
	{
		static async Task<string?> ICommand.Execute(HttpRequest request)
		{
			IFormCollection form = await request.ReadFormAsync();

			string[]? paths;
			if (form.TryGetValue("Path", out StringValues values))
			{
				paths = values;
			}
			else if (NativeDialog.Supported)
			{
				paths = await OpenFolderDialog.OpenFolders();
			}
			else
			{
				return CommandsPath;
			}

			if (paths is { Length: > 0 })
			{
				GameFileLoader.LoadAndProcess(paths);
			}
			return null;
		}
	}

	/// <summary>
	/// 扫描指定文件夹生成依赖关系文件。命令仅负责生成文件，不改变 GameFileLoader 的已加载状态；
	/// 扫描可能耗时较长，放到后台线程执行以免长时间占用 HTTP 请求线程导致超时。
	/// </summary>
	public readonly struct GenerateDependencyMap : ICommand
	{
		static async Task<string?> ICommand.Execute(HttpRequest request)
		{
			IFormCollection form = await request.ReadFormAsync();
			// StringValues 到 string 是显式转换，直接与 null 组成三元表达式无法推断类型
			string? path = form.TryGetValue("Path", out StringValues values) ? (string?)values : null;
			string? outputPath = form.TryGetValue("OutputPath", out StringValues outputValues) ? (string?)outputValues : null;
			if (string.IsNullOrEmpty(path))
			{
				return CommandsPath;
			}

			// 输出路径留空时由扫描器默认输出到被扫描的文件夹内
			string? finalOutputPath = string.IsNullOrEmpty(outputPath) ? null : outputPath;
			Logger.Info(LogCategory.General, $"开始扫描依赖关系：{path}");
			_ = Task.Run(() =>
			{
				try
				{
					DependencyMapScanner.ScanToFile(path, finalOutputPath, LocalFileSystem.Instance);
					Logger.Info(LogCategory.General, $"依赖关系扫描完成：{path}");
				}
				catch (Exception ex)
				{
					Logger.Error(LogCategory.General, $"依赖关系扫描失败：{ex}");
				}
			});
			return null;
		}
	}

	public readonly struct ExportUnityProject : ICommand
	{
		static async Task<string?> ICommand.Execute(HttpRequest request)
		{
			IFormCollection form = await request.ReadFormAsync();

			string? path;
			if (form.TryGetValue("Path", out StringValues values))
			{
				path = values;
			}
			else
			{
				return CommandsPath;
			}

			if (!string.IsNullOrEmpty(path))
			{
				bool createSubfolder = TryGetCreateSubfolder(form);
				string finalPath = MaybeAppendTimestampedSubfolder(path, createSubfolder);

				// 记住本次导出：保存用户输入的基础路径与子文件夹选项，下次打开导出页自动预填
				GameFileLoader.LastExport.ExportPath = path;
				GameFileLoader.LastExport.CreateSubfolder = createSubfolder;
				GameFileLoader.LastExport.SaveToDefaultPath();

				await GameFileLoader.ExportUnityProject(finalPath);
			}
			return null;
		}
	}

	public readonly struct ExportPrimaryContent : ICommand
	{
		static async Task<string?> ICommand.Execute(HttpRequest request)
		{
			IFormCollection form = await request.ReadFormAsync();

			string? path;
			if (form.TryGetValue("Path", out StringValues values))
			{
				path = values;
			}
			else
			{
				return CommandsPath;
			}

			if (!string.IsNullOrEmpty(path))
			{
				bool createSubfolder = TryGetCreateSubfolder(form);
				string finalPath = MaybeAppendTimestampedSubfolder(path, createSubfolder);

				// 记住本次导出：保存用户输入的基础路径与子文件夹选项，下次打开导出页自动预填
				GameFileLoader.LastExport.ExportPath = path;
				GameFileLoader.LastExport.CreateSubfolder = createSubfolder;
				GameFileLoader.LastExport.SaveToDefaultPath();

				await GameFileLoader.ExportPrimaryContent(finalPath);
			}
			return null;
		}
	}

	private static string MaybeAppendTimestampedSubfolder(string path, bool append)
	{
		if (append)
		{
			string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
			string subfolder = $"AssetRipper_export_{timestamp}";
			return Path.Combine(path, subfolder);
		}

		return path;
	}

	public readonly struct Reset : ICommand
	{
		static Task<string?> ICommand.Execute(HttpRequest request)
		{
			GameFileLoader.Reset();
			return Task.FromResult<string?>(null);
		}
	}

	public static async Task HandleCommand<T>(HttpContext context) where T : ICommand
	{
		string? redirectionTarget = await T.Execute(context.Request);
		context.Response.Redirect(redirectionTarget ?? RootPath);
	}
}
