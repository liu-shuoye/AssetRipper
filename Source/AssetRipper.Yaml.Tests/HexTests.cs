using NUnit.Framework;
using System.Numerics;

namespace AssetRipper.Yaml.Tests;

internal class HexTests
{
	[Test]
	public void OneFloatTest()
	{
		YamlScalarNode node = YamlScalarNode.CreateHex(1f);
		Assert.That(node.ToString(), Is.EqualTo("0x3f800000(1)"));
	}

	[Test]
	public void Bit31SetValuesTest()
	{
		using (Assert.EnterMultipleScope())
		{
			Assert.That(EncodeOne<uint>(0x80000000u), Is.EqualTo("00000080"));
			Assert.That(EncodeOne<uint>(0xFFFFFFFFu), Is.EqualTo("ffffffff"));
			Assert.That(EncodeOne<uint>(0xA5B6C7D8u), Is.EqualTo("d8c7b6a5"));
			Assert.That(EncodeOne<uint>(0x7FFFFFFFu), Is.EqualTo("ffffff7f"));
			Assert.That(EncodeOne<int>(-1), Is.EqualTo("ffffffff"));
			Assert.That(EncodeOne<int>(int.MinValue), Is.EqualTo("00000080"));
			Assert.That(EncodeOne<int>(-2), Is.EqualTo("feffffff"));
		}
	}

	[Test]
	public void Bit63SetValuesTest()
	{
		using (Assert.EnterMultipleScope())
		{
			Assert.That(EncodeOne<ulong>(0x8000000000000000UL), Is.EqualTo("0000000000000080"));
			Assert.That(EncodeOne<ulong>(0xFFFFFFFFFFFFFFFFUL), Is.EqualTo("ffffffffffffffff"));
			Assert.That(EncodeOne<long>(long.MinValue), Is.EqualTo("0000000000000080"));
			Assert.That(EncodeOne<long>(-1L), Is.EqualTo("ffffffffffffffff"));
		}
	}

	[Test]
	public void NegativeFloatListTest()
	{
		// 0xBF800000 的最高 nibble 为 b，修复前会被符号扩展成 ASCII '/'（47）
		Assert.That(EncodeOne<float>(-1f), Is.EqualTo("000080bf"));
	}

	[Test]
	public void FfffCharListTest()
	{
		YamlScalarNode node = YamlScalarNode.CreateHex(['\uFFFF', '\u0000']);
		Assert.That(node.EmitToString(), Is.EqualTo("ffff0000"));
	}

	[Test]
	public void EncodedCharactersAreAllInHexAlphabet()
	{
		// 修复前 bit31/bit63 置位时 nibble 8..f 会写成 ASCII '(' ')' '*' '+' ',' '-' '.' '/'
		foreach (YamlScalarNode node in CreateTrickyNodes())
		{
			string text = node.EmitToString();
			foreach (char c in text)
			{
				Assert.That(char.IsAsciiHexDigitLower(c), Is.True, $"非法字符 '{c}' (U+{(int)c:X4}) 出现在 \"{text}\" 中");
			}
		}
	}

	[Test]
	public void OutputMatchesNaiveLittleEndianByteEncoding()
	{
		// 独立实现的“小端字节序 + 字节内高 nibble 在前”为参考，覆盖全 0、全 0xFFFF、最高位置位与随机值；
		// 高 nibble 非负的输入在修复前后走同一条算术路径，因此该对照同时证明了合法输入输出未变。
		foreach (uint value in SampleValues<uint>(seed: 0x1234, byteCount: 4))
		{
			Assert.That(YamlScalarNode.CreateHex<uint>([value]).EmitToString(), Is.EqualTo(NaiveEncode((ulong)value, 4)));
		}
		foreach (ulong value in SampleValues<ulong>(seed: 0x5678, byteCount: 8))
		{
			Assert.That(YamlScalarNode.CreateHex<ulong>([value]).EmitToString(), Is.EqualTo(NaiveEncode(value, 8)));
		}
		foreach (ushort value in SampleValues<ushort>(seed: 0x9ABC, byteCount: 2))
		{
			Assert.That(YamlScalarNode.CreateHex<ushort>([value]).EmitToString(), Is.EqualTo(NaiveEncode(value, 2)));
		}
		foreach (byte value in SampleValues<byte>(seed: 0xDEF0, byteCount: 1))
		{
			Assert.That(YamlScalarNode.CreateHex<byte>([value]).EmitToString(), Is.EqualTo(NaiveEncode(value, 1)));
		}
	}

	private static IEnumerable<YamlScalarNode> CreateTrickyNodes()
	{
		yield return YamlScalarNode.CreateHex<int>([int.MinValue, -1, -0x8000000, 0, int.MaxValue, 0x00FF00FF]);
		yield return YamlScalarNode.CreateHex<long>([long.MinValue, -1, 0, long.MaxValue]);
		yield return YamlScalarNode.CreateHex<ulong>([0, 0xFFFF_FFFF_FFFF_FFFF, 0x8000_0000_0000_0000]);
		yield return YamlScalarNode.CreateHex<float>([-1f, -0f, float.NegativeInfinity]);
		yield return YamlScalarNode.CreateHex<double>([double.MinValue, double.MaxValue]);
	}

	private static IEnumerable<T> SampleValues<T>(int seed, int byteCount) where T : unmanaged, INumber<T>
	{
		Random random = new(seed);
		ulong max = byteCount == 8 ? ulong.MaxValue : (1UL << (byteCount * 8)) - 1;
		ulong topBit = 1UL << (byteCount * 8 - 1);
		yield return FromBits<T>(0);
		yield return FromBits<T>(max);
		yield return FromBits<T>(topBit);
		yield return FromBits<T>(max & ~topBit);
		yield return FromBits<T>(topBit | 1);
		for (int i = 0; i < 500; i++)
		{
			yield return FromBits<T>((ulong)random.NextInt64(0, long.MaxValue) & max);
		}
	}

	private static T FromBits<T>(ulong bits) where T : unmanaged, INumber<T>
	{
		// 按位宽截断，保证最高位置位场景能落到目标类型
		if (typeof(T) == typeof(byte))
		{
			return (T)(object)(byte)bits;
		}
		if (typeof(T) == typeof(ushort))
		{
			return (T)(object)(ushort)bits;
		}
		if (typeof(T) == typeof(uint))
		{
			return (T)(object)(uint)bits;
		}
		return (T)(object)bits;
	}

	private static string EncodeOne<T>(T value) where T : unmanaged, INumber<T>
		=> YamlScalarNode.CreateHex<T>([value]).EmitToString();

	private static string NaiveEncode(ulong value, int byteCount)
	{
		Span<char> buffer = stackalloc char[byteCount * 2];
		for (int i = 0; i < byteCount; i++)
		{
			byte b = (byte)((value >> (8 * i)) & 0xFF);
			buffer[2 * i] = HexDigit(b >> 4);
			buffer[2 * i + 1] = HexDigit(b & 0x0F);
		}
		return new string(buffer);
	}

	private static char HexDigit(int nibble) => (char)(nibble < 10 ? '0' + nibble : 'a' + (nibble - 10));
}
