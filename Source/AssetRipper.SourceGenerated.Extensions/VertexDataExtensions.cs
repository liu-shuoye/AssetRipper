using AssetRipper.Numerics;
using AssetRipper.SourceGenerated.Extensions;
using AssetRipper.SourceGenerated.Extensions.Enums.Shader.ShaderChannel;
using AssetRipper.SourceGenerated.Extensions.Enums.Shader.VertexFormat;
using AssetRipper.SourceGenerated.Subclasses.ChannelInfo;
using AssetRipper.SourceGenerated.Subclasses.StreamInfo;
using AssetRipper.SourceGenerated.Subclasses.StreamingInfo;
using AssetRipper.SourceGenerated.Subclasses.SubMesh;
using AssetRipper.SourceGenerated.Subclasses.VertexData;
using System.Numerics;

namespace AssetRipper.SourceGenerated.Extensions;

public static class VertexDataExtensions
{
	private const int VertexStreamAlign = 16;

	/// <summary>
	/// 标准 Unity 导入器允许的顶点流上限（stream 索引 0-3）
	/// </summary>
	private const int MaxVertexStreams = 4;

	public static bool IsSet(this IVertexData instance, IStreamingInfo? streamingInfo)
	{
		return instance.VertexCount > 0 && (instance.Data.Length > 0 || streamingInfo is not null && streamingInfo.IsSet());
	}

	public static IReadOnlyList<IStreamInfo>? GetStreams(this IVertexData instance)
	{
		if (instance.Has_Streams())
		{
			return instance.Streams;
		}
		else if (instance.Has_Streams_0_())
		{
			return
			[
				instance.Streams_0_,
				instance.Streams_1_,
				instance.Streams_2_,
				instance.Streams_3_
			];
		}
		else
		{
			return null;
		}
	}

	public static uint GetCurrentChannels(this IVertexData instance)
	{
		if (instance.Has_CurrentChannels_Int32())
		{
			return unchecked((uint)instance.CurrentChannels_Int32);
		}
		else
		{
			return instance.CurrentChannels_UInt32;
		}
	}

	public static void SetCurrentChannels(this IVertexData instance, uint value)
	{
		if (instance.Has_CurrentChannels_Int32())
		{
			instance.CurrentChannels_Int32 = unchecked((int)value);
		}
		else
		{
			instance.CurrentChannels_UInt32 = value;
		}
	}

	/// <summary>
	/// 5.6.0
	/// </summary>
	private static bool AllowUnsetVertexChannel(UnityVersion version) => version.Equals(5, 6, 0);

	public static ChannelInfo GetChannel(this IVertexData instance, UnityVersion version, ShaderChannel channelType)
	{
		if (instance.Has_Channels())
		{
			return instance.Channels[channelType.ToChannel(version)];
		}
		else
		{
			IReadOnlyList<IStreamInfo> streams = instance.GetStreams()!;
			ChannelInfo channelInfo = new();
			ShaderChannel4 channelv4 = channelType.ToShaderChannel4();
			int streamIndex = streams.IndexOf(t => t.IsMatch(channelv4));
			if (streamIndex >= 0)
			{
				byte offset = 0;
				IStreamInfo stream = streams[streamIndex];
				for (ShaderChannel4 i = 0; i < channelv4; i++)
				{
					if (stream.IsMatch(i))
					{
						offset += i.ToShaderChannel().GetStride(version);
					}
				}

				channelInfo.Stream = (byte)streamIndex;
				channelInfo.Offset = offset;
				channelInfo.Format = channelType.GetVertexFormat(version).ToFormat(version);
				channelInfo.Dimension = channelType.GetDimention(version);
			}
			return channelInfo;
		}
	}

