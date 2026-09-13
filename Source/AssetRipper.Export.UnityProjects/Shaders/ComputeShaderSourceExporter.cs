using System.Text;
using AssetRipper.Assets;
using AssetRipper.Export.Modules.Shaders.IO;
using AssetRipper.SourceGenerated.Classes.ClassID_72;
using AssetRipper.SourceGenerated.Subclasses.ComputeShaderKernel;
using AssetRipper.SourceGenerated.Subclasses.ComputeShaderVariant;

namespace AssetRipper.Export.UnityProjects.Shaders;

/// <summary>
/// 把 ComputeShader 还原为可读源码（.compute 文本文件）。
/// </summary>
/// <remarks>
/// 工作方式：遍历平台变体（<see cref="IComputeShaderVariant"/>）里的每个 kernel，
/// 把 kernel 的 <c>Code</c> 载荷直通写出。对闪耀暖暖这类随包携带 HLSLcc 生成的
/// GLES GLSL 文本源码的资产，解码后即为完整可读的 kernel 源码；对二进制字节码载荷
/// （DXBC/SPIR-V 等）则只写出 kernel 签名与注释，保证导出不中断。
/// 产物是「高可读参考源码」，不保证能在 Unity Editor 中编译。
/// </remarks>
public sealed class ComputeShaderSourceExporter : BinaryAssetExporter
{
	public override bool TryCreateCollection(IUnityObjectBase asset, [NotNullWhen(true)] out IExportCollection? exportCollection)
	{
		if (asset is IComputeShader computeShader)
		{
			exportCollection = new ComputeShaderExportCollection(this, computeShader);
			return true;
		}

		exportCollection = null;
		return false;
	}

	public override bool Export(IExportContainer container, IUnityObjectBase asset, string path, FileSystem fileSystem)
	{
		IComputeShader computeShader = (IComputeShader)asset;
		using Stream fileStream = fileSystem.File.Create(path);
		using InvariantStreamWriter writer = new(fileStream, new UTF8Encoding(false));

		writer.WriteLine($"// ComputeShader 还原源码：{computeShader.Name_R.String}");
		writer.WriteLine($"// 平台变体数：{computeShader.Variants?.Count ?? 0}");
		writer.WriteLine();

		int kernelIndex = 0;
		// 个别版本 Variants / Kernels 可能为空（无平台变体），逐一判空保证导出不中断
		if (computeShader.Variants is { } variants)
		{
			foreach (IComputeShaderVariant variant in variants)
			{
				writer.WriteLine($"// ===== 变体：targetRenderer={variant.TargetRenderer}, targetLevel={variant.TargetLevel} =====");
				if (variant.Kernels is { } kernels)
				{
					foreach (IComputeShaderKernel kernel in kernels)
					{
						WriteKernel(writer, kernel, kernelIndex);
						kernelIndex++;
					}
				}
			}
		}

		return true;
	}

	private static void WriteKernel(TextWriter writer, IComputeShaderKernel kernel, int index)
	{
		string name = GetKernelName(kernel);
		(uint x, uint y, uint z) = GetThreadGroupSize(kernel);

		writer.WriteLine($"// ================= Kernel[{index}]：{name} (numthreads {x},{y},{z}) =================");
		writer.WriteLine($"#pragma kernel {name}");
		writer.WriteLine($"#pragma numthreads({x}, {y}, {z})");
		writer.WriteLine();

		if (kernel.Code is { Length: > 0 })
		{
			if (IsLikelyText(kernel.Code))
			{
				// 随包携带的文本源码（HLSLcc 生成的 GLSL 等）直通输出，去尾部空字符
				string source = Encoding.UTF8.GetString(kernel.Code).TrimEnd('\0');
				writer.Write(source);
			}
			else
			{
				writer.WriteLine("// 载荷为二进制字节码（DXBC/SPIR-V 等），未还原。");
			}
		}
		else
		{
			writer.WriteLine("// 无载荷。");
		}

		writer.WriteLine();
	}

	/// <summary>
	/// kernel 名字优先取序列化的 Utf8String 字段；个别版本该字段为空时回退到 FastPropertyName。
	/// </summary>
	private static string GetKernelName(IComputeShaderKernel kernel)
	{
		// 序列化的 Utf8String 优先；个别版本为空时回退到 FastPropertyName 包装字段
		return kernel.Name_R_Utf8String?.String
			?? kernel.Name_R_FastPropertyName?.Name.String
			?? string.Empty;
	}

	private static (uint, uint, uint) GetThreadGroupSize(IComputeShaderKernel kernel)
	{
		// ThreadGroupSize 理论上恒为 3 个 uint（x/y/z），防御性判空避免老版本缺字段
		if (kernel.ThreadGroupSize is { Count: >= 3 } sizes)
		{
			return (sizes[0], sizes[1], sizes[2]);
		}

		return (0, 0, 0);
	}

	/// <summary>
	/// 粗判载荷是否为文本：取前 512 字节，可打印字符（含 \t\n\r）占比 ≥90% 视为文本。
	/// 二进制字节码（如 DXBC/SPIR-V）通常无法达到该比例。
	/// </summary>
	private static bool IsLikelyText(byte[] data)
	{
		int sample = Math.Min(data.Length, 512);
		int printable = 0;
		for (int i = 0; i < sample; i++)
		{
			byte b = data[i];
			if (b >= 0x20 && b < 0x7F || b is 0x09 or 0x0A or 0x0D)
			{
				printable++;
			}
		}

		return printable * 10 >= sample * 9;
	}
}
