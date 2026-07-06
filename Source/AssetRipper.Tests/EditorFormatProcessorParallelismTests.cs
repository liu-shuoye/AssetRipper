using AssetRipper.Assets;
using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.IO.Files;
using AssetRipper.IO.Files.SerializedFiles;
using AssetRipper.Primitives;
using AssetRipper.Processing;
using AssetRipper.Processing.Configuration;
using AssetRipper.Processing.Editor;
using AssetRipper.SourceGenerated.Extensions;
using System.Threading;

namespace AssetRipper.Tests;

/// <summary>
/// Tests for <see cref="EditorFormatProcessor"/> parallelism configuration,
/// verifying that <see cref="ProcessingSettings.MaxImportParallelism"/> is honored
/// by the <see cref="ParallelOptions.MaxDegreeOfParallelism"/> used in
/// <see cref="EditorFormatProcessor.Process(GameData)"/>.
/// </summary>
internal class EditorFormatProcessorParallelismTests
{
	/// <summary>
	/// When <see cref="ProcessingSettings.MaxImportParallelism"/> is set to 1,
	/// <see cref="EditorFormatProcessor.ConvertAsync"/> must never execute concurrently.
	/// </summary>
	[Test]
	public void ProcessWithParallelismOne_RunsSerially()
	{
		const int AssetCount = 50;
		ProcessedAssetCollection collection = CreateReleaseCollection();
		for (int i = 0; i < AssetCount; i++)
		{
			collection.CreateMesh();
		}
		GameData gameData = CreateGameData(collection);

		ProcessingSettings settings = new() { MaxImportParallelism = 1 };
		ConcurrencyTrackingProcessor processor = new(BundledAssetsExportMode.DirectExport, settings);

		processor.Process(gameData);

		Assert.Multiple(() =>
		{
			Assert.That(processor.TotalProcessed, Is.EqualTo(AssetCount),
				"All release assets should have been processed by ConvertAsync.");
			Assert.That(processor.MaxConcurrentCount, Is.EqualTo(1),
				"ConvertAsync must run completely serially when MaxImportParallelism = 1.");
		});
	}

	/// <summary>
	/// When <see cref="ProcessingSettings.MaxImportParallelism"/> is set to a small value
	/// greater than 1, the observed concurrency must not exceed that value.
	/// </summary>
	[Test]
	public void ProcessWithParallelismTwo_ConcurrencyDoesNotExceedLimit()
	{
		const int AssetCount = 50;
		ProcessedAssetCollection collection = CreateReleaseCollection();
		for (int i = 0; i < AssetCount; i++)
		{
			collection.CreateMesh();
		}
		GameData gameData = CreateGameData(collection);

		ProcessingSettings settings = new() { MaxImportParallelism = 2 };
		ConcurrencyTrackingProcessor processor = new(BundledAssetsExportMode.DirectExport, settings);

		processor.Process(gameData);

		Assert.Multiple(() =>
		{
			Assert.That(processor.TotalProcessed, Is.EqualTo(AssetCount),
				"All release assets should have been processed by ConvertAsync.");
			Assert.That(processor.MaxConcurrentCount, Is.LessThanOrEqualTo(2),
				"ConvertAsync must not exceed MaxImportParallelism concurrent executions.");
		});
	}

	/// <summary>
	/// With the default <see cref="ProcessingSettings.MaxImportParallelism"/>
	/// (which is <see cref="Environment.ProcessorCount"/>), processing must complete
	/// without throwing and must process every release asset.
	/// </summary>
	[Test]
	public void ProcessWithProcessorCount_DoesNotThrowAndProcessesAllAssets()
	{
		const int AssetCount = 20;
		ProcessedAssetCollection collection = CreateReleaseCollection();
		for (int i = 0; i < AssetCount; i++)
		{
			collection.CreateMesh();
		}
		GameData gameData = CreateGameData(collection);

		ProcessingSettings settings = new() { MaxImportParallelism = Environment.ProcessorCount };
		ConcurrencyTrackingProcessor processor = new(BundledAssetsExportMode.DirectExport, settings);

		Assert.DoesNotThrow(() => processor.Process(gameData));
		Assert.That(processor.TotalProcessed, Is.EqualTo(AssetCount),
			"All release assets should have been processed by ConvertAsync.");
	}

	/// <summary>
	/// When <see cref="EditorFormatProcessor"/> is constructed with a null
	/// <see cref="ProcessingSettings"/> (the default parameter value), processing
	/// must still work and fall back to <see cref="Environment.ProcessorCount"/>.
	/// </summary>
	[Test]
	public void ProcessWithNullSettings_DoesNotThrow()
	{
		ProcessedAssetCollection collection = CreateReleaseCollection();
		for (int i = 0; i < 20; i++)
		{
			collection.CreateMesh();
		}
		GameData gameData = CreateGameData(collection);

		EditorFormatProcessor processor = new(BundledAssetsExportMode.DirectExport, settings: null);

		Assert.DoesNotThrow(() => processor.Process(gameData));
	}