	public static BoneWeight4[] GenerateSkin(this IVertexData instance, UnityVersion version)
	{
		if (instance.Channels is null)
		{
			throw new NotImplementedException("GenerateSkin is not implemented for this version.");
		}
		ChannelInfo weightChannel = instance.Channels[(int)ShaderChannel2018.SkinWeight];
		ChannelInfo indexChannel = instance.Channels[(int)ShaderChannel2018.SkinBoneIndex];
		if (!weightChannel.IsSet())
		{
			return Array.Empty<BoneWeight4>();
		}

		BoneWeight4[] skin = new BoneWeight4[instance.VertexCount];
		int weightStride = instance.Channels.Where(t => t.Stream == weightChannel.Stream).Sum(t => t.GetStride(version));
		int weightStreamOffset = instance.GetStreamOffset(version, weightChannel.Stream);
		int indexStride = instance.Channels.Where(t => t.Stream == indexChannel.Stream).Sum(t => t.GetStride(version));
		int indexStreamOffset = instance.GetStreamOffset(version, indexChannel.Stream);

		using MemoryStream memStream = new MemoryStream(instance.Data);
		using BinaryReader reader = new BinaryReader(memStream);

		int weightCount = Math.Min((int)weightChannel.GetDataDimension(), 4);
		int indexCount = Math.Min((int)indexChannel.GetDataDimension(), 4);
		float[] weights = new float[Math.Max(weightCount, 4)];
		int[] indices = new int[Math.Max(indexCount, 4)];
		for (int v = 0; v < instance.VertexCount; v++)
		{
			memStream.Position = weightStreamOffset + v * weightStride + weightChannel.Offset;
			for (int i = 0; i < weightCount; i++)
			{
				weights[i] = reader.ReadSingle();
			}

			memStream.Position = indexStreamOffset + v * indexStride + indexChannel.Offset;
			for (int i = 0; i < indexCount; i++)
			{
				indices[i] = reader.ReadInt32();
			}

			skin[v] = new BoneWeight4(weights[0], weights[1], weights[2], weights[3], indices[0], indices[1], indices[2], indices[3]);
		}
		return skin;
	}

	public static Vector3[] GenerateVertices(this IVertexData instance, UnityVersion version, ISubMesh submesh)
	{
		IChannelInfo channel = instance.GetChannel(version, ShaderChannel.Vertex);
		if (!channel.IsSet())
		{
			if (AllowUnsetVertexChannel(version))
			{
				return Array.Empty<Vector3>();
			}
			else
			{
				throw new Exception("Vertices hasn't been found");
			}
		}

		Vector3[] verts = new Vector3[submesh.VertexCount];
		int streamStride = instance.GetStreamStride(version, channel.Stream);
		int streamOffset = instance.GetStreamOffset(version, channel.Stream);
		using (MemoryStream memStream = new MemoryStream(instance.Data))
		{
			using BinaryReader reader = new BinaryReader(memStream);
			memStream.Position = streamOffset + submesh.FirstVertex * streamStride + channel.Offset;
			for (int v = 0; v < submesh.VertexCount; v++)
			{
				float x = reader.ReadSingle();
				float y = reader.ReadSingle();
				float z = reader.ReadSingle();
				verts[v] = new Vector3(x, y, z);
				memStream.Position += streamStride - 12;
			}
		}
		return verts;
	}

	public static int GetStreamStride(this IVertexData instance, UnityVersion version, int stream)
	{
		return instance.HasStreamsInvariant() ?
			(int)instance.GetStreamsInvariant()[stream].GetStride() : instance.Channels!.Where(t => t.IsSet() && t.Stream == stream).Sum(t => t.GetStride(version));
	}

	public static int GetStreamSize(this IVertexData instance, UnityVersion version, int stream)
	{
		return instance.GetStreamStride(version, stream) * (int)instance.VertexCount;
	}

	public static int GetStreamOffset(this IVertexData instance, UnityVersion version, int stream)
	{
		int offset = 0;
		for (int i = 0; i < stream; i++)
		{
			offset += instance.GetStreamSize(version, i);
			offset = offset + (VertexStreamAlign - 1) & ~(VertexStreamAlign - 1);
		}
		return offset;
	}

	private static bool HasStreamsInvariant(this IVertexData instance) => instance.Has_Streams() || instance.Has_Streams_0_();

	private static IReadOnlyList<IStreamInfo> GetStreamsInvariant(this IVertexData instance)
	{
		if (instance.Has_Streams())
		{
			return instance.Streams;
		}
		else if (instance.Has_Streams_0_())
		{
			return new IStreamInfo[]
			{
				instance.Streams_0_,
				instance.Streams_1_,
				instance.Streams_2_,
				instance.Streams_3_
			};
		}
		else
		{
			return Array.Empty<IStreamInfo>();
		}
	}

