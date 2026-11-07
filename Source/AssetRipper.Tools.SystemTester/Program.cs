using AssetRipper.Export.Configuration;
using AssetRipper.Export.UnityProjects;
using AssetRipper.Logging;
using AssetRipper.IO.Files;
using AssetRipper.Processing;
using AssetRipper.Processing.AnimationClips;
using AssetRipper.SourceGenerated.Classes.ClassID_74;
using AssetRipper.SourceGenerated.Extensions;
using AssetRipper.Assets;
using AssetRipper.Assets.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace AssetRipper.Tools.SystemTester;

static class Program
{
	private const string TestsDirectory = "../../Tests";

	static void Main(string[] args)
	{
		Logger.Add(new ConsoleLogger(true));
		Logger.Add(new FileLogger("AssetRipper.Tools.SystemTester.log"));
		Logger.LogSystemInformation("System Tester");
		Logger.BlankLine();

		if (args.Length > 0 && args[0] == "--cache-timing")
		{
			RunCacheTiming(args[1..]);
			return;
		}

		if (args.Length > 0 && args[0] == "--scan-paths")
		{
			RunPathScan(args[1], args[2..]);
			return;
		}

		if (args.Length == 0)
		{
			RunTests();
			Console.ReadLine();
		}
		else
		{
			Rip(args, Path.Join(AppContext.BaseDirectory, "Ripped"));
		}
	}

	/// <summary>
	/// 仅加载/处理并构建 PathChecksumCache，不导出；用于分批前的耗时/内存评估。
	/// </summary>
	static void RunCacheTiming(string[] inputPaths)
	{
		FullConfiguration settings = new();
		ExportHandler exportHandler = new(settings);
		Stopwatch total = Stopwatch.StartNew();
		long workingSetBefore = Environment.WorkingSet;
		GameData gameData = exportHandler.LoadAndProcess(inputPaths, LocalFileSystem.Instance);
		long loadMs = total.ElapsedMilliseconds;
		long workingSetAfterLoad = Environment.WorkingSet;

		total.Restart();
		PathChecksumCache cache = new(gameData.GameBundle.FetchAssetCollections(), gameData.AssemblyManager);
		long cacheMs = total.ElapsedMilliseconds;
		long workingSetAfterCache = Environment.WorkingSet;

		Logger.Info(LogCategory.General,
			$"[CacheTiming] loadProcess={loadMs} ms, cacheBuild={cacheMs} ms, "
			+ $"collections={cache.CollectionCount}, knownPaths={cache.KnownPathCount}, avatarPaths={cache.AvatarPathCount}, "
			+ $"managed={GC.GetTotalMemory(false) / (1024.0 * 1024.0):F1} MB, "
			+ $"workingSet={workingSetAfterCache / (1024.0 * 1024.0):F1} MB "
			+ $"(load delta={(workingSetAfterLoad - workingSetBefore) / (1024.0 * 1024.0):F1} MB, "
			+ $"cache delta={(workingSetAfterCache - workingSetAfterLoad) / (1024.0 * 1024.0):F1} MB)");
	}


	static void RunPathScan(string outputPath, string[] inputPaths)
	{
		FullConfiguration settings = new();
		ExportHandler exportHandler = new(settings);
		Stopwatch total = Stopwatch.StartNew();
		GameData gameData = exportHandler.LoadAndProcess(inputPaths, LocalFileSystem.Instance);
		Logger.Info(LogCategory.General, $"[PathScan] loadProcess={total.ElapsedMilliseconds} ms");

		total.Restart();
		PathChecksumCache cache = new(gameData.GameBundle.FetchAssetCollections(), gameData.AssemblyManager);
		Logger.Info(LogCategory.General,
			$"[PathScan] cacheBuild={total.ElapsedMilliseconds} ms, collections={cache.CollectionCount}, "
			+ $"knownPaths={cache.KnownPathCount}, avatarPaths={cache.AvatarPathCount}, "
			+ $"workingSet={Environment.WorkingSet / (1024.0 * 1024.0):F1} MB");

		int clipCount = 0;
		int unresolvedClips = 0;
		long totalHashes = 0;
		long totalUnresolved = 0;
		List<string> rows = new();
		foreach (AssetCollection collection in gameData.GameBundle.FetchAssetCollections())
		{
			foreach (AssetCollection.AssetMetadata meta in collection.EnumerateAssetMetadata())
			{
				if (meta.ClassID != 74)
				{
					continue;
				}

				IAnimationClip? clip = collection.TryGetAssetOnly<IAnimationClip>(meta.PathID);
				if (clip is null)
				{
					continue;
				}

				clipCount++;
				HashSet<uint> hashes = GetClipHashes(clip);
				Nikki4ClothAnimationPathResolver.TryAddPaths(clip, cache);

				int unresolved = 0;
				foreach (uint hash in hashes)
				{
					if (!cache.TryGetPath(hash, out _))
					{
						unresolved++;
					}
				}

				totalHashes += hashes.Count;
				totalUnresolved += unresolved;
				if (unresolved > 0)
				{
					unresolvedClips++;
				}
				rows.Add($"{clip.Name_C130},{hashes.Count},{unresolved},{collection.Name}");
			}
		}

		File.WriteAllLines(outputPath, new[] { "clip,hashes,unresolved,collection" }.Concat(rows));
		Logger.Info(LogCategory.General,
			$"[PathScan] clips={clipCount}, unresolvedClips={unresolvedClips}, "
			+ $"hashes={totalHashes}, unresolvedHashes={totalUnresolved}, output={outputPath}");
	}

