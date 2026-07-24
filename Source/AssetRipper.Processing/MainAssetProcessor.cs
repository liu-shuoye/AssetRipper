using AssetRipper.Assets;
using AssetRipper.Assets.Collections;
using AssetRipper.Import.Logging;
using AssetRipper.SourceGenerated.Classes.ClassID_128;
using AssetRipper.SourceGenerated.Classes.ClassID_156;
using AssetRipper.SourceGenerated.Classes.ClassID_21;
using AssetRipper.SourceGenerated.Classes.ClassID_27;
using AssetRipper.SourceGenerated.Classes.ClassID_28;
using AssetRipper.SourceGenerated.Extensions;

namespace AssetRipper.Processing;

public class MainAssetProcessor : IAssetProcessor
{
	public void Process(GameData gameData)
	{
		Logger.Info(LogCategory.Processing, "主要资产配对");
		// 用元数据枚举避免 FetchAssets() 触发全量反序列化。
		// 仅对 IFont (128) 与 ITerrainData (156) 调用 TryGetAssetOnly 做单对象反序列化。
		foreach (AssetCollection collection in gameData.GameBundle.FetchAssetCollections())
		{
			foreach (AssetCollection.AssetMetadata meta in collection.EnumerateAssetMetadata())
			{
				if (meta.ClassID == 128) // IFont
				{
					IFont? font = collection.TryGetAssetOnly<IFont>(meta.PathID);
					if (font is null)
					{
						continue;
					}
					font.MainAsset = font;
					if (font.TryGetFontMaterial(out IMaterial? fontMaterial))
					{
						fontMaterial.MainAsset = font;
					}
					if (font.TryGetFontTexture(out ITexture? fontTexture))
					{
						fontTexture.MainAsset = font;
					}
				}
				else if (meta.ClassID == 156) // ITerrainData
				{
					ITerrainData? terrainData = collection.TryGetAssetOnly<ITerrainData>(meta.PathID);
					if (terrainData is null)
					{
						continue;
					}
					terrainData.MainAsset = terrainData;
					foreach (ITexture2D alphaTexture in terrainData.GetSplatAlphaTextures())
					{
						//有时 TerrainData 可能会被复制，但保留相同的 alpha 纹理。
						//https://github.com/AssetRipper/AssetRipper/issues/1356
						alphaTexture.MainAsset ??= terrainData;
					}
				}
			}
		}
	}
}
