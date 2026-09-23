using AssetRipper.TextureDecoder.Rgb;
using AssetRipper.TextureDecoder.Rgb.Formats;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AssetRipper.Export.Modules.Textures.Astc
{
	public static partial class AstcDecoder
	{
		private static void ValidateBlockDimension(int value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value < 4 || value > 12)
			{
				throw new ArgumentException($"Block dimension must be between 4 and 12, inclusive. Value: {value}", paramName);
			}
		}

		public static int DecodeASTC<TOutputColor, TOutputChannelValue>(ReadOnlySpan<byte> input, int width, int height, int blockWidth, int blockHeight, out byte[] output)
			where TOutputChannelValue : unmanaged
			where TOutputColor : unmanaged, IColor<TOutputChannelValue>
		{
			output = new byte[width * height * Unsafe.SizeOf<TOutputColor>()];
			return DecodeASTC<TOutputColor, TOutputChannelValue>(input, width, height, blockWidth, blockHeight, output);
		}

		public static int DecodeASTC<TOutputColor, TOutputChannelValue>(ReadOnlySpan<byte> input, int width, int height, int blockWidth, int blockHeight, Span<byte> output)
			where TOutputChannelValue : unmanaged
			where TOutputColor : unmanaged, IColor<TOutputChannelValue>
		{
			if (!BitConverter.IsLittleEndian)
			{
				throw new NotSupportedException("ASTC 解码仅支持小端序。");
			}
			return DecodeASTC<TOutputColor, TOutputChannelValue>(input, width, height, blockWidth, blockHeight, MemoryMarshal.Cast<byte, TOutputColor>(output));
		}

		public static int DecodeASTC<TOutputColor, TOutputChannelValue>(ReadOnlySpan<byte> input, int width, int height, int blockWidth, int blockHeight, Span<TOutputColor> output)
			where TOutputChannelValue : unmanaged
			where TOutputColor : unmanaged, IColor<TOutputChannelValue>
		{
			if (output.Length < width * height)
			{
				throw new ArgumentException($"ASTC 输出缓冲区不足：需要 {width * height}，实际 {output.Length}。");
			}
			ValidateBlockDimension(blockWidth);
			ValidateBlockDimension(blockHeight);
			int bcw = (width + blockWidth - 1) / blockWidth;
			int bch = (height + blockHeight - 1) / blockHeight;
			int clen_last = (width + blockWidth - 1) % blockWidth + 1;
			Span<ColorRGBA<byte>> buffer = stackalloc ColorRGBA<byte>[blockWidth * blockHeight];
			int inputOffset = 0;
			for (int t = 0; t < bch; t++)
			{
				for (int s = 0; s < bcw; s++, inputOffset += 16)
				{
					DecodeBlock(input[inputOffset..], blockWidth, blockHeight, buffer);
					int clen = s < bcw - 1 ? blockWidth : clen_last;
					for (int i = 0, y = t * blockHeight; i < blockHeight && y < height; i++, y++)
					{
						int outputOffsetUInt32 = t * blockHeight * width + s * blockWidth + i * width;
						for (int j = 0; j < clen; j++)
						{
							output[outputOffsetUInt32 + j] = buffer[j + i * blockWidth].Convert<ColorRGBA<byte>, byte, TOutputColor, TOutputChannelValue>();
						}
					}
				}
			}
			return inputOffset;
		}

		private static void DecodeBlock(ReadOnlySpan<byte> input, int blockWidth, int blockHeight, Span<ColorRGBA<byte>> output)
		{
			if (input[0] == 0xfc && (input[1] & 1) == 1)
			{
				// void-extent 块：input[1] 的 bit1 置位时 RGBA 以 16 位浮点（HDR）存储，否则为 8 位 LDR。
				// 参考 ASTC 规范 void-extent 编码位布局：float 值位于字节 8~15（每通道低字节在前取高 11 位）。
				if ((input[1] & 2) != 0)
				{
					byte r = F32ToU8(U16ToF32(BinaryPrimitives.ReadUInt16LittleEndian(input[8..])));
					byte g = F32ToU8(U16ToF32(BinaryPrimitives.ReadUInt16LittleEndian(input[10..])));
					byte b = F32ToU8(U16ToF32(BinaryPrimitives.ReadUInt16LittleEndian(input[12..])));
					byte a = F32ToU8(U16ToF32(BinaryPrimitives.ReadUInt16LittleEndian(input[14..])));
					output[..(blockWidth * blockHeight)].Fill(new ColorRGBA<byte>(r, g, b, a));
				}
				else
				{
					ColorRGBA<byte> c = new(input[9], input[11], input[13], input[15]);
					output[..(blockWidth * blockHeight)].Fill(c);
				}
			}
			else
			{
				BlockData blockData = new();
				blockData.bw = blockWidth;
				blockData.bh = blockHeight;
				DecodeBlockParameters(input, ref blockData);
				DecodeEndpoints(input, ref blockData);
				DecodeWeights(input, ref blockData);
				if (blockData.part_num > 1)
				{
					SelectPartition(input, ref blockData);
				}
				ApplicateColor(blockData, output);
			}
		}

		private static void DecodeBlockParameters(ReadOnlySpan<byte> input, ref BlockData pBlock)
		{
			pBlock.dual_plane = (input[1] & 4) >> 2;
			pBlock.weight_range = (input[0] >> 4 & 1) | (input[1] << 2 & 8);

			if ((input[0] & 3) != 0)
			{
				pBlock.weight_range |= input[0] << 1 & 6;
				switch (input[0] & 0xc)
				{
					case 0:
						pBlock.width = (BinaryPrimitives.ReadInt32LittleEndian(input) >> 7 & 3) + 4;
						pBlock.height = (input[0] >> 5 & 3) + 2;
						break;
					case 4:
						pBlock.width = (BinaryPrimitives.ReadInt32LittleEndian(input) >> 7 & 3) + 8;
						pBlock.height = (input[0] >> 5 & 3) + 2;
						break;
					case 8:
						pBlock.width = (input[0] >> 5 & 3) + 2;
						pBlock.height = (BinaryPrimitives.ReadInt32LittleEndian(input) >> 7 & 3) + 8;
						break;
					case 12:
						if ((input[1] & 1) != 0)
						{
							pBlock.width = (input[0] >> 7 & 1) + 2;
							pBlock.height = (input[0] >> 5 & 3) + 2;
						}
						else
						{
							pBlock.width = (input[0] >> 5 & 3) + 2;
							pBlock.height = (input[0] >> 7 & 1) + 6;
						}
						break;
				}
			}
			else
			{
				pBlock.weight_range |= input[0] >> 1 & 6;
				switch (BinaryPrimitives.ReadInt32LittleEndian(input) & 0x180)
				{
					case 0:
						pBlock.width = 12;
						pBlock.height = (input[0] >> 5 & 3) + 2;
						break;
					case 0x80:
						pBlock.width = (input[0] >> 5 & 3) + 2;
						pBlock.height = 12;
						break;
					case 0x100:
						pBlock.width = (input[0] >> 5 & 3) + 6;
						pBlock.height = (input[1] >> 1 & 3) + 6;
						pBlock.dual_plane = 0;
						pBlock.weight_range &= 7;
						break;
					case 0x180:
						pBlock.width = (input[0] & 0x20) != 0 ? 10 : 6;
						pBlock.height = (input[0] & 0x20) != 0 ? 6 : 10;
						break;
				}
			}

			pBlock.part_num = (input[1] >> 3 & 3) + 1;

			pBlock.weight_num = pBlock.width * pBlock.height;
			if (pBlock.dual_plane != 0)
			{
				pBlock.weight_num *= 2;
			}

			int config_bits, cem_base = 0;
			int weight_bits = WeightPrecTableA[pBlock.weight_range] switch
			{
				3 => pBlock.weight_num * WeightPrecTableB[pBlock.weight_range] + (pBlock.weight_num * 8 + 4) / 5,
				5 => pBlock.weight_num * WeightPrecTableB[pBlock.weight_range] + (pBlock.weight_num * 7 + 2) / 3,
				_ => pBlock.weight_num * WeightPrecTableB[pBlock.weight_range],
			};
			if (pBlock.part_num == 1)
			{
				pBlock.cem[0] = BinaryPrimitives.ReadInt32LittleEndian(input[1..]) >> 5 & 0xf;
				config_bits = 17;
			}
			else
			{
				cem_base = BinaryPrimitives.ReadInt32LittleEndian(input[2..]) >> 7 & 3;
				if (cem_base == 0)
				{
					int cem = input[3] >> 1 & 0xf;
					for (int i = 0; i < pBlock.part_num; i++)
					{
						pBlock.cem[i] = cem;
					}
					config_bits = 29;
				}
				else
				{
					for (int i = 0; i < pBlock.part_num; i++)
					{
						pBlock.cem[i] = ((input[3] >> (i + 1) & 1) + cem_base - 1) << 2;
					}
					switch (pBlock.part_num)
					{
						case 2:
							pBlock.cem[0] |= input[3] >> 3 & 3;
							pBlock.cem[1] |= GetBits(input, 126 - weight_bits, 2);
							break;
						case 3:
							pBlock.cem[0] |= input[3] >> 4 & 1;
							pBlock.cem[0] |= GetBits(input, 122 - weight_bits, 2) & 2;
							pBlock.cem[1] |= GetBits(input, 124 - weight_bits, 2);
							pBlock.cem[2] |= GetBits(input, 126 - weight_bits, 2);
							break;
						case 4:
							for (int i = 0; i < 4; i++)
							{
								pBlock.cem[i] |= GetBits(input, 120 + i * 2 - weight_bits, 2);
							}
							break;
					}
					config_bits = 25 + pBlock.part_num * 3;
				}
			}

			if (pBlock.dual_plane != 0)
			{
				config_bits += 2;
				pBlock.plane_selector = GetBits(input, cem_base != 0 ? 130 - weight_bits - pBlock.part_num * 3 : 126 - weight_bits, 2);
			}

			int remain_bits = 128 - config_bits - weight_bits;

			pBlock.endpoint_value_num = 0;
			for (int i = 0; i < pBlock.part_num; i++)
			{
				pBlock.endpoint_value_num += (pBlock.cem[i] >> 1 & 6) + 2;
			}

			for (int i = 0, endpoint_bits; i < CemTableA.Length; i++)
			{
				endpoint_bits = CemTableA[i] switch
				{
					3 => pBlock.endpoint_value_num * CemTableB[i] + (pBlock.endpoint_value_num * 8 + 4) / 5,
					5 => pBlock.endpoint_value_num * CemTableB[i] + (pBlock.endpoint_value_num * 7 + 2) / 3,
					_ => pBlock.endpoint_value_num * CemTableB[i],
				};
				if (endpoint_bits <= remain_bits)
				{
					pBlock.cem_range = i;
					break;
				}
			}
		}

		private static void DecodeEndpoints(ReadOnlySpan<byte> input, ref BlockData pBlock)
		{
			Span<IntSeqData> epSeq = stackalloc IntSeqData[32];
			DecodeIntseq(input, pBlock.part_num == 1 ? 17 : 29, CemTableA[pBlock.cem_range], CemTableB[pBlock.cem_range], pBlock.endpoint_value_num, false, epSeq);

			Span<int> ev = stackalloc int[32];
			switch (CemTableA[pBlock.cem_range])
			{
				case 3:
					for (int i = 0, b = 0, c = DETritsTable[CemTableB[pBlock.cem_range]]; i < pBlock.endpoint_value_num; i++)
					{
						int a = (epSeq[i].bits & 1) * 0x1ff;
						int x = epSeq[i].bits >> 1;
						switch (CemTableB[pBlock.cem_range])
						{
							case 1:
								b = 0;
								break;
							case 2:
								b = 0b100010110 * x;
								break;
							case 3:
								b = x << 7 | x << 2 | x;
								break;
							case 4:
								b = x << 6 | x;
								break;
							case 5:
								b = x << 5 | x >> 2;
								break;
							case 6:
								b = x << 4 | x >> 4;
								break;
						}
						ev[i] = (a & 0x80) | ((epSeq[i].nonbits * c + b) ^ a) >> 2;
					}
					break;

				case 5:
					for (int i = 0, b = 0, c = DEQuintsTable[CemTableB[pBlock.cem_range]]; i < pBlock.endpoint_value_num; i++)
					{
						int a = (epSeq[i].bits & 1) * 0x1ff;
						int x = epSeq[i].bits >> 1;
						switch (CemTableB[pBlock.cem_range])
						{
							case 1:
								b = 0;
								break;
							case 2:
								b = 0b100001100 * x;
								break;
							case 3:
								b = x << 7 | x << 1 | x >> 1;
								break;
							case 4:
								b = x << 6 | x >> 1;
								break;
							case 5:
								b = x << 5 | x >> 3;
								break;
						}
						ev[i] = (a & 0x80) | ((epSeq[i].nonbits * c + b) ^ a) >> 2;
					}
					break;

				default:
					switch (CemTableB[pBlock.cem_range])
					{
						case 1:
							for (int i = 0; i < pBlock.endpoint_value_num; i++)
							{
								ev[i] = epSeq[i].bits * 0xff;
							}
							break;
						case 2:
							for (int i = 0; i < pBlock.endpoint_value_num; i++)
							{
								ev[i] = epSeq[i].bits * 0x55;
							}
							break;
						case 3:
							for (int i = 0; i < pBlock.endpoint_value_num; i++)
							{
								ev[i] = epSeq[i].bits << 5 | epSeq[i].bits << 2 | epSeq[i].bits >> 1;
							}
							break;
						case 4:
							for (int i = 0; i < pBlock.endpoint_value_num; i++)
							{
								ev[i] = epSeq[i].bits << 4 | epSeq[i].bits;
							}
							break;
						case 5:
							for (int i = 0; i < pBlock.endpoint_value_num; i++)
							{
								ev[i] = epSeq[i].bits << 3 | epSeq[i].bits >> 2;
							}
							break;
						case 6:
							for (int i = 0; i < pBlock.endpoint_value_num; i++)
							{
								ev[i] = epSeq[i].bits << 2 | epSeq[i].bits >> 4;
							}
							break;
						case 7:
							for (int i = 0; i < pBlock.endpoint_value_num; i++)
							{
								ev[i] = epSeq[i].bits << 1 | epSeq[i].bits >> 6;
							}
							break;
						case 8:
							for (int i = 0; i < pBlock.endpoint_value_num; i++)
							{
								ev[i] = epSeq[i].bits;
							}
							break;
					}
					break;
			}

			Span<int> v = ev;
			for (int cem = 0; cem < pBlock.part_num; v = v.Slice((pBlock.cem[cem] / 4 + 1) * 2), cem++)
			{
				switch (pBlock.cem[cem])
				{
					case 0:
						SetEndpoint(pBlock.endpoints[cem], v[0], v[0], v[0], 255, v[1], v[1], v[1], 255);
						break;
					case 1:
							{
								int l0 = (v[0] >> 2) | (v[1] & 0xc0);
								int l1 = Clamp(l0 + (v[1] & 0x3f));
								SetEndpoint(pBlock.endpoints[cem], l0, l0, l0, 255, l1, l1, l1, 255);
							}
							break;
						case 2:
							// HDR 亮度：两个 12 位 HDR 端点差值编码
							if (v[0] <= v[1])
							{
								SetEndpoint(pBlock.endpoints[cem], v[0] << 4, v[0] << 4, v[0] << 4, 0x780, v[1] << 4, v[1] << 4, v[1] << 4, 0x780);
							}
							else
							{
								SetEndpoint(pBlock.endpoints[cem], (v[1] << 4) + 8, (v[1] << 4) + 8, (v[1] << 4) + 8, 0x780, (v[0] << 4) - 8, (v[0] << 4) - 8, (v[0] << 4) - 8, 0x780);
							}
							break;
						case 3:
							// HDR 亮度 + 偏移：高 2 位标志选择 10/12 位精度两种布局
							{
								int y0, d;
								if ((v[0] & 0x80) != 0)
								{
									y0 = ((v[1] & 0xe0) << 4) | ((v[0] & 0x7f) << 2);
									d = (v[1] & 0x1f) << 2;
								}
								else
								{
									y0 = ((v[1] & 0xf0) << 4) | ((v[0] & 0x7f) << 1);
									d = (v[1] & 0x0f) << 1;
								}
								int y1 = ClampHdr(y0 + d);
								SetEndpoint(pBlock.endpoints[cem], y0, y0, y0, 0x780, y1, y1, y1, 0x780);
							}
							break;
						case 4:
						SetEndpoint(pBlock.endpoints[cem], v[0], v[0], v[0], v[2], v[1], v[1], v[1], v[3]);
						break;
					case 5:
						BitTransferSigned(ref v[1], ref v[0]);
						BitTransferSigned(ref v[3], ref v[2]);
						v[1] += v[0];
						SetEndpointClamp(pBlock.endpoints[cem], v[0], v[0], v[0], v[2], v[1], v[1], v[1], v[2] + v[3]);
						break;
					case 6:
							SetEndpoint(pBlock.endpoints[cem], v[0] * v[3] >> 8, v[1] * v[3] >> 8, v[2] * v[3] >> 8, 255, v[0], v[1], v[2], 255);
							break;
						case 7:
							// HDR RGB：依据模式值重构 12 位 HDR 端点（含主分量重排与差值重建）
							DecodeEndpointsHdr7(pBlock.endpoints[cem], v);
							break;
						case 8:
						if (v[0] + v[2] + v[4] <= v[1] + v[3] + v[5])
						{
							SetEndpoint(pBlock.endpoints[cem], v[0], v[2], v[4], 255, v[1], v[3], v[5], 255);
						}
						else
						{
							SetEndpointBlue(pBlock.endpoints[cem], v[1], v[3], v[5], 255, v[0], v[2], v[4], 255);
						}

						break;
					case 9:
						BitTransferSigned(ref v[1], ref v[0]);
						BitTransferSigned(ref v[3], ref v[2]);
						BitTransferSigned(ref v[5], ref v[4]);
						if (v[1] + v[3] + v[5] >= 0)
						{
							SetEndpointClamp(pBlock.endpoints[cem], v[0], v[2], v[4], 255, v[0] + v[1], v[2] + v[3], v[4] + v[5], 255);
						}
						else
						{
							SetEndpointBlueClamp(pBlock.endpoints[cem], v[0] + v[1], v[2] + v[3], v[4] + v[5], 255, v[0], v[2], v[4], 255);
						}

						break;
					case 10:
							SetEndpoint(pBlock.endpoints[cem], v[0] * v[3] >> 8, v[1] * v[3] >> 8, v[2] * v[3] >> 8, v[4], v[0], v[1], v[2], v[5]);
							break;
						case 11:
							// HDR RGB：6 值差分编码（alpha 恒为不透明标记 0x780）
							DecodeEndpointsHdr11(pBlock.endpoints[cem], v, 0x780, 0x780);
							break;
						case 12:
						if (v[0] + v[2] + v[4] <= v[1] + v[3] + v[5])
						{
							SetEndpoint(pBlock.endpoints[cem], v[0], v[2], v[4], v[6], v[1], v[3], v[5], v[7]);
						}
						else
						{
							SetEndpointBlue(pBlock.endpoints[cem], v[1], v[3], v[5], v[7], v[0], v[2], v[4], v[6]);
						}

						break;
					case 13:
							BitTransferSigned(ref v[1], ref v[0]);
							BitTransferSigned(ref v[3], ref v[2]);
							BitTransferSigned(ref v[5], ref v[4]);
							BitTransferSigned(ref v[7], ref v[6]);
							if (v[1] + v[3] + v[5] >= 0)
							{
								SetEndpointClamp(pBlock.endpoints[cem], v[0], v[2], v[4], v[6], v[0] + v[1], v[2] + v[3], v[4] + v[5], v[6] + v[7]);
							}
							else
							{
								SetEndpointBlueClamp(pBlock.endpoints[cem], v[0] + v[1], v[2] + v[3], v[4] + v[5], v[6] + v[7], v[0], v[2], v[4], v[6]);
							}

							break;
						case 14:
							// HDR RGBA：6 值差分编码 + 两个显式 alpha 端点
							DecodeEndpointsHdr11(pBlock.endpoints[cem], v, v[6], v[7]);
							break;
						case 15:
							// HDR RGBA：alpha 也参与差分编码，mode 取自 alpha 值高位
							{
								int mode = ((v[6] >> 7) & 1) | ((v[7] >> 6) & 2);
								v[6] &= 0x7f;
								v[7] &= 0x7f;
								if (mode == 3)
								{
									DecodeEndpointsHdr11(pBlock.endpoints[cem], v, v[6] << 5, v[7] << 5);
								}
								else
								{
									v[6] |= (v[7] << (mode + 1)) & 0x780;
									// 符号扩展：mode 位宽下的最高位还原为负值
									v[7] = ((v[7] & (0x3f >> mode)) ^ (0x20 >> mode)) - (0x20 >> mode);
									v[6] <<= 4 - mode;
									v[7] <<= 4 - mode;
									DecodeEndpointsHdr11(pBlock.endpoints[cem], v, v[6], ClampHdr(v[6] + v[7]));
								}
							}
							break;
					}
				}
			}

		private static void DecodeWeights(ReadOnlySpan<byte> input, ref BlockData block)
		{
			Span<IntSeqData> wSeq = stackalloc IntSeqData[128];
			DecodeIntseq(input, 128, WeightPrecTableA[block.weight_range], WeightPrecTableB[block.weight_range], block.weight_num, true, wSeq);

			Span<int> wv = stackalloc int[128];
			if (WeightPrecTableA[block.weight_range] == 0)
			{
				switch (WeightPrecTableB[block.weight_range])
				{
					case 1:
						for (int i = 0; i < block.weight_num; i++)
						{
							wv[i] = wSeq[i].bits != 0 ? 63 : 0;
						}

						break;
					case 2:
						for (int i = 0; i < block.weight_num; i++)
						{
							wv[i] = wSeq[i].bits << 4 | wSeq[i].bits << 2 | wSeq[i].bits;
						}

						break;
					case 3:
						for (int i = 0; i < block.weight_num; i++)
						{
							wv[i] = wSeq[i].bits << 3 | wSeq[i].bits;
						}

						break;
					case 4:
						for (int i = 0; i < block.weight_num; i++)
						{
							wv[i] = wSeq[i].bits << 2 | wSeq[i].bits >> 2;
						}

						break;
					case 5:
						for (int i = 0; i < block.weight_num; i++)
						{
							wv[i] = wSeq[i].bits << 1 | wSeq[i].bits >> 4;
						}

						break;
				}
				for (int i = 0; i < block.weight_num; i++)
				{
					if (wv[i] > 32)
					{
						++wv[i];
					}
				}
			}
			else if (WeightPrecTableB[block.weight_range] == 0)
			{
				int s = WeightPrecTableA[block.weight_range] == 3 ? 32 : 16;
				for (int i = 0; i < block.weight_num; i++)
				{
					wv[i] = wSeq[i].nonbits * s;
				}
			}
			else
			{
				if (WeightPrecTableA[block.weight_range] == 3)
				{
					switch (WeightPrecTableB[block.weight_range])
					{
						case 1:
							for (int i = 0; i < block.weight_num; i++)
							{
								wv[i] = wSeq[i].nonbits * 50;
							}
							break;
						case 2:
							for (int i = 0; i < block.weight_num; i++)
							{
								wv[i] = wSeq[i].nonbits * 23;
								if ((wSeq[i].bits & 2) != 0)
								{
									wv[i] += 0b1000101;
								}
							}
							break;
						case 3:
							for (int i = 0; i < block.weight_num; i++)
							{
								wv[i] = wSeq[i].nonbits * 11 + ((wSeq[i].bits << 4 | wSeq[i].bits >> 1) & 0b1100011);
							}
							break;
					}
				}
				else if (WeightPrecTableA[block.weight_range] == 5)
				{
					switch (WeightPrecTableB[block.weight_range])
					{
						case 1:
							for (int i = 0; i < block.weight_num; i++)
							{
								wv[i] = wSeq[i].nonbits * 28;
							}

							break;
						case 2:
							for (int i = 0; i < block.weight_num; i++)
							{
								wv[i] = wSeq[i].nonbits * 13;
								if ((wSeq[i].bits & 2) != 0)
								{
									wv[i] += 0b1000010;
								}
							}
							break;
					}
				}
				for (int i = 0; i < block.weight_num; i++)
				{
					int a = (wSeq[i].bits & 1) * 0x7f;
					wv[i] = (a & 0x20) | ((wv[i] ^ a) >> 2);
					if (wv[i] > 32)
					{
						++wv[i];
					}
				}
			}

			int ds = (1024 + block.bw / 2) / (block.bw - 1);
			int dt = (1024 + block.bh / 2) / (block.bh - 1);
			int pn = block.dual_plane != 0 ? 2 : 1;

			for (int t = 0, i = 0; t < block.bh; t++)
			{
				for (int s = 0; s < block.bw; s++, i++)
				{
					int gs = (ds * s * (block.width - 1) + 32) >> 6;
					int gt = (dt * t * (block.height - 1) + 32) >> 6;
					int fs = gs & 0xf;
					int ft = gt & 0xf;
					int v = (gs >> 4) + (gt >> 4) * block.width;
					int w11 = (fs * ft + 8) >> 4;
					int w10 = ft - w11;
					int w01 = fs - w11;
					int w00 = 16 - fs - ft + w11;

					for (int p = 0; p < pn; p++)
					{
						int p00 = wv[v * pn + p];
						int p01 = wv[(v + 1) * pn + p];
						int p10 = wv[(v + block.width) * pn + p];
						int p11 = wv[(v + block.width + 1) * pn + p];
						block.weights[i * 2 + p] = (p00 * w00 + p01 * w01 + p10 * w10 + p11 * w11 + 8) >> 4;
					}
				}
			}
		}

		private static void SelectPartition(ReadOnlySpan<byte> input, ref BlockData block)
		{
			bool small_block = block.bw * block.bh < 31;
			int seed = (BinaryPrimitives.ReadInt32LittleEndian(input) >> 13 & 0x3ff) | (block.part_num - 1) << 10;

			uint rnum;
			unchecked
			{
				rnum = (uint)seed;
				rnum ^= rnum >> 15;
				rnum -= rnum << 17;
				rnum += rnum << 7;
				rnum += rnum << 4;
				rnum ^= rnum >> 5;
				rnum += rnum << 16;
				rnum ^= rnum >> 7;
				rnum ^= rnum >> 3;
				rnum ^= rnum << 6;
				rnum ^= rnum >> 17;
			}

			Span<int> seeds = stackalloc int[8];
			for (int i = 0; i < 8; i++)
			{
				seeds[i] = (int)((rnum >> (i * 4)) & 0xF);
				seeds[i] *= seeds[i];
			}


			ReadOnlySpan<int> sh =
			[
				(seed & 2) != 0 ? 4 : 5,
				block.part_num == 3 ? 6 : 5
			];

			if ((seed & 1) != 0)
			{
				for (int i = 0; i < 8; i++)
				{
					seeds[i] >>= sh[i % 2];
				}
			}
			else
			{
				for (int i = 0; i < 8; i++)
				{
					seeds[i] >>= sh[1 - i % 2];
				}
			}

			if (small_block)
			{
				for (int t = 0, i = 0; t < block.bh; t++)
				{
					for (int s = 0; s < block.bw; s++, i++)
					{
						int x = s << 1;
						int y = t << 1;
						int a = (int)((seeds[0] * x + seeds[1] * y + (rnum >> 14)) & 0x3f);
						int b = (int)((seeds[2] * x + seeds[3] * y + (rnum >> 10)) & 0x3f);
						int c = (int)(block.part_num < 3 ? 0 : (seeds[4] * x + seeds[5] * y + (rnum >> 6)) & 0x3f);
						int d = (int)(block.part_num < 4 ? 0 : (seeds[6] * x + seeds[7] * y + (rnum >> 2)) & 0x3f);
						block.partition[i] = (a >= b && a >= c && a >= d) ? 0 : (b >= c && b >= d) ? 1 : (c >= d) ? 2 : 3;
					}
				}
			}
			else
			{
				for (int y = 0, i = 0; y < block.bh; y++)
				{
					for (int x = 0; x < block.bw; x++, i++)
					{
						int a = (int)((seeds[0] * x + seeds[1] * y + (rnum >> 14)) & 0x3f);
						int b = (int)((seeds[2] * x + seeds[3] * y + (rnum >> 10)) & 0x3f);
						int c = (int)(block.part_num < 3 ? 0 : (seeds[4] * x + seeds[5] * y + (rnum >> 6)) & 0x3f);
						int d = (int)(block.part_num < 4 ? 0 : (seeds[6] * x + seeds[7] * y + (rnum >> 2)) & 0x3f);
						block.partition[i] = (a >= b && a >= c && a >= d) ? 0 : (b >= c && b >= d) ? 1 : (c >= d) ? 2 : 3;
					}
				}
			}
		}

		private static void ApplicateColor(BlockData block, Span<ColorRGBA<byte>> output)
		{
			if (block.dual_plane != 0)
			{
				Span<int> ps = [0, 0, 0, 0];
				ps[block.plane_selector] = 1;
				if (block.part_num > 1)
				{
					for (int i = 0; i < block.bw * block.bh; i++)
					{
						int p = block.partition[i];
						byte r = SelectColorByCem(block.cem[p], block.endpoints[p][0], block.endpoints[p][4], block.weights[i * 2 + ps[0]]);
						byte g = SelectColorByCem(block.cem[p], block.endpoints[p][1], block.endpoints[p][5], block.weights[i * 2 + ps[1]]);
						byte b = SelectColorByCem(block.cem[p], block.endpoints[p][2], block.endpoints[p][6], block.weights[i * 2 + ps[2]]);
						byte a = SelectAlphaByCem(block.cem[p], block.endpoints[p][3], block.endpoints[p][7], block.weights[i * 2 + ps[3]]);
						output[i] = new(r, g, b, a);
					}
				}
				else
				{
					for (int i = 0; i < block.bw * block.bh; i++)
					{
						byte r = SelectColorByCem(block.cem[0], block.endpoints[0][0], block.endpoints[0][4], block.weights[i * 2 + ps[0]]);
						byte g = SelectColorByCem(block.cem[0], block.endpoints[0][1], block.endpoints[0][5], block.weights[i * 2 + ps[1]]);
						byte b = SelectColorByCem(block.cem[0], block.endpoints[0][2], block.endpoints[0][6], block.weights[i * 2 + ps[2]]);
						byte a = SelectAlphaByCem(block.cem[0], block.endpoints[0][3], block.endpoints[0][7], block.weights[i * 2 + ps[3]]);
						output[i] = new(r, g, b, a);
					}
				}
			}
			else if (block.part_num > 1)
			{
				for (int i = 0; i < block.bw * block.bh; i++)
				{
					int p = block.partition[i];
					byte r = SelectColorByCem(block.cem[p], block.endpoints[p][0], block.endpoints[p][4], block.weights[i * 2]);
					byte g = SelectColorByCem(block.cem[p], block.endpoints[p][1], block.endpoints[p][5], block.weights[i * 2]);
					byte b = SelectColorByCem(block.cem[p], block.endpoints[p][2], block.endpoints[p][6], block.weights[i * 2]);
					byte a = SelectAlphaByCem(block.cem[p], block.endpoints[p][3], block.endpoints[p][7], block.weights[i * 2]);
					output[i] = new(r, g, b, a);
				}
			}
			else
			{
				for (int i = 0; i < block.bw * block.bh; i++)
				{
					byte r = SelectColorByCem(block.cem[0], block.endpoints[0][0], block.endpoints[0][4], block.weights[i * 2]);
					byte g = SelectColorByCem(block.cem[0], block.endpoints[0][1], block.endpoints[0][5], block.weights[i * 2]);
					byte b = SelectColorByCem(block.cem[0], block.endpoints[0][2], block.endpoints[0][6], block.weights[i * 2]);
					byte a = SelectAlphaByCem(block.cem[0], block.endpoints[0][3], block.endpoints[0][7], block.weights[i * 2]);
					output[i] = new(r, g, b, a);
				}
			}
		}

		private static void DecodeIntseq(ReadOnlySpan<byte> input, int offset, int a, int b, int count, bool reverse, Span<IntSeqData> _out)
		{
			if (count <= 0)
			{
				return;
			}

			int n = 0;

			if (a == 3)
			{
				int mask = (1 << b) - 1;
				int block_count = (count + 4) / 5;
				int last_block_count = (count + 4) % 5 + 1;
				int block_size = 8 + 5 * b;
				int last_block_size = (block_size * last_block_count + 4) / 5;

				if (reverse)
				{
					for (int i = 0, p = offset; i < block_count; i++, p -= block_size)
					{
						int now_size = (i < block_count - 1) ? block_size : last_block_size;
						ulong d = BitReverseU64(GetBits64(input, p - now_size, now_size), now_size);
						int x = (int)((d >> b & 3) | (d >> b * 2 & 0xc) | (d >> b * 3 & 0x10) | (d >> b * 4 & 0x60) | (d >> b * 5 & 0x80));
						for (int j = 0; j < 5 && n < count; j++, n++)
						{
							_out[n] = new IntSeqData()
							{
								bits = (int)(d >> (DImt[j] + b * j)) & mask,
								nonbits = DITritsTable[j * 256 + x],
							};
						}
					}
				}
				else
				{
					for (int i = 0, p = offset; i < block_count; i++, p += block_size)
					{
						ulong d = GetBits64(input, p, (i < block_count - 1) ? block_size : last_block_size);
						int x = (int)((d >> b & 3) | (d >> b * 2 & 0xc) | (d >> b * 3 & 0x10) | (d >> b * 4 & 0x60) | (d >> b * 5 & 0x80));
						for (int j = 0; j < 5 && n < count; j++, n++)
						{
							_out[n] = new IntSeqData()
							{
								bits = unchecked((int)(d >> (DImt[j] + b * j))) & mask,
								nonbits = DITritsTable[j * 256 + x],
							};
						}
					}
				}
			}
			else if (a == 5)
			{
				int mask = (1 << b) - 1;
				int block_count = (count + 2) / 3;
				int last_block_count = (count + 2) % 3 + 1;
				int block_size = 7 + 3 * b;
				int last_block_size = (block_size * last_block_count + 2) / 3;

				if (reverse)
				{
					for (int i = 0, p = offset; i < block_count; i++, p -= block_size)
					{
						int now_size = (i < block_count - 1) ? block_size : last_block_size;
						ulong d = BitReverseU64(GetBits64(input, p - now_size, now_size), now_size);
						int x = (int)((d >> b & 7) | (d >> b * 2 & 0x18) | (d >> b * 3 & 0x60));
						for (int j = 0; j < 3 && n < count; j++, n++)
						{
							_out[n] = new IntSeqData()
							{
								bits = (int)d >> (DImq[j] + b * j) & mask,
								nonbits = DIQuintsTable[j * 128 + x],
							};
						}
					}
				}
				else
				{
					for (int i = 0, p = offset; i < block_count; i++, p += block_size)
					{
						ulong d = GetBits64(input, p, (i < block_count - 1) ? block_size : last_block_size);
						int x = (int)((d >> b & 7) | (d >> b * 2 & 0x18) | (d >> b * 3 & 0x60));
						for (int j = 0; j < 3 && n < count; j++, n++)
						{
							_out[n] = new IntSeqData()
							{
								bits = (int)d >> (DImq[j] + b * j) & mask,
								nonbits = DIQuintsTable[j * 128 + x],
							};
						}
					}
				}
			}
			else
			{
				if (reverse)
				{
					for (int p = offset - b; n < count; n++, p -= b)
					{
						_out[n] = new IntSeqData()
						{
							bits = BitReverseU8((byte)GetBits(input, p, b), b),
							nonbits = 0,
						};
					}
				}
				else
				{
					for (int p = offset; n < count; n++, p += b)
					{
						_out[n] = new IntSeqData()
						{
							bits = GetBits(input, p, b),
							nonbits = 0,
						};
					}
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		private static byte BitReverseU8(byte c, int bits)
		{
			return (byte)(BitReverseTable[c] >> (8 - bits));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		private static ulong BitReverseU64(ulong d, int bits)
		{
			ulong ret;
			unchecked
			{
				ret =
					(ulong)BitReverseTable[(int)(d >> 0 & 0xff)] << 56 |
					(ulong)BitReverseTable[(int)(d >> 8 & 0xff)] << 48 |
					(ulong)BitReverseTable[(int)(d >> 16 & 0xff)] << 40 |
					(ulong)BitReverseTable[(int)(d >> 24 & 0xff)] << 32 |
					(ulong)BitReverseTable[(int)(d >> 32 & 0xff)] << 24 |
					(ulong)BitReverseTable[(int)(d >> 40 & 0xff)] << 16 |
					(ulong)BitReverseTable[(int)(d >> 48 & 0xff)] << 8 |
					(ulong)BitReverseTable[(int)(d >> 56 & 0xff)];
			}
			return ret >> (64 - bits);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		private static int GetBits(ReadOnlySpan<byte> input, int bit, int len)
		{
			if (bit < 0 || input.Length * 8 < bit + len)
			{
				// Invalid input, but we should return something instead of throwing an exception
				return default;
			}
			ReadOnlySpan<byte> slice = input[(bit / 8)..];
			int bitBuffer;
			if (slice.Length >= sizeof(int))
			{
				bitBuffer = BinaryPrimitives.ReadInt32LittleEndian(slice);
			}
			else
			{
				Span<byte> temp = stackalloc byte[sizeof(int)];
				slice.CopyTo(temp);
				bitBuffer = BinaryPrimitives.ReadInt32LittleEndian(temp);
			}
			return (bitBuffer >> (bit % 8)) & ((1 << len) - 1);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		private static ulong GetBits64(ReadOnlySpan<byte> input, int bit, int len)
		{
			ulong mask = len == 64 ? 0xffffffffffffffff : (1UL << len) - 1;
			if (len < 1)
			{
				return 0;
			}
			else if (bit >= 64)
			{
				return BinaryPrimitives.ReadUInt64LittleEndian(input[sizeof(ulong)..]) >> (bit - 64) & mask;
			}
			else if (bit <= 0)
			{
				return BinaryPrimitives.ReadUInt64LittleEndian(input) << -bit & mask;
			}
			else if (bit + len <= 64)
			{
				return BinaryPrimitives.ReadUInt64LittleEndian(input) >> bit & mask;
			}
			else
			{
				return (BinaryPrimitives.ReadUInt64LittleEndian(input) >> bit | BinaryPrimitives.ReadUInt64LittleEndian(input[sizeof(ulong)..]) << (64 - bit)) & mask;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		private static byte Clamp(int n)
		{
			return n < 0 ? byte.MinValue : n > 255 ? byte.MaxValue : (byte)n;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		private static void BitTransferSigned(ref int a, ref int b)
		{
			b = (b >> 1) | (a & 0x80);
			a = (a >> 1) & 0x3f;
			if ((a & 0x20) != 0)
			{
				a -= 0x40;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		private static void SetEndpoint(Span<int> endpoint, int r1, int g1, int b1, int a1, int r2, int g2, int b2, int a2)
		{
			endpoint[0] = r1;
			endpoint[1] = g1;
			endpoint[2] = b1;
			endpoint[3] = a1;
			endpoint[4] = r2;
			endpoint[5] = g2;
			endpoint[6] = b2;
			endpoint[7] = a2;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		private static void SetEndpointClamp(Span<int> endpoint, int r1, int g1, int b1, int a1, int r2, int g2, int b2, int a2)
		{
			endpoint[0] = Clamp(r1);
			endpoint[1] = Clamp(g1);
			endpoint[2] = Clamp(b1);
			endpoint[3] = Clamp(a1);
			endpoint[4] = Clamp(r2);
			endpoint[5] = Clamp(g2);
			endpoint[6] = Clamp(b2);
			endpoint[7] = Clamp(a2);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		private static void SetEndpointBlue(Span<int> endpoint, int r1, int g1, int b1, int a1, int r2, int g2, int b2, int a2)
		{
			endpoint[0] = (r1 + b1) >> 1;
			endpoint[1] = (g1 + b1) >> 1;
			endpoint[2] = b1;
			endpoint[3] = a1;
			endpoint[4] = (r2 + b2) >> 1;
			endpoint[5] = (g2 + b2) >> 1;
			endpoint[6] = b2;
			endpoint[7] = a2;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		private static void SetEndpointBlueClamp(Span<int> endpoint, int r1, int g1, int b1, int a1, int r2, int g2, int b2, int a2)
		{
			endpoint[0] = Clamp((r1 + b1) >> 1);
			endpoint[1] = Clamp((g1 + b1) >> 1);
			endpoint[2] = Clamp(b1);
			endpoint[3] = Clamp(a1);
			endpoint[4] = Clamp((r2 + b2) >> 1);
			endpoint[5] = Clamp((g2 + b2) >> 1);
			endpoint[6] = Clamp(b2);
			endpoint[7] = Clamp(a2);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		private static byte SelectColor(int v0, int v1, int weight)
		{
			const int SignAdjustment = short.MaxValue + 1;
			const int MaxValue = ushort.MaxValue + 1;
			return (byte)(((((v0 << 8 | v0) * (64 - weight) + (v1 << 8 | v1) * weight + 32) >> 6) * byte.MaxValue + SignAdjustment) / MaxValue);
		}

		/// <summary>HDR 端点插值：结果按 12 位端点编码的"自定义半精度浮点"重新解释后映射回 8 位。</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		private static byte SelectColorHdr(int v0, int v1, int weight)
		{
			// (v0<<4)*(64-w)+(v1<<4)*w 与参考实现一致：v0/v1 已带 <<4 缩放，权重再按 64 加权；
			// 先右移 6 除权，再截断到 16 位得到 12 位浮点编码 c
			uint c = (uint)(((v0 << 4) * (64 - weight) + (v1 << 4) * weight + 32) >> 6) & 0xFFFF;

			// 12 位专用编码 → 标准 fp16 中间值：尾数扩展表把 c 的低 11 位映射到 10 位尾数
			uint m = c & 0x7FF;
			if (m < 0x200)
			{
				m *= 3;
			}
			else if (m < 0x600)
			{
				m = 4 * m - 0x200;
			}
			else
			{
				m = 5 * m - 0x800;
			}

			float f = U16ToF32((ushort)(((c >> 1) & 0x7C00) | (m >> 3)));
			return float.IsFinite(f) ? ClampColorByte(f) : byte.MaxValue;
		}

		/// <summary>按颜色端点模式选择插值函数：HDR 模式（2/3/7/11/14/15）走浮点插值。</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		private static byte SelectColorByCem(int cem, int v0, int v1, int weight)
		{
			return cem is 2 or 3 or 7 or 11 or 14 or 15 ? SelectColorHdr(v0, v1, weight) : SelectColor(v0, v1, weight);
		}

		/// <summary>alpha 通道的插值函数选取：HDR 模式 14 的 alpha 恒为 LDR（不透明/普通），15 才走 HDR。</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		private static byte SelectAlphaByCem(int cem, int v0, int v1, int weight)
		{
			return cem is 2 or 3 or 7 or 11 or 15 ? SelectColorHdr(v0, v1, weight) : SelectColor(v0, v1, weight);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		private static int ClampHdr(int n)
		{
			return n < 0 ? 0 : n > 0xFFF ? 0xFFF : n;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		private static byte ClampColorByte(float f)
		{
			double c = Math.Round(f * 255.0, MidpointRounding.AwayFromZero);
			return c < 0 ? byte.MinValue : c > byte.MaxValue ? byte.MaxValue : (byte)c;
		}

		/// <summary>半精度浮点（fp16）→ 单精度。实现参考 ASTC 规范附录的 LNS 解码位运算。</summary>
		private static float U16ToF32(ushort h)
		{
			uint w = (uint)h << 16;
			uint sign = w & 0x8000_0000u;
			uint ww = w + w;
			uint expOffset = 0xE0u << 23;
			float normalizedValue = BitConverter.UInt32BitsToSingle((ww >> 4) | expOffset) * ExpScaleDown;
			uint magicMask = 126u << 23;
			const float MagicBias = 0.5f;
			float denormalizedValue = BitConverter.UInt32BitsToSingle((ww >> 17) | magicMask) - MagicBias;
			const uint DenormalizedCutoff = 1u << 27;
			uint result = ww < DenormalizedCutoff
				? BitConverter.SingleToUInt32Bits(denormalizedValue)
				: BitConverter.SingleToUInt32Bits(normalizedValue);
			return BitConverter.UInt32BitsToSingle(result | sign);
		}

		/// <summary>2^-112（fp16 LNS 解码的归一化缩放因子，位形 0x07800000）。</summary>
		private static readonly float ExpScaleDown = BitConverter.UInt32BitsToSingle(0x0780_0000u);

		/// <summary>把超出 LDR 范围的值钳到 [0, 255] 并取整到字节（HDR 高光按此截断）。</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		private static byte F32ToU8(float f)
		{
			double c = Math.Round(f * 255.0, MidpointRounding.AwayFromZero);
			return c < 0 ? byte.MinValue : c > byte.MaxValue ? byte.MaxValue : (byte)c;
		}

		/// <summary>HDR 端点写入：全部按 12 位 HDR 范围钳制（alpha 部分通常为不透明标记 0x780）。</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		private static void SetEndpointHdrClamp(Span<int> endpoint, int r1, int g1, int b1, int a1, int r2, int g2, int b2, int a2)
		{
			endpoint[0] = ClampHdr(r1);
			endpoint[1] = ClampHdr(g1);
			endpoint[2] = ClampHdr(b1);
			endpoint[3] = ClampHdr(a1);
			endpoint[4] = ClampHdr(r2);
			endpoint[5] = ClampHdr(g2);
			endpoint[6] = ClampHdr(b2);
			endpoint[7] = ClampHdr(a2);
		}

		/// <summary>
		/// HDR RGB（CEM 7）：3 值 + 主分量模式，重排后重建 12 位 HDR 端点对。
		/// 实现与 ASTC 规范 HDR 端点解码一致（v[0..3] 为反量化后的 16 位 token 低 8 位）。
		/// </summary>
		private static void DecodeEndpointsHdr7(Span<int> endpoint, ReadOnlySpan<int> v)
		{
			int modeValue = ((v[2] >> 4) & 0x8) | ((v[1] >> 5) & 0x4) | (v[0] >> 6);
			int majorComponent;
			int mode;
			if ((modeValue & 0xC) != 0xC)
			{
				majorComponent = modeValue >> 2;
				mode = modeValue & 3;
			}
			else if (modeValue != 0xF)
			{
				majorComponent = modeValue & 3;
				mode = 4;
			}
			else
			{
				majorComponent = 0;
				mode = 5;
			}

			Span<int> c = stackalloc int[4];
			c[0] = v[0] & 0x3f;
			c[1] = v[1] & 0x1f;
			c[2] = v[2] & 0x1f;
			c[3] = v[3] & 0x1f;

			switch (mode)
			{
				case 0:
					c[3] |= v[3] & 0x60;
					c[0] |= (v[3] >> 1) & 0x40;
					c[0] |= (v[2] << 1) & 0x80;
					c[0] |= (v[1] << 3) & 0x300;
					c[0] |= (v[2] << 5) & 0x400;
					break;
				case 1:
					c[1] |= v[1] & 0x20;
					c[2] |= v[2] & 0x20;
					c[0] |= (v[3] >> 1) & 0x40;
					c[0] |= (v[2] << 1) & 0x80;
					c[0] |= (v[1] << 2) & 0x100;
					c[0] |= (v[3] << 4) & 0x600;
					break;
				case 2:
					c[3] |= v[3] & 0xE0;
					c[0] |= (v[2] << 1) & 0xC0;
					c[0] |= (v[1] << 3) & 0x300;
					break;
				case 3:
					c[1] |= v[1] & 0x20;
					c[2] |= v[2] & 0x20;
					c[3] |= v[3] & 0x60;
					c[0] |= (v[3] >> 1) & 0x40;
					c[0] |= (v[2] << 1) & 0x80;
					c[0] |= (v[1] << 2) & 0x100;
					break;
				case 4:
					c[1] |= v[1] & 0x60;
					c[2] |= v[2] & 0x60;
					c[3] |= v[3] & 0x20;
					c[0] |= (v[3] >> 1) & 0x40;
					c[0] |= (v[3] << 1) & 0x80;
					break;
				case 5:
					c[1] |= v[1] & 0x60;
					c[2] |= v[2] & 0x60;
					c[3] |= v[3] & 0x60;
					c[0] |= (v[3] >> 1) & 0x40;
					break;
			}

			// 每个模式把端点左移到其位精度对应的高位（模式 0/1→1 位、2→2 位…5→5 位）
			for (int i = 0; i < 4; i++)
			{
				c[i] <<= mode is 0 or 1 ? 1 : mode;
			}

			if (mode != 5)
			{
				c[1] = c[0] - c[1];
				c[2] = c[0] - c[2];
			}

			if (majorComponent == 1)
			{
				SetEndpointHdrClamp(endpoint, c[1] - c[3], c[0] - c[3], c[2] - c[3], 0x780, c[1], c[0], c[2], 0x780);
			}
			else if (majorComponent == 2)
			{
				SetEndpointHdrClamp(endpoint, c[2] - c[3], c[1] - c[3], c[0] - c[3], 0x780, c[2], c[1], c[0], 0x780);
			}
			else
			{
				SetEndpointHdrClamp(endpoint, c[0] - c[3], c[1] - c[3], c[2] - c[3], 0x780, c[0], c[1], c[2], 0x780);
			}
		}

		/// <summary>
		/// HDR RGB/RGBA（CEM 11/14/15）：6 值差分解码。va/vb/vc 拼出基线端点，
		/// vd0/vd1 按模式位宽做符号扩展后作为色差偏移；主分量决定通道重排。
		/// </summary>
		private static void DecodeEndpointsHdr11(Span<int> endpoint, ReadOnlySpan<int> v, int alpha1, int alpha2)
		{
			int majorComponent = (v[4] >> 7) | ((v[5] >> 6) & 2);
			if (majorComponent == 3)
			{
				// 主分量=3 是特殊直通布局：rgb 直接取自 v0/v1、v2/v3、v4/v5
				SetEndpoint(endpoint, v[0] << 4, v[2] << 4, (v[4] << 5) & 0xFE0, alpha1, v[1] << 4, v[3] << 4, (v[5] << 5) & 0xFE0, alpha2);
				return;
			}

			int mode = (v[1] >> 7) | ((v[2] >> 6) & 2) | ((v[3] >> 5) & 4);
			int va = v[0] | ((v[1] << 2) & 0x100);
			int vb0 = v[2] & 0x3f;
			int vb1 = v[3] & 0x3f;
			int vc = v[1] & 0x3f;

			// vd0/vd1 按模式位宽（7/6/5）读字段并各自从自己的最高有效位做符号扩展
			int vd0, vd1;
			switch (mode)
			{
				case 0:
				case 2:
					vd0 = v[4] & 0x7f;
					if ((vd0 & 0x40) != 0)
					{
						vd0 |= unchecked((int)0xFFFFFF80);
					}
					vd1 = v[5] & 0x7f;
					if ((vd1 & 0x40) != 0)
					{
						vd1 |= unchecked((int)0xFFFFFF80);
					}
					break;
				case 1:
				case 3:
				case 5:
				case 7:
					vd0 = v[4] & 0x3f;
					if ((vd0 & 0x20) != 0)
					{
						vd0 |= unchecked((int)0xFFFFFFC0);
					}
					vd1 = v[5] & 0x3f;
					if ((vd1 & 0x20) != 0)
					{
						vd1 |= unchecked((int)0xFFFFFFC0);
					}
					break;
				default:
					vd0 = v[4] & 0x1f;
					if ((vd0 & 0x10) != 0)
					{
						vd0 |= unchecked((int)0xFFFFFFE0);
					}
					vd1 = v[5] & 0x1f;
					if ((vd1 & 0x10) != 0)
					{
						vd1 |= unchecked((int)0xFFFFFFE0);
					}
					break;
			}

			// 按模式补充分散在高位的标志位
			switch (mode)
			{
				case 0:
					vb0 |= v[2] & 0x40;
					vb1 |= v[3] & 0x40;
					break;
				case 1:
					vb0 |= (v[2] & 0x40) | ((v[4] << 1) & 0x80);
					vb1 |= (v[3] & 0x40) | ((v[5] << 1) & 0x80);
					break;
				case 2:
					va |= (v[2] << 3) & 0x200;
					vc |= v[3] & 0x40;
					break;
				case 3:
					va |= (v[4] << 3) & 0x200;
					vc |= v[5] & 0x40;
					vb0 |= v[2] & 0x40;
					vb1 |= v[3] & 0x40;
					break;
				case 4:
					va |= ((v[4] << 4) & 0x200) | ((v[5] << 5) & 0x400);
					vb0 |= (v[2] & 0x40) | ((v[4] << 1) & 0x80);
					vb1 |= (v[3] & 0x40) | ((v[5] << 1) & 0x80);
					break;
				case 5:
					va |= ((v[2] << 3) & 0x200) | ((v[3] << 4) & 0x400);
					vc |= (v[5] & 0x40) | ((v[4] << 1) & 0x80);
					break;
				case 6:
					va |= ((v[4] << 4) & 0x200) | ((v[5] << 5) & 0x400) | ((v[4] << 5) & 0x800);
					vc |= v[5] & 0x40;
					vb0 |= v[2] & 0x40;
					vb1 |= v[3] & 0x40;
					break;
				case 7:
					va |= ((v[2] << 3) & 0x200) | ((v[3] << 4) & 0x400) | ((v[4] << 5) & 0x800);
					vc |= v[5] & 0x40;
					break;
			}

			int shamt = (mode >> 1) ^ 3;
			va <<= shamt;
			vb0 <<= shamt;
			vb1 <<= shamt;
			vc <<= shamt;
			int mult = 1 << shamt;
			vd0 *= mult;
			vd1 *= mult;

			if (majorComponent == 1)
			{
				SetEndpointHdrClamp(endpoint, va - vb0 - vc - vd0, va - vc, va - vb1 - vc - vd1, alpha1, va - vb0, va, va - vb1, alpha2);
			}
			else if (majorComponent == 2)
			{
				SetEndpointHdrClamp(endpoint, va - vb1 - vc - vd1, va - vb0 - vc - vd0, va - vc, alpha1, va - vb1, va - vb0, va, alpha2);
			}
			else
			{
				SetEndpointHdrClamp(endpoint, va - vc, va - vb0 - vc - vd0, va - vb1 - vc - vd1, alpha1, va, va - vb0, va - vb1, alpha2);
			}
		}

		private static ReadOnlySpan<byte> BitReverseTable =>
		[
			0x00, 0x80, 0x40, 0xC0, 0x20, 0xA0, 0x60, 0xE0, 0x10, 0x90, 0x50, 0xD0, 0x30, 0xB0, 0x70, 0xF0,
			0x08, 0x88, 0x48, 0xC8, 0x28, 0xA8, 0x68, 0xE8, 0x18, 0x98, 0x58, 0xD8, 0x38, 0xB8, 0x78, 0xF8,
			0x04, 0x84, 0x44, 0xC4, 0x24, 0xA4, 0x64, 0xE4, 0x14, 0x94, 0x54, 0xD4, 0x34, 0xB4, 0x74, 0xF4,
			0x0C, 0x8C, 0x4C, 0xCC, 0x2C, 0xAC, 0x6C, 0xEC, 0x1C, 0x9C, 0x5C, 0xDC, 0x3C, 0xBC, 0x7C, 0xFC,
			0x02, 0x82, 0x42, 0xC2, 0x22, 0xA2, 0x62, 0xE2, 0x12, 0x92, 0x52, 0xD2, 0x32, 0xB2, 0x72, 0xF2,
			0x0A, 0x8A, 0x4A, 0xCA, 0x2A, 0xAA, 0x6A, 0xEA, 0x1A, 0x9A, 0x5A, 0xDA, 0x3A, 0xBA, 0x7A, 0xFA,
			0x06, 0x86, 0x46, 0xC6, 0x26, 0xA6, 0x66, 0xE6, 0x16, 0x96, 0x56, 0xD6, 0x36, 0xB6, 0x76, 0xF6,
			0x0E, 0x8E, 0x4E, 0xCE, 0x2E, 0xAE, 0x6E, 0xEE, 0x1E, 0x9E, 0x5E, 0xDE, 0x3E, 0xBE, 0x7E, 0xFE,
			0x01, 0x81, 0x41, 0xC1, 0x21, 0xA1, 0x61, 0xE1, 0x11, 0x91, 0x51, 0xD1, 0x31, 0xB1, 0x71, 0xF1,
			0x09, 0x89, 0x49, 0xC9, 0x29, 0xA9, 0x69, 0xE9, 0x19, 0x99, 0x59, 0xD9, 0x39, 0xB9, 0x79, 0xF9,
			0x05, 0x85, 0x45, 0xC5, 0x25, 0xA5, 0x65, 0xE5, 0x15, 0x95, 0x55, 0xD5, 0x35, 0xB5, 0x75, 0xF5,
			0x0D, 0x8D, 0x4D, 0xCD, 0x2D, 0xAD, 0x6D, 0xED, 0x1D, 0x9D, 0x5D, 0xDD, 0x3D, 0xBD, 0x7D, 0xFD,
			0x03, 0x83, 0x43, 0xC3, 0x23, 0xA3, 0x63, 0xE3, 0x13, 0x93, 0x53, 0xD3, 0x33, 0xB3, 0x73, 0xF3,
			0x0B, 0x8B, 0x4B, 0xCB, 0x2B, 0xAB, 0x6B, 0xEB, 0x1B, 0x9B, 0x5B, 0xDB, 0x3B, 0xBB, 0x7B, 0xFB,
			0x07, 0x87, 0x47, 0xC7, 0x27, 0xA7, 0x67, 0xE7, 0x17, 0x97, 0x57, 0xD7, 0x37, 0xB7, 0x77, 0xF7,
			0x0F, 0x8F, 0x4F, 0xCF, 0x2F, 0xAF, 0x6F, 0xEF, 0x1F, 0x9F, 0x5F, 0xDF, 0x3F, 0xBF, 0x7F, 0xFF
		];

		private static ReadOnlySpan<int> WeightPrecTableA => [0, 0, 0, 3, 0, 5, 3, 0, 0, 0, 5, 3, 0, 5, 3, 0];
		private static ReadOnlySpan<int> WeightPrecTableB => [0, 0, 1, 0, 2, 0, 1, 3, 0, 0, 1, 2, 4, 2, 3, 5];

		private static ReadOnlySpan<int> CemTableA => [0, 3, 5, 0, 3, 5, 0, 3, 5, 0, 3, 5, 0, 3, 5, 0, 3, 0, 0];
		private static ReadOnlySpan<int> CemTableB => [8, 6, 5, 7, 5, 4, 6, 4, 3, 5, 3, 2, 4, 2, 1, 3, 1, 2, 1];

		private static ReadOnlySpan<int> DImt => [0, 2, 4, 5, 7];
		private static ReadOnlySpan<int> DImq => [0, 3, 5];
		private static ReadOnlySpan<int> DITritsTable =>
		[
			0, 1, 2, 0, 0, 1, 2, 1, 0, 1, 2, 2, 0, 1, 2, 2, 0, 1, 2, 0, 0, 1, 2, 1, 0, 1, 2, 2, 0, 1, 2, 0, 0, 1, 2, 0, 0, 1, 2, 1, 0, 1, 2, 2, 0, 1, 2, 2, 0, 1, 2, 0, 0, 1, 2, 1, 0, 1, 2, 2, 0, 1, 2, 1, 0, 1, 2, 0, 0, 1, 2, 1, 0, 1, 2, 2, 0, 1, 2, 2, 0, 1, 2, 0, 0, 1, 2, 1, 0, 1, 2, 2, 0, 1, 2, 2, 0, 1, 2, 0, 0, 1, 2, 1, 0, 1, 2, 2, 0, 1, 2, 2, 0, 1, 2, 0, 0, 1, 2, 1, 0, 1, 2, 2, 0, 1, 2, 2, 0, 1, 2, 0, 0, 1, 2, 1, 0, 1, 2, 2, 0, 1, 2, 2, 0, 1, 2, 0, 0, 1, 2, 1, 0, 1, 2, 2, 0, 1, 2, 0, 0, 1, 2, 0, 0, 1, 2, 1, 0, 1, 2, 2, 0, 1, 2, 2, 0, 1, 2, 0, 0, 1, 2, 1, 0, 1, 2, 2, 0, 1, 2, 1, 0, 1, 2, 0, 0, 1, 2, 1, 0, 1, 2, 2, 0, 1, 2, 2, 0, 1, 2, 0, 0, 1, 2, 1, 0, 1, 2, 2, 0, 1, 2, 2, 0, 1, 2, 0, 0, 1, 2, 1, 0, 1, 2, 2, 0, 1, 2, 2, 0, 1, 2, 0, 0, 1, 2, 1, 0, 1, 2, 2, 0, 1, 2, 2,
			0, 0, 0, 0, 1, 1, 1, 0, 2, 2, 2, 0, 2, 2, 2, 0, 0, 0, 0, 1, 1, 1, 1, 1, 2, 2, 2, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 0, 2, 2, 2, 0, 2, 2, 2, 0, 0, 0, 0, 1, 1, 1, 1, 1, 2, 2, 2, 1, 1, 1, 1, 0, 0, 0, 0, 0, 1, 1, 1, 0, 2, 2, 2, 0, 2, 2, 2, 0, 0, 0, 0, 1, 1, 1, 1, 1, 2, 2, 2, 1, 2, 2, 2, 0, 0, 0, 0, 0, 1, 1, 1, 0, 2, 2, 2, 0, 2, 2, 2, 0, 0, 0, 0, 1, 1, 1, 1, 1, 2, 2, 2, 1, 2, 2, 2, 0, 0, 0, 0, 0, 1, 1, 1, 0, 2, 2, 2, 0, 2, 2, 2, 0, 0, 0, 0, 1, 1, 1, 1, 1, 2, 2, 2, 1, 0, 0, 0, 1, 0, 0, 0, 0, 1, 1, 1, 0, 2, 2, 2, 0, 2, 2, 2, 0, 0, 0, 0, 1, 1, 1, 1, 1, 2, 2, 2, 1, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 0, 2, 2, 2, 0, 2, 2, 2, 0, 0, 0, 0, 1, 1, 1, 1, 1, 2, 2, 2, 1, 2, 2, 2, 1, 0, 0, 0, 0, 1, 1, 1, 0, 2, 2, 2, 0, 2, 2, 2, 0, 0, 0, 0, 1, 1, 1, 1, 1, 2, 2, 2, 1, 2, 2, 2, 1,
			0, 0, 0, 2, 0, 0, 0, 2, 0, 0, 0, 2, 2, 2, 2, 2, 1, 1, 1, 2, 1, 1, 1, 2, 1, 1, 1, 2, 0, 0, 0, 2, 0, 0, 0, 2, 0, 0, 0, 2, 0, 0, 0, 2, 2, 2, 2, 2, 1, 1, 1, 2, 1, 1, 1, 2, 1, 1, 1, 2, 0, 0, 0, 2, 0, 0, 0, 2, 0, 0, 0, 2, 0, 0, 0, 2, 2, 2, 2, 2, 1, 1, 1, 2, 1, 1, 1, 2, 1, 1, 1, 2, 0, 0, 0, 2, 0, 0, 0, 2, 0, 0, 0, 2, 0, 0, 0, 2, 2, 2, 2, 2, 1, 1, 1, 2, 1, 1, 1, 2, 1, 1, 1, 2, 2, 2, 2, 2, 0, 0, 0, 2, 0, 0, 0, 2, 0, 0, 0, 2, 2, 2, 2, 2, 1, 1, 1, 2, 1, 1, 1, 2, 1, 1, 1, 2, 1, 1, 1, 2, 0, 0, 0, 2, 0, 0, 0, 2, 0, 0, 0, 2, 2, 2, 2, 2, 1, 1, 1, 2, 1, 1, 1, 2, 1, 1, 1, 2, 1, 1, 1, 2, 0, 0, 0, 2, 0, 0, 0, 2, 0, 0, 0, 2, 2, 2, 2, 2, 1, 1, 1, 2, 1, 1, 1, 2, 1, 1, 1, 2, 1, 1, 1, 2, 0, 0, 0, 2, 0, 0, 0, 2, 0, 0, 0, 2, 2, 2, 2, 2, 1, 1, 1, 2, 1, 1, 1, 2, 1, 1, 1, 2, 2, 2, 2, 2,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 2, 2, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 2, 2, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 2, 2, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 2, 2, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 2, 2, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2
		];
		private static ReadOnlySpan<int> DIQuintsTable =>
		[
			0, 1, 2, 3, 4, 0, 4, 4, 0, 1, 2, 3, 4, 1, 4, 4, 0, 1, 2, 3, 4, 2, 4, 4, 0, 1, 2, 3, 4, 3, 4, 4, 0, 1, 2, 3, 4, 0, 4, 0, 0, 1, 2, 3, 4, 1, 4, 1, 0, 1, 2, 3, 4, 2, 4, 2, 0, 1, 2, 3, 4, 3, 4, 3, 0, 1, 2, 3, 4, 0, 2, 3, 0, 1, 2, 3, 4, 1, 2, 3, 0, 1, 2, 3, 4, 2, 2, 3, 0, 1, 2, 3, 4, 3, 2, 3, 0, 1, 2, 3, 4, 0, 0, 1, 0, 1, 2, 3, 4, 1, 0, 1, 0, 1, 2, 3, 4, 2, 0, 1, 0, 1, 2, 3, 4, 3, 0, 1,
			0, 0, 0, 0, 0, 4, 4, 4, 1, 1, 1, 1, 1, 4, 4, 4, 2, 2, 2, 2, 2, 4, 4, 4, 3, 3, 3, 3, 3, 4, 4, 4, 0, 0, 0, 0, 0, 4, 0, 4, 1, 1, 1, 1, 1, 4, 1, 4, 2, 2, 2, 2, 2, 4, 2, 4, 3, 3, 3, 3, 3, 4, 3, 4, 0, 0, 0, 0, 0, 4, 0, 0, 1, 1, 1, 1, 1, 4, 1, 1, 2, 2, 2, 2, 2, 4, 2, 2, 3, 3, 3, 3, 3, 4, 3, 3, 0, 0, 0, 0, 0, 4, 0, 0, 1, 1, 1, 1, 1, 4, 1, 1, 2, 2, 2, 2, 2, 4, 2, 2, 3, 3, 3, 3, 3, 4, 3, 3,
			0, 0, 0, 0, 0, 0, 0, 4, 0, 0, 0, 0, 0, 0, 1, 4, 0, 0, 0, 0, 0, 0, 2, 4, 0, 0, 0, 0, 0, 0, 3, 4, 1, 1, 1, 1, 1, 1, 4, 4, 1, 1, 1, 1, 1, 1, 4, 4, 1, 1, 1, 1, 1, 1, 4, 4, 1, 1, 1, 1, 1, 1, 4, 4, 2, 2, 2, 2, 2, 2, 4, 4, 2, 2, 2, 2, 2, 2, 4, 4, 2, 2, 2, 2, 2, 2, 4, 4, 2, 2, 2, 2, 2, 2, 4, 4, 3, 3, 3, 3, 3, 3, 4, 4, 3, 3, 3, 3, 3, 3, 4, 4, 3, 3, 3, 3, 3, 3, 4, 4, 3, 3, 3, 3, 3, 3, 4, 4,
		];

		private static ReadOnlySpan<int> DETritsTable => [0, 204, 93, 44, 22, 11, 5];
		private static ReadOnlySpan<int> DEQuintsTable => [0, 113, 54, 26, 13, 6];
	}
}