	static HashSet<uint> GetClipHashes(IAnimationClip clip)
	{
		HashSet<uint> hashes = new();
		if (!clip.Has_ClipBindingConstant_C74())
		{
			return hashes;
		}

		foreach (AssetRipper.SourceGenerated.Subclasses.GenericBinding.IGenericBinding binding in clip.ClipBindingConstant_C74.GenericBindings)
		{
			hashes.Add(binding.Path);
		}
		return hashes;
	}


	static void RunTests()
	{
		if (!Directory.Exists(TestsDirectory))
		{
			Logger.Log(LogType.Warning, LogCategory.General, "Tests folder did not exist. Creating...");
			Directory.CreateDirectory(TestsDirectory);
			Logger.Info(LogCategory.General, "Created. Program will now exit.");
			return;
		}

		Logger.Info(LogCategory.General, $"Running tests in {Path.GetFullPath(TestsDirectory)}");
		Logger.BlankLine();

		int numTests = 0;
		int numSuccessful = 0;
		List<(string, string)> successfulTests = new();
		List<(string, string)> unsuccessfulTests = new();
		foreach (string versionPath in Directory.GetDirectories(TestsDirectory))
		{
			string versionName = Path.GetRelativePath(TestsDirectory, versionPath);
			foreach (string testPath in Directory.GetDirectories(versionPath))
			{
				string testName = Path.GetRelativePath(versionPath, testPath);
				Logger.Info(LogCategory.General, $"Found test: '{testName}' for Unity version: '{versionName}'");
				numTests++;
				string inputPath = Path.Join(testPath, "Input");
				if (!Directory.Exists(inputPath))
				{
					Logger.Log(LogType.Error, LogCategory.General, $"No input folder for '{testName}' on Unity version '{versionName}'");
					unsuccessfulTests.Add((versionName, testName));
				}
				else
				{
					try
					{
						string[] inputFiles = Directory.GetFiles(inputPath);
						string[] inputDirectories = Directory.GetDirectories(inputPath);
						string[] inputPaths = Combine(inputFiles, inputDirectories);
						string outputPath = Path.Join(testPath, "Output");
						Rip(inputPaths, outputPath);
						Logger.Info(LogCategory.General, $"Completed test: '{testName}' for Unity version: '{versionName}'");
						Logger.BlankLine(2);
						numSuccessful++;
						successfulTests.Add((versionName, testName));
					}
					catch (Exception ex)
					{
						Logger.Log(LogType.Error, LogCategory.General, ex.ToString());
						Logger.BlankLine(2);
						unsuccessfulTests.Add((versionName, testName));
					}
				}
			}
		}

		Logger.Info(LogCategory.General, $"{numSuccessful}/{numTests} tests successfully completed");
		if (numSuccessful > 0)
		{
			Logger.Info(LogCategory.General, "Successful:");
			foreach ((string version, string test) in successfulTests)
			{
				Logger.Info(LogCategory.General, $"\t{version,-12} {test}");
			}
		}
		if (numSuccessful < numTests)
		{
			Logger.Info(LogCategory.General, "Unsuccessful:");
			foreach ((string version, string test) in unsuccessfulTests)
			{
				Logger.Info(LogCategory.General, $"\t{version,-12} {test}");
			}
		}
	}

	private static void Rip(string[] inputPaths, string outputPath)
	{
		FullConfiguration settings = new();
		settings.LogConfigurationValues();
		ExportHandler exportHandler = new(settings);
		GameData gameData = exportHandler.LoadAndProcess(inputPaths, LocalFileSystem.Instance);
		PrepareExportDirectory(outputPath);
		exportHandler.Export(gameData, outputPath, LocalFileSystem.Instance);
	}

	private static void PrepareExportDirectory(string path)
	{
		if (Directory.Exists(path))
		{
			Logger.Info(LogCategory.Export, "Clearing export directory...");
			Directory.Delete(path, true);
		}
	}

	private static T[] Combine<T>(T[] array1, T[] array2)
	{
		ArgumentNullException.ThrowIfNull(array1);
		ArgumentNullException.ThrowIfNull(array2);

		T[] result = new T[array1.Length + array2.Length];
		for (int i = 0; i < array1.Length; i++)
		{
			result[i] = array1[i];
		}
		for (int j = 0; j < array2.Length; j++)
		{
			result[j + array1.Length] = array2[j];
		}
		return result;
	}
}
