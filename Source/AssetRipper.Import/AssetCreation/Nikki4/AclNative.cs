using System.Runtime.InteropServices;

namespace AssetRipper.Import.AssetCreation.Nikki4;

/// <summary>
/// 与 External/acl-native/include/acl_native.h 中的 AclvHeaderInfo 一一对应。
/// 字段顺序与布局不可随意调整。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct AclNativeHeaderInfo
{
	public uint Size;
	public uint Hash;
	public ushort Version;
	public byte AlgorithmType;
	public byte TrackType;
	public uint NumTracks;
	public uint NumSamples;
	public float SampleRate;
	public float Duration;
	public uint NumSegments;
	public uint AnimatedRotationSubTracks;
	public uint AnimatedTranslationSubTracks;
	public uint AnimatedScaleSubTracks;
	public uint ConstantRotationSamples;
	public uint ConstantTranslationSamples;
	public uint ConstantScaleSamples;
	public byte HasScale;
	public byte DefaultScale;
	public int ErrorCode;
	[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
	public string ErrorMessage;

	public readonly string GetErrorMessage() => ErrorMessage ?? string.Empty;
}

/// <summary>
/// acl-native.dll 的托管封装。Open 时把 blob 复制到非托管内存，
/// 解压上下文在 Close 前一直引用该内存，因此不能在 Open 之后释放。
/// </summary>
public sealed class AclNativeClip : IDisposable
{
	private IntPtr m_blob;
	private IntPtr m_handle;
	private bool m_disposed;
	public AclNativeHeaderInfo Info { get; }

	private AclNativeClip(IntPtr blob, IntPtr handle, AclNativeHeaderInfo info)
	{
		m_blob = blob;
		m_handle = handle;
		Info = info;
	}

	public static AclNativeClip Open(byte[] blob)
	{
		ArgumentNullException.ThrowIfNull(blob);
		if (blob.Length < 16)
		{
			throw new ArgumentException("ACL blob is too small.", nameof(blob));
		}

		IntPtr nativeBlob = Marshal.AllocHGlobal(blob.Length);
		try
		{
			Marshal.Copy(blob, 0, nativeBlob, blob.Length);
			AclNativeHeaderInfo info = default;
			info.ErrorMessage = string.Empty;
			int result = AclvOpen(nativeBlob, (uint)blob.Length, out IntPtr handle, ref info);
			if (result != 0)
			{
				throw new InvalidOperationException($"aclv_open failed ({result}): {info.GetErrorMessage()}");
			}
			return new AclNativeClip(nativeBlob, handle, info);
		}
		catch
		{
			Marshal.FreeHGlobal(nativeBlob);
			throw;
		}
	}

	public void GetSubTrackTypes(byte[] rotationTypes, byte[] translationTypes, byte[] scaleTypes)
	{
		ObjectDisposedException.ThrowIf(m_disposed, this);
		ArgumentNullException.ThrowIfNull(rotationTypes);
		ArgumentNullException.ThrowIfNull(translationTypes);
		ArgumentNullException.ThrowIfNull(scaleTypes);
		if (rotationTypes.Length < Info.NumTracks || translationTypes.Length < Info.NumTracks || scaleTypes.Length < Info.NumTracks)
		{
			throw new ArgumentException("Sub-track type arrays are too small.");
		}
		int result = AclvGetSubTrackTypes(m_handle, rotationTypes, translationTypes, scaleTypes, Info.NumTracks);
		if (result != 0)
		{
			throw new InvalidOperationException($"aclv_get_sub_track_types failed ({result}).");
		}
	}

	public void DecompressTrack(int trackIndex, float[] rotations, float[] translations, float[] scales)
	{
		ObjectDisposedException.ThrowIf(m_disposed, this);
		ArgumentNullException.ThrowIfNull(rotations);
		ArgumentNullException.ThrowIfNull(translations);
		ArgumentNullException.ThrowIfNull(scales);
		if ((uint)trackIndex >= Info.NumTracks)
		{
			throw new ArgumentOutOfRangeException(nameof(trackIndex));
		}
		if (rotations.Length < Info.NumSamples * 4 || translations.Length < Info.NumSamples * 3 || scales.Length < Info.NumSamples * 3)
		{
			throw new ArgumentException("Track sample arrays are too small.");
		}
		int result = AclvDecompressTrack(m_handle, (uint)trackIndex, rotations, translations, scales, Info.NumSamples);
		if (result != 0)
		{
			throw new InvalidOperationException($"aclv_decompress_track failed ({result}) for track {trackIndex}.");
		}
	}

	public void Dispose()
	{
		if (m_disposed)
		{
			return;
		}
		m_disposed = true;
		if (m_handle != IntPtr.Zero)
		{
			AclvClose(m_handle);
			m_handle = IntPtr.Zero;
		}
		if (m_blob != IntPtr.Zero)
		{
			Marshal.FreeHGlobal(m_blob);
			m_blob = IntPtr.Zero;
		}
	}

	[DllImport("acl-native", EntryPoint = "aclv_open", CallingConvention = CallingConvention.Cdecl)]
	private static extern int AclvOpen(IntPtr data, uint size, out IntPtr handle, ref AclNativeHeaderInfo info);

	[DllImport("acl-native", EntryPoint = "aclv_close", CallingConvention = CallingConvention.Cdecl)]
	private static extern void AclvClose(IntPtr handle);

	[DllImport("acl-native", EntryPoint = "aclv_get_sub_track_types", CallingConvention = CallingConvention.Cdecl)]
	private static extern int AclvGetSubTrackTypes(IntPtr handle, byte[] rotationTypes, byte[] translationTypes, byte[] scaleTypes, uint trackCapacity);

	[DllImport("acl-native", EntryPoint = "aclv_decompress_track", CallingConvention = CallingConvention.Cdecl)]
	private static extern int AclvDecompressTrack(IntPtr handle, uint trackIndex, float[] rotations, float[] translations, float[] scales, uint sampleCapacity);
}
