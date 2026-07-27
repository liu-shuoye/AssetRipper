using AssetRipper.Import.Logging;
using AssetRipper.SourceGenerated.Classes.ClassID_83;
using AssetRipper.SourceGenerated.Extensions;
using AssetRipper.SourceGenerated.NativeEnums.Fmod;
using Fmod5Sharp;
using Fmod5Sharp.FmodTypes;
using Fmod5Sharp.Util;

namespace AssetRipper.Export.Modules.Audio;

public static class AudioClipDecoder
{
	public static bool TryDecode(
		IAudioClip audioClip,
		[NotNullWhen(true)] out byte[]? decodedData,
		[NotNullWhen(true)] out string? fileExtension,
		[NotNullWhen(false)] out string? message)
	{
		byte[] rawData = audioClip.GetAudioData();

		if (rawData.Length == 0)
		{
			decodedData = null;
			fileExtension = null;
			message = $"Can't decode audio clip '{audioClip.Name}' with default decoder because its audio data could not be found.";
			return false;
		}

		if (audioClip.Has_Type())
		{
			fileExtension = audioClip.GetSoundType().ToRawExtension();
			decodedData = rawData;
			message = null;
			return true;
		}
		else if (CheckMagic(rawData, "FSB5"u8))
		{
			// 使用 nullable 类型，区分"未确定"和"已确定但不支持"
			FmodAudioType? audioType = null;
			try
			{
				if (FsbLoader.TryLoadFsbFromByteArray(rawData, out FmodSoundBank? fsbData))
				{
					audioType = fsbData!.Header.AudioType;
					if (audioType.Value.IsSupported() && fsbData.Samples.Single().RebuildAsStandardFileFormat(out decodedData, out fileExtension))
					{
						message = null;
						return true;
					}
					else
					{
						decodedData = null;
						fileExtension = null;
						message = $"Can't decode audio clip '{audioClip.Name}' with Fmod5Sharp because it's '{audioType.Value}' encoded.";
						return false;
					}
				}
			}
			catch (Exception ex)
			{
				// Fmod5Sharp 解析 FSB5 时抛出异常（如数据截断导致 EndOfStreamException）。
				// 此时 audioType 可能为 null，不应报告误导性的 uint.MaxValue。
				// 回退为导出原始 FSB5 数据，避免音频完全丢失并维持引用完整性，
				// 用户可用外部工具（如 fsbank、VGMStream）处理导出的 .fsb 文件。
				string typeInfo = audioType.HasValue ? $" ({audioType.Value})" : " (unknown)";
				Logger.Warning(LogCategory.Export, $"Failed to decode FSB5 audio clip '{audioClip.Name}'{typeInfo} with data length {rawData.Length}, falling back to raw export.\n{ex}");
				fileExtension = "fsb";
				decodedData = rawData;
				message = null;
				return true;
			}
			// TryLoadFsbFromByteArray 返回 false（未抛出异常），说明 FSB5 头校验失败但数据确实是 FSB5 格式。
			// 同样回退为导出原始 FSB5 数据。
			fileExtension = "fsb";
			decodedData = rawData;
			message = null;
			return true;
		}
		else if (CheckMagic(rawData, "IMPM"u8))
		{
			fileExtension = FmodSoundType.It.ToRawExtension();
			decodedData = rawData;
			message = null;
			return true;
		}
		else if (CheckMagic(rawData, "Extended Module: "u8))
		{
			fileExtension = FmodSoundType.Xm.ToRawExtension();
			decodedData = rawData;
			message = null;
			return true;
		}
		// https://moddingwiki.shikadi.net/wiki/S3M_Format
		else if (CheckMagic(rawData, "SCRM"u8, 156))
		{
			fileExtension = FmodSoundType.S3m.ToRawExtension();
			decodedData = rawData;
			message = null;
			return true;
		}
		// https://www.aes.id.au/modformat.html
		else if (
			CheckMagic(rawData, "M.K."u8, 1080) ||
			CheckMagic(rawData, "M!K!"u8, 1080) ||
			CheckMagic(rawData, "FLT4"u8, 1080) ||
			CheckMagic(rawData, "FLT8"u8, 1080) ||
			CheckMagic(rawData, "4CHN"u8, 1080) ||
			CheckMagic(rawData, "6CHN"u8, 1080) ||
			CheckMagic(rawData, "8CHN"u8, 1080))
		{
			fileExtension = FmodSoundType.Mod.ToRawExtension();
			decodedData = rawData;
			message = null;
			return true;
		}

		decodedData = null;
		fileExtension = null;
		Span<char> asciiCharacters = stackalloc char[4];
		CopyPrintable(rawData, asciiCharacters);
		Span<char> hexCharacters = stackalloc char[8];
		CopyHex(rawData, hexCharacters);
		message = $"Failed to convert audio starting with '{asciiCharacters}' ({hexCharacters})";
		return false;
	}

	private static bool CheckMagic(byte[] data, ReadOnlySpan<byte> magic, int startIndex = 0)
	{
		if (data.Length < magic.Length + startIndex)
		{
			return false;
		}
		return data.AsSpan(startIndex, magic.Length).SequenceEqual(magic);
	}

	private static void CopyPrintable(ReadOnlySpan<byte> data, Span<char> characters)
	{
		const char FillCharacter = 'Ø';
		characters.Fill(FillCharacter);
		for (int i = 0; i < data.Length && i < characters.Length; i++)
		{
			char c = (char)data[i];
			if (IsAsciiPrintable(c) && c != '\t')
			{
				characters[i] = c;
			}
		}

		static bool IsAsciiPrintable(char c) => c >= (char)32 && c <= (char)126;
	}

	private static void CopyHex(ReadOnlySpan<byte> data, Span<char> characters)
	{
		ReadOnlySpan<char> hexCharacterLookup = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F'];
		const char FillCharacter = '0';
		characters.Fill(FillCharacter);
		for (int i = 0; i < data.Length && i < characters.Length / 2; i++)
		{
			byte b = data[i];
			int upper = (b & 0xF0) >> 4;
			characters[2 * i] = hexCharacterLookup[upper];
			int lower = b & 0x0F;
			characters[2 * i + 1] = hexCharacterLookup[lower];
		}
	}
}