	/// <summary>
	/// 将顶点流数量压缩到标准 Unity 支持的上限（stream 0-3）。
	/// 闪耀暖暖（Nikki4）的魔改引擎序列化 Mesh 时允许写出 5 个及以上的顶点流，
	/// 而标准 Unity 导入器遇到 stream &gt;= 4 的通道会直接抛出
	/// "Vertex stream out of range: 4 (max 3)" 崩溃。因此把 stream 3 之后的全部
	/// 通道合并进 stream 3（按流号、流内偏移升序紧凑排列），并按 16 字节对齐规则
	/// 重建顶点数据缓冲，使导出的 .asset 可以被 Unity 正常解析。
	/// 布局合法时不做任何修改，仅当存在越界流（stream &gt;= 4）时才执行重建。
	/// </summary>
	public static void NormalizeStreams(this IVertexData instance, UnityVersion version)
	{
		if (instance.Channels is not { Count: > 0 } channels)
		{
			return;
		}

		// 没有任何通道引用第 5 个及以后的流时，布局本就是标准 Unity 兼容的，直接返回
		bool needsNormalize = false;
		foreach (ChannelInfo channel in channels)
		{
			if (channel.IsSet() && channel.Stream >= MaxVertexStreams)
			{
				needsNormalize = true;
				break;
			}
		}
		if (!needsNormalize)
		{
			return;
		}

		int vertexCount = (int)instance.VertexCount;
		if (vertexCount == 0)
		{
			return;
		}

		// ---- 旧布局：流内 stride 与各流的全局偏移（相邻流之间 16 字节对齐） ----
		int oldStreamCount = channels.Where(c => c.IsSet()).Max(c => (int)c.Stream) + 1;
		int[] oldStreamStride = new int[oldStreamCount];
		foreach (ChannelInfo channel in channels)
		{
			if (channel.IsSet())
			{
				oldStreamStride[channel.Stream] += channel.GetStride(version);
			}
		}
		int[] oldStreamOffset = new int[oldStreamCount];
		ComputeStreamOffsets(vertexCount, oldStreamStride, oldStreamOffset);

		// ---- 新布局：stream 0..2 原样保留，stream 3 及以后的通道紧凑合并进新 stream 3 ----
		// 合并组按 (原始流号, 流内偏移) 升序，保证源数据拷贝时定位正确
		List<(ChannelInfo Channel, byte OldStream, byte OldOffset)> merged = new();
		for (int stream = 3; stream < oldStreamCount; stream++)
		{
			foreach (ChannelInfo channel in channels)
			{
				if (channel.IsSet() && channel.Stream == stream)
				{
					merged.Add((channel, (byte)stream, channel.Offset));
				}
			}
		}
		merged.Sort(static (a, b) => a.OldStream != b.OldStream
			? a.OldStream - b.OldStream
			: a.OldOffset - b.OldOffset);

		int newStream3Stride = 0;
		foreach ((ChannelInfo channel, _, _) in merged)
		{
			newStream3Stride += channel.GetStride(version);
		}

		int[] newStreamStride = new[] { oldStreamStride[0], oldStreamStride[1], oldStreamStride[2], newStream3Stride };
		int[] newStreamOffset = new int[4];
		ComputeStreamOffsets(vertexCount, newStreamStride, newStreamOffset);

		// 更新通道的流归属与流内偏移：stream < 3 的通道保持不变
		int newStream3LocalOffset = 0;
		foreach ((ChannelInfo channel, _, _) in merged)
		{
			channel.Stream = 3;
			channel.Offset = (byte)newStream3LocalOffset;
			newStream3LocalOffset += channel.GetStride(version);
		}

		byte[] oldData = instance.Data;
		byte[] newData = new byte[newStreamOffset[3] + vertexCount * newStream3Stride];
		for (int v = 0; v < vertexCount; v++)
		{
			// stream 0..2 的通道：新旧布局一致，原位搬运
			foreach (ChannelInfo channel in channels)
			{
				if (channel.IsSet() && channel.Stream < 3)
				{
					int stride = channel.GetStride(version);
					int src = oldStreamOffset[channel.Stream] + v * oldStreamStride[channel.Stream] + channel.Offset;
					int dst = newStreamOffset[channel.Stream] + v * newStreamStride[channel.Stream] + channel.Offset;
					Array.Copy(oldData, src, newData, dst, stride);
				}
			}

			// 合并到新 stream 3 的通道：从旧流位置读取，写入紧凑后的新 offset
			foreach ((ChannelInfo channel, byte oldStream, byte oldOffset) in merged)
			{
				int stride = channel.GetStride(version);
				int src = oldStreamOffset[oldStream] + v * oldStreamStride[oldStream] + oldOffset;
				int dst = newStreamOffset[3] + v * newStream3Stride + channel.Offset;
				Array.Copy(oldData, src, newData, dst, stride);
			}
		}
		instance.Data = newData;
	}

	/// <summary>
	/// 按 Unity 规则计算每个流的全局偏移：前一个流结束后对齐到 16 字节再排下一个流
	/// </summary>
	private static void ComputeStreamOffsets(int vertexCount, int[] streamStrides, int[] streamOffsets)
	{
		int offset = 0;
		for (int i = 0; i < streamOffsets.Length; i++)
		{
			streamOffsets[i] = offset;
			offset += streamStrides[i] * vertexCount;
			offset = offset + (VertexStreamAlign - 1) & ~(VertexStreamAlign - 1);
		}
	}
}
