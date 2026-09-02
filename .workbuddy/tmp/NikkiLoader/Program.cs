using AssetRipper.Assets;
using AssetRipper.Export.Configuration;
using AssetRipper.Import.Logging;
using AssetRipper.IO.Files;
using AssetRipper.Processing;

namespace NikkiLoader;

internal static class Program
{
	private static void Main(string[] args)
	{
		string path = args.Length > 0
			? args[0]
			: @"D:\UserData\閃耀暖暖_4.1.2328503\assets\art\spine\face\bq_skeletondata.asset_locale.chinesesimplified";

		Logger.Add(new ConsoleLogger(false));

		FullConfiguration settings = new();
		settings.ImportSettings.GameType = AssetRipper.Import.Configuration.GameType.Nikki4;

		ExportHandler handler = new(settings);
		GameData gameData = handler.LoadAndProcess([path], LocalFileSystem.Instance);

		Console.WriteLine();
		Console.WriteLine("==== ASSETS ====");
		foreach (AssetCollection collection in gameData.Collections)
		{
			Console.WriteLine($"-- Collection: {collection.Name} ({collection.GetType().Name})");
			foreach (IUnityObjectBase asset in collection)
			{
				Console.WriteLine(
					$"   PathID={asset.PathID} ClassID={asset.ClassID} ClassName={asset.ClassName} Name={asset.GetBestName()}");
			}
		}
	}
}