	/// <summary>
	/// When <see cref="ProcessingSettings.MaxImportParallelism"/> is 1 and the
	/// real <see cref="EditorFormatProcessor.ConvertAsync(IUnityObjectBase)"/>
	/// runs (not just a tracking override), processing must complete without
	/// throwing. This exercises the actual asset mutation path serially.
	/// </summary>
	[Test]
	public void ProcessWithRealConvertAndParallelismOne_DoesNotThrow()
	{
		ProcessedAssetCollection collection = CreateReleaseCollection();
		for (int i = 0; i < 20; i++)
		{
			collection.CreateMesh();
		}
		GameData gameData = CreateGameData(collection);

		ProcessingSettings settings = new() { MaxImportParallelism = 1 };
		EditorFormatProcessor processor = new(BundledAssetsExportMode.DirectExport, settings);

		Assert.DoesNotThrow(() => processor.Process(gameData));
	}

	/// <summary>
	/// A collection with no release assets (the default for
	/// <see cref="GameBundle.AddNewProcessedCollection(string, UnityVersion)"/>)
	/// must result in zero ConvertAsync invocations, since
	/// <see cref="EditorFormatProcessor"/> only processes release collections.
	/// </summary>
	[Test]
	public void ProcessWithNoReleaseAssets_DoesNotInvokeConvertAsync()
	{
		// Create a collection with default (non-release) flags
		ProcessedAssetCollection collection = AssetCreator.CreateCollection(UnityVersion.V_2022);
		for (int i = 0; i < 10; i++)
		{
			collection.CreateMesh();
		}
		GameData gameData = CreateGameData(collection);

		ProcessingSettings settings = new() { MaxImportParallelism = 1 };
		ConcurrencyTrackingProcessor processor = new(BundledAssetsExportMode.DirectExport, settings);

		processor.Process(gameData);

		Assert.That(processor.TotalProcessed, Is.EqualTo(0),
			"Non-release assets must not be processed by ConvertAsync.");
	}

	private static ProcessedAssetCollection CreateReleaseCollection()
	{
		ProcessedAssetCollection collection = AssetCreator.CreateCollection(UnityVersion.V_2022);
		// The default flags from AddNewProcessedCollection are NoTransferInstructionFlags (editor),
		// so we must explicitly mark the collection as a release collection to match what
		// EditorFormatProcessor.GetReleaseCollections expects.
		collection.SetLayout(UnityVersion.V_2022, BuildTarget.NoTarget, TransferInstructionFlags.SerializeGameRelease);
		return collection;
	}

	private static GameData CreateGameData(ProcessedAssetCollection collection)
	{
		return new GameData((GameBundle)collection.Bundle, collection.Version, new BaseManager(_ => { }), null);
	}

	/// <summary>
	/// A test double that overrides <see cref="EditorFormatProcessor.ConvertAsync"/>
	/// to track concurrent invocations via <see cref="Interlocked"/> operations.
	/// This lets us verify that <see cref="ParallelOptions.MaxDegreeOfParallelism"/>
	/// is actually being honored at runtime.
	/// </summary>
	private sealed class ConcurrencyTrackingProcessor : EditorFormatProcessor
	{
		private int concurrentCount;
		private int maxConcurrentCount;
		private int totalProcessed;

		public int MaxConcurrentCount => maxConcurrentCount;
		public int TotalProcessed => totalProcessed;

		public ConcurrencyTrackingProcessor(BundledAssetsExportMode mode, ProcessingSettings? settings)
			: base(mode, settings)
		{
		}

		protected override void ConvertAsync(IUnityObjectBase asset)
		{
			int current = Interlocked.Increment(ref concurrentCount);

			// Atomically track the maximum observed concurrent count.
			int observedMax;
			do
			{
				observedMax = maxConcurrentCount;
				if (current <= observedMax)
				{
					break;
				}
			}
			while (Interlocked.CompareExchange(ref maxConcurrentCount, current, observedMax) != observedMax);

			// A small delay increases the probability of overlapping executions when
			// MaxDegreeOfParallelism > 1, making the test more likely to detect
			// concurrency violations. When MaxDegreeOfParallelism == 1, this delay
			// has no effect on serial execution.
			Thread.Sleep(1);

			Interlocked.Increment(ref totalProcessed);
			Interlocked.Decrement(ref concurrentCount);
		}
	}
}
