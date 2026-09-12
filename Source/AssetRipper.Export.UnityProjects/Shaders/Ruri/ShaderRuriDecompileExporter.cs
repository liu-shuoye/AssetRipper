using System.Buffers;
using System.Text;
using System.Threading;
using AssetRipper.Assets;
using AssetRipper.Assets.Generics;
using AssetRipper.Export.Modules.Shaders.Extensions;
using AssetRipper.Export.Modules.Shaders.IO;
using AssetRipper.Export.Modules.Shaders.ShaderBlob;
using AssetRipper.Export.UnityProjects;
using AssetRipper.Export.UnityProjects.Shaders;
using AssetRipper.IO.Files;
using AssetRipper.Primitives;
using AssetRipper.SourceGenerated.Classes.ClassID_48;
using AssetRipper.SourceGenerated.Extensions.Enums.Shader;
using AssetRipper.SourceGenerated.Extensions.Enums.Shader.GpuProgramType;
using AssetRipper.SourceGenerated.Subclasses.SerializedPass;
using AssetRipper.SourceGenerated.Subclasses.SerializedPlayerSubProgram;
using AssetRipper.SourceGenerated.Subclasses.SerializedProgram;
using AssetRipper.SourceGenerated.Subclasses.SerializedProgramParameters;
using AssetRipper.SourceGenerated.Subclasses.SerializedShaderRTBlendState;
using AssetRipper.SourceGenerated.Subclasses.SerializedShaderState;
using AssetRipper.SourceGenerated.Subclasses.SerializedSubProgram;
using AssetRipper.SourceGenerated.NativeEnums.Global;
using Ruri.ShaderTools;
using Ruri.ShaderTools.Unity.ShaderLab;
using Ruri.ShaderTools.Pipeline.Frontend;

// AssetRipper 原生的参数类型与本文件默认解析到的 Ruri 类型同名但不同源，
// 用别名显式区分，避免误用（见 AppendRuntimeSymbols）。
using ForkParameter = AssetRipper.Export.Modules.Shaders.ShaderBlob.Parameters;
// 同名扩展类有两个（Modules.Shaders.Extensions 与 SourceGenerated.Extensions），
// 这里要的是后者（提供 GpuProgramType55Relevant），故取别名消歧。
using GpuProgramTypeExtensions = AssetRipper.SourceGenerated.Extensions.Enums.Shader.GpuProgramType.ShaderGpuProgramTypeExtensions;

namespace AssetRipper.Export.UnityProjects.Shaders;

/// <summary>
/// 用 <c>Ruri.ShaderDecompiler</c> 把编译后的 Shader 二进制还原为可读 HLSL，并组装成 ShaderLab 文本导出。
/// </summary>
/// <remarks>
/// 工作方式：取首选平台（D3D11 &gt; Vulkan）的 subprogram 二进制 → 连同从 Unity 资产读出的
/// cbuffer/纹理/采样器等符号表一起交给反编译库 → 把每个 pass 的源码回填后由 ShaderLabWriter 输出。
/// 与 <c>USCShaderExporter</c> 的区别在于它会覆盖全部 pass 与变体，且支持把变体拆成独立 .hlsl 文件。
/// 产物是「高可读参考源码」，不保证能在 Unity Editor 中编译。
/// </remarks>
public sealed class ShaderRuriDecompileExporter : ShaderExporterBase
{
    /// <summary>
    /// 供宿主注入的观察者钩子，用于在不改动本导出器的前提下扩展行为。
    /// </summary>
    public interface IShaderExportObserver
    {
        /// <summary>单个 pass 的符号表读取完毕时回调，可用于补全或记录符号。</summary>
        void OnPassSymbolsRead(SerializedProgramData symbols, ShaderSubProgram subProgram, ShaderReadContext context) { }

        /// <summary>整个 Shader 的所有 pass 都读取完毕后回调。</summary>
        void OnShaderSymbolsRead(IReadOnlyList<ShaderPassView> passes) { }

        /// <summary>整个 Shader 反编译结束后回调。</summary>
        void OnShaderDecompiled(string shaderName, IReadOnlyList<ShaderPassResultView> passes) { }

        /// <summary>在候选平台中做最终选择；默认沿用传入的 <paramref name="defaultChoice"/>。</summary>
        GPUPlatform PickPlatform(IShader shader, IReadOnlyCollection<GPUPlatform> available, GPUPlatform defaultChoice) => defaultChoice;

        /// <summary>
        /// 在默认的「按 header 偏移裁剪」之外提供自定义的二进制拆包方式
        /// （例如把 Vulkan 的 smolv 压缩 SPIR-V 解压后再交给反编译库）。
        /// 返回 <see langword="null"/> 表示沿用默认处理。
        /// </summary>
        IReadOnlyList<(string Stage, byte[] Binary)>? SplitProgramPayload(byte[] programData, GPUPlatform platform, string stage, UnityVersion version) => null;
    }

    /// <summary>
    /// 当前生效的观察者；为 <see langword="null"/> 时全部钩子被跳过。
    /// </summary>
    public static IShaderExportObserver? Observer;

    /// <summary>
    /// 是否把各个变体拆成独立 .hlsl 文件（<see langword="true"/>）还是全部内联到一个 .shader（<see langword="false"/>）。
    /// 底层读写的是 <c>Ruri.ShaderDecompiler</c> 的全局设置，因此这里做整体替换。
    /// </summary>
    public static bool SplitVariantsToHlslFiles
    {
        get => ShaderDecompilerSettingsAccess.Current.SplitVariantsToHlslFiles;
        set
        {
            var current = ShaderDecompilerSettingsAccess.Current;
            if (current.SplitVariantsToHlslFiles == value) return;
            ShaderDecompilerSettingsAccess.Replace(new ShaderDecompilerSettings
            {
                SplitVariantsToHlslFiles = value,
                WarnIfNoMappings = current.WarnIfNoMappings,
                TryMatchBaseEngineVersion = current.TryMatchBaseEngineVersion,
            });
        }
    }

    private static readonly GPUPlatform[] PreferredPlatforms = new[]
    {
        GPUPlatform.D3D11,
        GPUPlatform.Vulkan,
    };

    /// <summary>
    /// 每个 program slot 只保留体积最大的变体，用于快速迭代。可由环境变量 <c>RURI_SHADER_FAST_ITERATION=1</c> 置位。
    /// </summary>
    public static bool OneVariantPerProgramSlot { get; set; } =
        Environment.GetEnvironmentVariable("RURI_SHADER_FAST_ITERATION") == "1";

    /// <summary>
    /// 遇到第一个反编译失败就终止进程。默认关闭；GUI 宿主下请勿开启（会直接杀掉进程）。
    /// 可由环境变量 <c>RURI_STRICT_SHADER_EXPORT=1</c> 置位。
    /// </summary>
    public static bool StrictShaderExport { get; set; } =
        Environment.GetEnvironmentVariable("RURI_STRICT_SHADER_EXPORT") == "1";

    /// <summary>
    /// 导出单个 Shader。无法反编译时退回占位 shader，保证始终有产物。
    /// </summary>
    public override bool Export(IExportContainer container, IUnityObjectBase asset, string path, FileSystem fileSystem)
    {
        IShader shader = (IShader)asset;
        GPUPlatform platform = PickBestPlatform(shader);
        if (platform == GPUPlatform.Unknown || !DecompileShader(shader, platform, path, fileSystem))
        {
            // 无法反编译时（没有 ParsedForm、没有可读 blob、平台不在 D3D11/Vulkan 之列等）
            // 退回占位 shader：本导出器已被 TryCreateCollection 认领，若直接返回 false
            // 上层不会改写其他导出器，结果是该资产一个文件都不产出。
            // 这里与 USCShaderExporter 的失败路径保持一致。
            Diag($"回退占位：{shader.Name} | Platforms={DescribePlatforms(shader)} | 选中={platform} | ParsedForm={(shader.Has_ParsedForm() ? "有" : "无")}");
            WriteDummyShader(shader, path, fileSystem);
        }

        return true;
    }

    private static void WriteDummyShader(IShader shader, string path, FileSystem fileSystem)
    {
        using Stream stream = fileSystem.File.Create(path);
        using InvariantStreamWriter writer = new(stream, new UTF8Encoding(false));
        DummyShaderTextExporter.ExportShader(shader, writer);
    }

    private static GPUPlatform PickBestPlatform(IShader shader)
    {
        if (shader.Platforms is null || shader.Platforms.Count == 0)
        {
            return GPUPlatform.Unknown;
        }

        HashSet<GPUPlatform> available = new();
        foreach (var p in shader.Platforms)
        {
            available.Add((GPUPlatform)(int)p);
        }

        string? pinned = Environment.GetEnvironmentVariable("RURI_SHADER_PLATFORM");
        if (!string.IsNullOrWhiteSpace(pinned)
            && Enum.TryParse(pinned, ignoreCase: true, out GPUPlatform pinnedPlatform)
            && available.Contains(pinnedPlatform))
        {
            return pinnedPlatform;
        }

        GPUPlatform chosen = GPUPlatform.Unknown;
        foreach (GPUPlatform candidate in PreferredPlatforms)
        {
            if (available.Contains(candidate))
            {
                chosen = candidate;
                break;
            }
        }

        if (chosen == GPUPlatform.Unknown)
        {
            // 退而求其次：取资产声明的第一个平台。
            // 必要性：部分游戏（如闪耀暖暖）只声明 Gles3x，且随包携带的就是
            // HLSLcc 生成的 GLES GLSL 源码——那条路走「源码直通」，不需要平台能力。
            // 若该平台只有编译字节码且库处理不了，后续仍会逐 pass 失败并回退占位。
            chosen = (GPUPlatform)(int)shader.Platforms[0];
        }

        GPUPlatform preferred = Observer?.PickPlatform(shader, available, chosen) ?? chosen;
        return available.Contains(preferred) ? preferred : chosen;
    }

    private static bool DecompileShader(IShader shader, GPUPlatform platform, string outputPath, FileSystem fileSystem)
    {
        if (shader.ParsedForm is null)
        {
            Diag($"{shader.Name}: 没有 ParsedForm（未编译/只有 Script 文本）");
            return false;
        }

        ShaderSubProgramBlob[] blobs = shader.ReadBlobs();
        if (blobs.Length == 0)
        {
            Diag($"{shader.Name}: ReadBlobs() 为空（CompressedBlob / Offsets / Lengths 缺失）");
            return false;
        }

        List<ShaderReadPass> reads = ReadPasses(shader, blobs, platform);
        if (reads.Count == 0)
        {
            Diag($"{shader.Name}: 平台 {platform} 下没有可读的 subprogram（blobs={blobs.Length}）");
            return false;
        }

        List<ShaderSymbolPass> symbols = BuildSymbols(reads);
        if (symbols.Count == 0)
        {
            Diag($"{shader.Name}: 符号表为空（reads={reads.Count}）");
            return false;
        }

        Diag($"{shader.Name}: 开始反编译 {symbols.Count} 个 pass，平台 {platform}");
        UnityShaderMetadata unityMetadata = UnityShaderMetadataBuilder.Build(shader, platform, EnumerateProgramBlobIndices,
            symbols.Select(static s => new UnityShaderMetadataBuilder.ProgramResultLocation(
                s.Read.SubShaderIndex, s.Read.PassIndex, s.Read.Stage, s.Read.BlobIndex, s.Read.ParameterBlobIndex, s.Read.KeywordIndices)).ToList());
        DecompileAndWritePasses(shader, symbols, unityMetadata, outputPath, fileSystem);
        return true;
    }

    private static List<ShaderReadPass> ReadPasses(IShader shader, ShaderSubProgramBlob[] blobs, GPUPlatform platform)
    {
        List<int> platformValues = shader.Platforms?.Select(p => (int)p).ToList() ?? [];
        int selectedPlatformIndex = platformValues.FindIndex(p => p == (int)platform);
        if (selectedPlatformIndex < 0 || selectedPlatformIndex >= blobs.Length)
        {
            return [];
        }

        ShaderSubProgramBlob blob = blobs[selectedPlatformIndex];
        List<ShaderReadPass> result = [];
        for (int subShaderIndex = 0; subShaderIndex < shader.ParsedForm!.SubShaders.Count; subShaderIndex++)
        {
            var subShader = shader.ParsedForm.SubShaders[subShaderIndex];
            for (int passIndex = 0; passIndex < subShader.Passes.Count; passIndex++)
            {
                var pass = subShader.Passes[passIndex];
                Dictionary<int, string> nameTable = BuildNameTable(pass.NameIndices);
                ReadProgram(shader, blob, pass, pass.ProgVertex, platform, subShaderIndex, passIndex, "Vertex", nameTable, result);
                ReadProgram(shader, blob, pass, pass.ProgFragment, platform, subShaderIndex, passIndex, "Fragment", nameTable, result);
                ReadProgram(shader, blob, pass, pass.ProgGeometry, platform, subShaderIndex, passIndex, "Geometry", nameTable, result);
                ReadProgram(shader, blob, pass, pass.ProgHull, platform, subShaderIndex, passIndex, "Hull", nameTable, result);
                ReadProgram(shader, blob, pass, pass.ProgDomain, platform, subShaderIndex, passIndex, "Domain", nameTable, result);
                ReadProgram(shader, blob, pass, pass.ProgRayTracing, platform, subShaderIndex, passIndex, "RayTracing", nameTable, result);
            }
        }

        return result;
    }

    private static void ReadProgram(
        IShader shader,
        ShaderSubProgramBlob blob,
        ISerializedPass pass,
        ISerializedProgram? program,
        GPUPlatform platform,
        int subShaderIndex,
        int passIndex,
        string stage,
        Dictionary<int, string> nameTable,
        List<ShaderReadPass> result)
    {
        if (program is null)
        {
            return;
        }

        LogProgramEnumeration(shader.Name, stage, program, shader.Collection.Version);

        int slotStart = result.Count;

        foreach (ShaderReadSource source in EnumerateProgramSources(program, shader.Collection.Version, platform))
        {
            // 烘焙/未编译的 shader 会带无效 BlobIndex（如 0xFFFFFFFE）或根本没有 entry，
            // 此时解析不出程序数据；跳过而不是抛异常，保证单个 shader 的坏数据不影响整体导出
            if (blob.Entries.Length == 0
                || source.BlobIndex >= blob.Entries.Length
                || (source.ParameterBlobIndex is uint parameterIndex && parameterIndex >= blob.Entries.Length))
            {
                continue;
            }

            ShaderSubProgram subProgram = source.ParameterBlobIndex is uint paramBlobIndex
                ? blob.GetSubProgram(source.BlobIndex, paramBlobIndex)
                : blob.GetSubProgram(source.BlobIndex);

            if (subProgram.ProgramData.Length == 0)
            {
                continue;
            }

            List<(string Stage, byte[] Binary)> binaries = [];
            IReadOnlyList<(string Stage, byte[] Binary)>? split =
                Observer?.SplitProgramPayload(subProgram.ProgramData, platform, stage, shader.Collection.Version);
            if (split is not null)
            {
                binaries.AddRange(split);
            }
            else
            {
                byte[] payload = ExtractPayload(subProgram.ProgramData, shader.Collection.Version);
                if (payload.Length > 0)
                {
                    binaries.Add((stage, payload));
                }

                Diag($"  {stage} blob{source.BlobIndex}: ProgramData={subProgram.ProgramData.Length}B " +
                    $"裁剪后={payload.Length}B 原始类型={ClassifyBinary(subProgram.ProgramData)} 裁剪后类型={ClassifyBinary(payload)}");
            }
            if (binaries.Count == 0)
            {
                Diag($"  {stage} blob{source.BlobIndex}: 拆包后为空，跳过");
                continue;
            }

            foreach ((string moduleStage, byte[] binary) in binaries)
            {
                if (result.Any(existing => existing.SubShaderIndex == subShaderIndex
                    && existing.PassIndex == passIndex
                    && existing.Stage == moduleStage
                    && existing.BlobIndex == source.BlobIndex
                    && existing.KeywordIndices.SequenceEqual(source.KeywordIndices)))
                {
                    continue;
                }

                result.Add(new ShaderReadPass(
                    pass.State.Name_R,
                    subShaderIndex,
                    passIndex,
                    moduleStage,
                    source.BlobIndex,
                    source.ParameterBlobIndex,
                    source.KeywordIndices,
                    subProgram,
                    ReadProgramSymbols(program.CommonParameters, nameTable),
                    ReadProgramSymbols(source.Parameters, nameTable),
                    binary,
                    shader.Name,
                    shader.Collection.Version));
            }
        }

        if (OneVariantPerProgramSlot)
        {
            KeepLargestVariantPerStage(result, slotStart);
        }
    }

    private static void KeepLargestVariantPerStage(List<ShaderReadPass> result, int slotStart)
    {
        if (result.Count - slotStart <= 1)
        {
            return;
        }

        Dictionary<string, ShaderReadPass> largestByStage = new(StringComparer.Ordinal);
        for (int i = slotStart; i < result.Count; i++)
        {
            ShaderReadPass candidate = result[i];
            if (!largestByStage.TryGetValue(candidate.Stage, out ShaderReadPass? incumbent)
                || candidate.Binary.Length > incumbent.Binary.Length)
            {
                largestByStage[candidate.Stage] = candidate;
            }
        }

        result.RemoveRange(slotStart, result.Count - slotStart);
        result.AddRange(largestByStage.Values);
    }

    private static IEnumerable<ShaderReadSource> EnumerateProgramSources(ISerializedProgram program, UnityVersion version, GPUPlatform platform)
    {
        Dictionary<uint, ISerializedSubProgram> subProgramsByBlob = new();
        foreach (ISerializedSubProgram subProgram in program.SubPrograms)
        {
            subProgramsByBlob[subProgram.BlobIndex] = subProgram;
        }

        HashSet<(uint BlobIndex, uint? ParameterBlobIndex, string KeywordIdentity)> emitted = new();

        if (program.Has_PlayerSubPrograms() && program.Has_ParameterBlobIndices()
            && program.PlayerSubPrograms is not null && program.ParameterBlobIndices is not null)
        {
            for (int groupIndex = 0; groupIndex < program.PlayerSubPrograms.Count; groupIndex++)
            {
                AssetList<SerializedPlayerSubProgram> group = program.PlayerSubPrograms[groupIndex];
                AssetList<uint>? paramGroup = groupIndex < program.ParameterBlobIndices.Count
                    ? program.ParameterBlobIndices[groupIndex]
                    : null;
                for (int i = 0; i < group.Count; i++)
                {
                    SerializedPlayerSubProgram playerSubProgram = group[i];
                    if (!MatchesPlatform(version, playerSubProgram.GpuProgramType, platform))
                    {
                        continue;
                    }
                    uint? parameterBlobIndex = paramGroup is not null && i < paramGroup.Count ? paramGroup[i] : null;
                    var emissionKey = CreateEmissionKey(playerSubProgram.BlobIndex, parameterBlobIndex, playerSubProgram.KeywordIndices);
                    if (emitted.Contains(emissionKey))
                    {
                        continue;
                    }

                    subProgramsByBlob.TryGetValue(playerSubProgram.BlobIndex, out ISerializedSubProgram? sourceSubProgram);
                    emitted.Add(emissionKey);
                    yield return new ShaderReadSource(
                        playerSubProgram.BlobIndex,
                        parameterBlobIndex,
                        playerSubProgram.KeywordIndices?.ToList() ?? [],
                        sourceSubProgram?.Has_Parameters() == true ? sourceSubProgram.Parameters : null);
                }
            }
        }

        foreach (ISerializedSubProgram subProgram in program.SubPrograms)
        {
            var emissionKey = CreateEmissionKey(subProgram.BlobIndex, null, subProgram.KeywordIndices);
            if (emitted.Contains(emissionKey))
            {
                continue;
            }
            if (!MatchesPlatform(version, (sbyte)subProgram.GpuProgramType, platform))
            {
                continue;
            }

            emitted.Add(emissionKey);
            yield return new ShaderReadSource(
                subProgram.BlobIndex,
                null,
                subProgram.KeywordIndices?.ToList() ?? [],
                subProgram.Has_Parameters() ? subProgram.Parameters : null);
        }
    }

    private static SerializedProgramData ReadProgramSymbols(ISerializedProgramParameters? parameters, Dictionary<int, string> nameTable)
    {
        SerializedProgramData data = new();
        if (parameters is null)
        {
            return data;
        }

        Func<int, string> resolveName = nameIndex => nameTable.TryGetValue(nameIndex, out string? name) ? name : $"name_{nameIndex}";

        foreach (var cbuffer in parameters.ConstantBuffers)
        {
            ConstantBufferParameter buffer = new()
            {
                Name = resolveName(cbuffer.NameIndex),
                NameIndex = cbuffer.NameIndex,
                Size = cbuffer.Size,
                IsPartialCB = cbuffer.Has_IsPartialCB() && cbuffer.IsPartialCB,
                MatrixParameters = cbuffer.MatrixParams.Select(matrix => new MatrixParameter
                {
                    Name = resolveName(matrix.NameIndex),
                    NameIndex = matrix.NameIndex,
                    Index = matrix.OffsetInConstantBuffer,
                    ArraySize = matrix.ArraySize,
                    Type = (Ruri.ShaderTools.ShaderParamType)(int)(sbyte)matrix.Type,
                    RowCount = unchecked((byte)matrix.RowCount),
                    ColumnCount = 4,
                    IsMatrix = true,
                }).ToArray(),
                VectorParameters = cbuffer.VectorParams.Select(vector => new VectorParameter
                {
                    Name = resolveName(vector.NameIndex),
                    NameIndex = vector.NameIndex,
                    Index = vector.OffsetInConstantBuffer,
                    ArraySize = vector.ArraySize,
                    Type = (Ruri.ShaderTools.ShaderParamType)(int)(sbyte)vector.Type,
                    RowCount = unchecked((byte)vector.Dim),
                    ColumnCount = 1,
                    IsMatrix = false,
                }).ToArray(),
                StructParameters = cbuffer.StructParams.Select(structParam => new StructParameter
                {
                    Name = resolveName(structParam.NameIndex),
                    NameIndex = structParam.NameIndex,
                    Index = structParam.OffsetInConstantBuffer,
                    ArraySize = structParam.ArraySize,
                    StructSize = structParam.StructSize,
                    MatrixMembers = structParam.MatrixMembers.Select(matrix => new MatrixParameter
                    {
                        Name = $"{resolveName(structParam.NameIndex)}.{resolveName(matrix.NameIndex)}",
                        NameIndex = matrix.NameIndex,
                        Index = matrix.OffsetInConstantBuffer,
                        ArraySize = matrix.ArraySize,
                        Type = (Ruri.ShaderTools.ShaderParamType)(int)(sbyte)matrix.Type,
                        RowCount = unchecked((byte)matrix.RowCount),
                        ColumnCount = 4,
                        IsMatrix = true,
                    }).ToArray(),
                    VectorMembers = structParam.VectorMembers.Select(vector => new VectorParameter
                    {
                        Name = $"{resolveName(structParam.NameIndex)}.{resolveName(vector.NameIndex)}",
                        NameIndex = vector.NameIndex,
                        Index = vector.OffsetInConstantBuffer,
                        ArraySize = vector.ArraySize,
                        Type = (Ruri.ShaderTools.ShaderParamType)(int)(sbyte)vector.Type,
                        RowCount = unchecked((byte)vector.Dim),
                        ColumnCount = 1,
                        IsMatrix = false,
                    }).ToArray(),
                }).ToArray(),
            };
            data.ConstantBufferParameters.Add(buffer);
        }

        foreach (var binding in parameters.ConstantBufferBindings)
        {
            data.BufferBindingParameters.Add(new BufferBindingParameter
            {
                Name = resolveName(binding.NameIndex),
                NameIndex = binding.NameIndex,
                Index = binding.Index,
                ArraySize = binding.Has_ArraySize() ? binding.ArraySize : 0,
            });
        }

        foreach (var texture in parameters.TextureParams)
        {
            data.TextureParameters.Add(new TextureParameter
            {
                Name = resolveName(texture.NameIndex),
                NameIndex = texture.NameIndex,
                Index = texture.Index,
                SamplerIndex = texture.SamplerIndex,
                MultiSampled = texture.Has_MultiSampled() && texture.MultiSampled,
                Dim = unchecked((byte)(sbyte)texture.Dim),
            });
        }

        foreach (var sampler in parameters.Samplers)
        {
            data.SamplerParameters.Add(new SamplerParameter
            {
                Sampler = sampler.Sampler,
                BindPoint = sampler.BindPoint,
            });
        }

        foreach (var uav in parameters.UAVParams)
        {
            data.UAVParameters.Add(new UAVParameter
            {
                Name = resolveName(uav.NameIndex),
                NameIndex = uav.NameIndex,
                Index = uav.Index,
                OriginalIndex = uav.OriginalIndex,
            });
        }

        foreach (var vector in parameters.VectorParams)
        {
            data.VectorParameters.Add(new VectorParameter
            {
                Name = resolveName(vector.NameIndex),
                NameIndex = vector.NameIndex,
                Index = vector.OffsetInConstantBuffer,
                ArraySize = vector.ArraySize,
                Type = (Ruri.ShaderTools.ShaderParamType)(int)(sbyte)vector.Type,
                RowCount = unchecked((byte)vector.Dim),
                ColumnCount = 1,
                IsMatrix = false,
            });
        }

        foreach (var matrix in parameters.MatrixParams)
        {
            data.MatrixParameters.Add(new MatrixParameter
            {
                Name = resolveName(matrix.NameIndex),
                NameIndex = matrix.NameIndex,
                Index = matrix.OffsetInConstantBuffer,
                ArraySize = matrix.ArraySize,
                Type = (Ruri.ShaderTools.ShaderParamType)(int)(sbyte)matrix.Type,
                RowCount = unchecked((byte)matrix.RowCount),
                ColumnCount = 4,
                IsMatrix = true,
            });
        }

        foreach (var buffer in parameters.BufferParams)
        {
            data.BufferParameters.Add(new BufferBindingParameter
            {
                Name = resolveName(buffer.NameIndex),
                NameIndex = buffer.NameIndex,
                Index = buffer.Index,
                ArraySize = buffer.Has_ArraySize() ? buffer.ArraySize : 0,
            });
        }

        return data;
    }

    private static List<ShaderSymbolPass> BuildSymbols(List<ShaderReadPass> reads)
    {
        List<ShaderSymbolPass> result = [];
        IShaderExportObserver? observer = Observer;
        foreach (ShaderReadPass read in reads)
        {
            SerializedProgramData symbols = new()
            {
                EntryPoint = "main",
                DebugName = $"{read.ShaderName}/SubShader{read.SubShaderIndex}/Pass{read.PassIndex}/{read.Stage}/{read.SubProgram.GetProgramType(read.Version)}/{read.BlobIndex}",
            };

            AppendSymbols(symbols, read.CommonSymbols);
            AppendSymbols(symbols, read.ParameterSymbols);
            AppendRuntimeSymbols(symbols, read.SubProgram);

            observer?.OnPassSymbolsRead(symbols, read.SubProgram, new ShaderReadContext(
                read.ShaderName, read.SubShaderIndex, read.PassIndex, read.BlobIndex, read.Version, read.Stage,
                ProgramTypeToPlatform(read.SubProgram.GetProgramType(read.Version)),
                read.CommonSymbols, read.ParameterSymbols));

            result.Add(new ShaderSymbolPass(read, symbols));
        }

        observer?.OnShaderSymbolsRead(result.Select(static p => new ShaderPassView(
            p.Symbols,
            p.Read.SubShaderIndex,
            p.Read.PassIndex,
            p.Read.Stage,
            ProgramTypeToPlatform(p.Read.SubProgram.GetProgramType(p.Read.Version)) == GPUPlatform.D3D11,
            p.Read.BlobIndex,
            p.Read.Binary,
            p.Read.KeywordIndices)).ToList());

        return result;
    }

    private static void DecompileAndWritePasses(IShader shader, List<ShaderSymbolPass> symbols, UnityShaderMetadata unityMetadata, string outputPath, FileSystem fileSystem)
    {
        // 失败转储会在导出目录旁生成 <output>.failures，属调试产物；
        // 默认关闭（原参考实现是无条件开启），需要排查时用 RURI_SHADER_DUMP_FAILURES=1 打开。
        string? failuresRoot = Environment.GetEnvironmentVariable("RURI_SHADER_DUMP_FAILURES") == "1"
            ? outputPath + ".failures"
            : null;
        int total = symbols.Count;
        var passStems = new string[total];
        // 每个 pass 的最终结果；「随包源码直通」的条目在循环里直接填好。
        DecompileResult[] results = new DecompileResult[total];
        // 只有真正需要反编译的 pass 才进 requests，下标用 decompileTargets 映射回 results。
        List<int> decompileTargets = [];
        List<(byte[] Binary, DecompileOptions Options)> requests = [];
        int passedThroughCount = 0;

        string? dumpInputDir = Environment.GetEnvironmentVariable("RURI_DUMP_INPUT_DIR");

        for (int i = 0; i < total; i++)
        {
            ShaderSymbolPass pass = symbols[i];
            string passStem = $"sub{pass.Read.SubShaderIndex}.pass{pass.Read.PassIndex}.{pass.Read.Stage.ToLowerInvariant()}.blob{pass.Read.BlobIndex}.{SanitizeFileName(pass.Read.PassName)}";
            passStems[i] = passStem;

            if (!string.IsNullOrEmpty(dumpInputDir))
            {
                Directory.CreateDirectory(dumpInputDir);
                string safeShader = SanitizeFileName(shader.Name);
                File.WriteAllBytes(Path.Combine(dumpInputDir, $"{safeShader}.{passStem}.input.bin"), pass.Read.Binary);
            }

            // 有些游戏（如闪耀暖暖）随包携带的本来就是 HLSLcc 生成的 GLES GLSL 源码，
            // 没有任何可反编译的字节码。这种载荷直接当结果使用——比回退成占位 shader 有用得多。
            // 反编译库对此天然支持：SourceLanguage 非 hlsl 时它原样输出，不做 Unity 改写。
            if (IsSourceText(pass.Read.Binary))
            {
                results[i] = new DecompileResult
                {
                    Success = true,
                    // 用完全限定名：AssetRipper.SourceGenerated.NativeEnums.Global 下也有个 Encoding，会歧义。
                    SourceCode = System.Text.Encoding.UTF8.GetString(pass.Read.Binary),
                    SourceLanguage = SourcePassthroughLanguage,
                    SourceFileExtension = SourcePassthroughFileExtension,
                };
                passedThroughCount++;
                Diag($"  {passStem}: 随包源码直通（{pass.Read.Binary.Length}B）");
                continue;
            }

            decompileTargets.Add(i);
            requests.Add((pass.Read.Binary, new DecompileOptions
            {
                Format = ShaderBinaryFormat.Unknown,
                Symbols = pass.Symbols,
                UnityMetadata = unityMetadata,
                ShaderModel = 51,
                DebugDumpDirectory = failuresRoot is null ? null : fileSystem.Path.Join(failuresRoot, passStem),
                DebugDumpStem = "with-symbols",
            }));
        }

        if (requests.Count > 0)
        {
            int completed = 0;
            int requestTotal = requests.Count;
            using ShaderDecompiler decompiler = new(AppDomain.CurrentDomain.BaseDirectory);
            DecompileResult[] batch = decompiler.Decompile(requests, (idx, r) =>
            {
                int target = decompileTargets[idx];
                int now = Interlocked.Increment(ref completed);
                string suffix = r.Success ? string.Empty : $"  fail: {FirstLine(r.ErrorMessage)}";
                Console.WriteLine($"[ShaderDecompile] {shader.Name} [{now}/{requestTotal}] {passStems[target]}{suffix}");

                if (!r.Success && StrictShaderExport)
                {
                    Console.Error.WriteLine($"[ShaderDecompile] RURI_STRICT_SHADER_EXPORT: aborting on first failure  {shader.Name} {passStems[target]}");
                    Console.Error.WriteLine(r.ErrorMessage);
                    if (failuresRoot is not null)
                    {
                        Console.Error.WriteLine($"Debug dump: {fileSystem.Path.Join(failuresRoot, passStems[target])}");
                    }
                    Environment.Exit(1);
                }
            });

            for (int k = 0; k < decompileTargets.Count; k++)
            {
                results[decompileTargets[k]] = batch[k];
            }
        }
        Observer?.OnShaderDecompiled(shader.Name, Enumerable.Range(0, total).Select(i => new ShaderPassResultView(
            symbols[i].Symbols,
            symbols[i].Read.Binary,
            passStems[i],
            results[i]?.Success == true)).ToList());

        int succeeded = 0;
        for (int i = 0; i < total; i++)
        {
            if (results[i]?.Success == true) succeeded++;
        }
        UnityShaderMetadataBuilder.BackfillProgramSources(
            unityMetadata,
            symbols.Select(static s => new UnityShaderMetadataBuilder.ProgramResultLocation(s.Read.SubShaderIndex, s.Read.PassIndex, s.Read.Stage, s.Read.BlobIndex, s.Read.ParameterBlobIndex, s.Read.KeywordIndices)).ToArray(),
            results);

        if (SplitVariantsToHlslFiles)
        {
            string variantFolderStem = fileSystem.Path.GetFileNameWithoutExtension(outputPath);
            ShaderLabDocument result = ShaderLabWriter.WriteSplit(unityMetadata, variantFolderStem);
            WriteTextFile(fileSystem, outputPath, result.ShaderText);

            if (result.VariantFiles.Count > 0)
            {
                string outputDir = fileSystem.Path.GetDirectoryName(outputPath) ?? string.Empty;
                string variantDir = fileSystem.Path.Join(outputDir, variantFolderStem);
                fileSystem.Directory.Create(variantDir);
                foreach (var (filename, body) in result.VariantFiles)
                {
                    WriteTextFile(fileSystem, fileSystem.Path.Join(variantDir, filename), body);
                }
            }

            Console.WriteLine($"[ShaderDecompile] {shader.Name} done ({succeeded}/{total} passes, {passedThroughCount} 个随包源码直通, {result.VariantFiles.Count} variant files)");
        }
        else
        {
            WriteTextFile(fileSystem, outputPath, ShaderLabWriter.Write(unityMetadata));
            Console.WriteLine($"[ShaderDecompile] {shader.Name} done ({succeeded}/{total} passes, {passedThroughCount} 个随包源码直通, inline)");
        }
    }

    /// <summary>
    /// 经 <see cref="FileSystem"/> 抽象写出文本。
    /// </summary>
    /// <remarks>
    /// 这里必须走抽象而不是 <c>File.WriteAllText</c>：导出目标可能是
    /// <see cref="AssetRipper.IO.Files.VirtualFileSystem"/>（不出盘、只组装在内存/打包流里），
    /// 直接写系统文件会失败或写到错误位置。
    /// </remarks>
    private static void WriteTextFile(FileSystem fileSystem, string path, string contents)
    {
        using Stream stream = fileSystem.File.Create(path);
        using InvariantStreamWriter writer = new(stream, new UTF8Encoding(false));
        writer.Write(contents);
    }

    /// <summary>
    /// 诊断开关：置 <c>RURI_SHADER_DIAGNOSTICS=1</c> 时把「为什么没走反编译」的关键信息打到控制台。
    /// </summary>
    /// <remarks>
    /// 排查「整体被回退成占位 shader」时用：能看到资产声明的平台、选中平台、以及在
    /// ParsedForm / blob / pass / 符号表 哪一步退出。
    /// </remarks>
    public static bool Diagnostics { get; set; } =
        Environment.GetEnvironmentVariable("RURI_SHADER_DIAGNOSTICS") == "1";

    private static void Diag(string message)
    {
        if (Diagnostics)
        {
            Console.WriteLine($"[ShaderDecompile:diag] {message}");
        }
    }

    /// <summary>
    /// 以枚举名列出资产声明的全部平台，形如 <c>D3D11(2), Vulkan(21)</c>。
    /// </summary>
    private static string DescribePlatforms(IShader shader)
    {
        if (shader.Platforms is null || shader.Platforms.Count == 0)
        {
            return "<空>";
        }

        return string.Join(", ", shader.Platforms.Select(p =>
        {
            GPUPlatform value = (GPUPlatform)(int)p;
            return $"{value}({(int)value})";
        }));
    }

    /// <summary><see cref="ClassifyBinary"/> 对「纯文本源码」的返回标记。</summary>
    private const string SourceTextKind = "文本(源码)";

    /// <summary>随包源码直通时写进 ShaderLab 的语言标记。</summary>
    private const string SourcePassthroughLanguage = "glsl";

    /// <summary>随包源码直通时拆出的变体文件扩展名。</summary>
    private const string SourcePassthroughFileExtension = ".glsl";

    /// <summary>
    /// 按魔数判断一段二进制是哪种着色器容器；用于识别「本来就是源码、无需反编译」的载荷。
    /// </summary>
    private static string ClassifyBinary(byte[] data)
    {
        if (data.Length >= 4)
        {
            if (data[0] == (byte)'D' && data[1] == (byte)'X' && data[2] == (byte)'B' && data[3] == (byte)'C')
            {
                return "DXBC";
            }
            if (data[0] == 0xBC && data[1] == 0xC0 && data[2] == 0xDE)
            {
                return "LLVM-Bitcode/DXIL";
            }
            if (data[0] == 0x03 && data[1] == 0x02 && data[2] == 0x23 && data[3] == 0x07)
            {
                return "SPIR-V";
            }
        }

        int printable = 0;
        int probe = Math.Min(data.Length, 32);
        for (int i = 0; i < probe; i++)
        {
            byte b = data[i];
            if (b is 0x09 or 0x0A or 0x0D || b is >= 0x20 and < 0x7F)
            {
                printable++;
            }
        }

        return probe > 0 && printable == probe ? SourceTextKind : "未知";
    }

    /// <summary>
    /// 该载荷是否本来就是要用的源码（而非待反编译的字节码）。
    /// </summary>
    private static bool IsSourceText(byte[] payload) => ClassifyBinary(payload) == SourceTextKind;

    private static IEnumerable<UnityShaderMetadataBuilder.ProgramBlobReference> EnumerateProgramBlobIndices(ISerializedProgram program, UnityVersion version, GPUPlatform platform)
    {
        foreach (ShaderReadSource source in EnumerateProgramSources(program, version, platform))
        {
            yield return new UnityShaderMetadataBuilder.ProgramBlobReference(source.BlobIndex, source.ParameterBlobIndex, source.KeywordIndices);
        }
    }

    private static (uint BlobIndex, uint? ParameterBlobIndex, string KeywordIdentity) CreateEmissionKey(uint blobIndex, uint? parameterBlobIndex, IReadOnlyList<ushort>? keywordIndices)
    {
        return (blobIndex, parameterBlobIndex, BuildKeywordIdentity(keywordIndices));
    }

    private static string BuildKeywordIdentity(IReadOnlyList<ushort>? keywordIndices)
    {
        if (keywordIndices is null || keywordIndices.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(",", keywordIndices);
    }

    private static string FirstLine(string? message)
    {
        if (string.IsNullOrEmpty(message)) return "<no message>";
        int newlineIndex = message.IndexOf('\n');
        return newlineIndex < 0 ? message : message.Substring(0, newlineIndex).TrimEnd();
    }


    private static void AppendSymbols(SerializedProgramData target, SerializedProgramData source)
    {
        foreach (ConstantBufferParameter buffer in source.ConstantBufferParameters)
        {
            target.ConstantBufferParameters.Add(buffer);
        }

        foreach (BufferBindingParameter binding in source.BufferBindingParameters)
        {
            target.BufferBindingParameters.Add(binding);
        }

        foreach (TextureParameter texture in source.TextureParameters)
        {
            target.TextureParameters.Add(texture);
        }

        foreach (VectorParameter vector in source.VectorParameters)
        {
            target.VectorParameters.Add(vector);
        }

        foreach (MatrixParameter matrix in source.MatrixParameters)
        {
            target.MatrixParameters.Add(matrix);
        }

        foreach (BufferBindingParameter buffer in source.BufferParameters)
        {
            target.BufferParameters.Add(buffer);
        }
    }

    /// <summary>
    /// 把 AssetRipper 原生 <see cref="ShaderSubProgram"/> 里的参数表搬运到 Ruri 的符号模型。
    /// </summary>
    /// <remarks>
    /// 这里是本移植与原参考实现差异最大的一处：RipperHook 版本的 ShaderSubProgram 是改造过的
    /// 影子类型，参数直接就是 <c>Ruri.ShaderTools.*</c>，所以能直接 Add；本仓库用的是 AssetRipper
    /// 原生类型（<c>AssetRipper.Export.Modules.Shaders.ShaderBlob.Parameters.*</c>），两边
    /// 名字相同但类型不同源，必须逐字段转换，不能直接赋值。
    /// </remarks>
    private static void AppendRuntimeSymbols(SerializedProgramData target, ShaderSubProgram subProgram)
    {
        foreach (ForkParameter.ConstantBuffer cbuffer in subProgram.ConstantBuffers)
        {
            target.ConstantBufferParameters.Add(new ConstantBufferParameter
            {
                Name = cbuffer.Name,
                NameIndex = cbuffer.NameIndex,
                Size = cbuffer.Size,
                IsPartialCB = cbuffer.IsPartialCB,
                MatrixParameters = cbuffer.MatrixParams.Select(ToMatrixParameter).ToArray(),
                VectorParameters = cbuffer.VectorParams.Select(ToVectorParameter).ToArray(),
                StructParameters = cbuffer.StructParams.Select(ToStructParameter).ToArray(),
            });
        }

        foreach (ForkParameter.BufferBinding binding in subProgram.ConstantBufferBindings)
        {
            target.BufferBindingParameters.Add(ToBufferBindingParameter(binding));
        }

        foreach (ForkParameter.TextureParameter texture in subProgram.TextureParameters)
        {
            target.TextureParameters.Add(new TextureParameter
            {
                Name = texture.Name,
                NameIndex = texture.NameIndex,
                Index = texture.Index,
                SamplerIndex = texture.SamplerIndex,
                MultiSampled = texture.MultiSampled,
                Dim = texture.Dim,
            });
        }

        foreach (ForkParameter.SamplerParameter sampler in subProgram.SamplerParameters)
        {
            target.SamplerParameters.Add(new SamplerParameter
            {
                Sampler = sampler.Sampler,
                BindPoint = sampler.BindPoint,
            });
        }

        foreach (ForkParameter.UAVParameter uav in subProgram.UAVParameters)
        {
            target.UAVParameters.Add(new UAVParameter
            {
                Name = uav.Name,
                NameIndex = uav.NameIndex,
                Index = uav.Index,
                OriginalIndex = uav.OriginalIndex,
            });
        }

        foreach (ForkParameter.VectorParameter vector in subProgram.VectorParameters)
        {
            target.VectorParameters.Add(ToVectorParameter(vector));
        }

        foreach (ForkParameter.MatrixParameter matrix in subProgram.MatrixParameters)
        {
            target.MatrixParameters.Add(ToMatrixParameter(matrix));
        }

        foreach (ForkParameter.BufferBinding buffer in subProgram.BufferParameters)
        {
            target.BufferParameters.Add(ToBufferBindingParameter(buffer));
        }
    }

    private static VectorParameter ToVectorParameter(ForkParameter.VectorParameter source) => new()
    {
        Name = source.Name,
        NameIndex = source.NameIndex,
        Index = source.Index,
        ArraySize = source.ArraySize,
        Type = (Ruri.ShaderTools.ShaderParamType)(int)(sbyte)source.Type,
        RowCount = source.Dim,
        ColumnCount = 1,
        IsMatrix = false,
    };

    private static MatrixParameter ToMatrixParameter(ForkParameter.MatrixParameter source) => new()
    {
        Name = source.Name,
        NameIndex = source.NameIndex,
        Index = source.Index,
        ArraySize = source.ArraySize,
        Type = (Ruri.ShaderTools.ShaderParamType)(int)(sbyte)source.Type,
        RowCount = source.RowCount,
        ColumnCount = 4,
        IsMatrix = true,
    };

    private static StructParameter ToStructParameter(ForkParameter.StructParameter source) => new()
    {
        Name = source.Name,
        NameIndex = source.NameIndex,
        Index = source.Index,
        ArraySize = source.ArraySize,
        StructSize = source.StructSize,
        MatrixMembers = source.MatrixMembers.Select(ToMatrixParameter).ToArray(),
        VectorMembers = source.VectorMembers.Select(ToVectorParameter).ToArray(),
    };

    private static BufferBindingParameter ToBufferBindingParameter(ForkParameter.BufferBinding source) => new()
    {
        Name = source.Name,
        NameIndex = source.NameIndex,
        Index = source.Index,
        ArraySize = source.ArraySize,
    };

    private static Dictionary<int, string> BuildNameTable(AccessDictionaryBase<Utf8String, int> nameIndices)
    {
        Dictionary<int, string> table = new(nameIndices.Count);
        for (int i = 0; i < nameIndices.Count; i++)
        {
            var pair = nameIndices.GetPair(i);
            table[pair.Value] = pair.Key.ToString();
        }
        return table;
    }

    private static byte[] ExtractPayload(byte[] programData, UnityVersion version)
    {
        if (programData.Length == 0)
        {
            return [];
        }

        int headerVersion = programData[0];
        int offset = version.GreaterThanOrEquals(5, 4) ? 6 : 5;
        if (headerVersion >= 2)
        {
            offset += 0x20;
        }
        if (offset < 0 || offset >= programData.Length)
        {
            return [];
        }

        byte[] trimmed = new byte[programData.Length - offset];
        Buffer.BlockCopy(programData, offset, trimmed, 0, trimmed.Length);
        return trimmed;
    }

    public static string SanitizeFileName(string value)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        StringBuilder builder = new(value.Length);
        foreach (char c in value)
        {
            builder.Append(invalidChars.Contains(c) ? '_' : c);
        }
        return builder.ToString();
    }

    private static void LogProgramEnumeration(string shaderName, string stage, ISerializedProgram program, UnityVersion version)
    {
        string? filter = Environment.GetEnvironmentVariable("RURI_SHADER_ENUM_DEBUG");
        if (string.IsNullOrWhiteSpace(filter))
        {
            return;
        }

        if (!shaderName.Contains(filter, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Console.WriteLine($"[ShaderEnum] {shaderName} stage={stage}");

        if (program.Has_PlayerSubPrograms() && program.PlayerSubPrograms is not null)
        {
            for (int groupIndex = 0; groupIndex < program.PlayerSubPrograms.Count; groupIndex++)
            {
                AssetList<SerializedPlayerSubProgram> group = program.PlayerSubPrograms[groupIndex];
                AssetList<uint>? paramGroup = program.Has_ParameterBlobIndices() && program.ParameterBlobIndices is not null && groupIndex < program.ParameterBlobIndices.Count
                    ? program.ParameterBlobIndices[groupIndex]
                    : null;
                for (int i = 0; i < group.Count; i++)
                {
                    SerializedPlayerSubProgram playerSubProgram = group[i];
                    uint? parameterBlobIndex = paramGroup is not null && i < paramGroup.Count ? paramGroup[i] : null;
                    ShaderGpuProgramType unityType = ToUnityProgramType(version, playerSubProgram.GpuProgramType);
                    GPUPlatform resolvedPlatform = ProgramTypeToPlatform(unityType);
                    Console.WriteLine($"[ShaderEnum]   Player group={groupIndex} index={i} blob={playerSubProgram.BlobIndex} paramBlob={(parameterBlobIndex.HasValue ? parameterBlobIndex.Value.ToString() : "<none>")} rawType={playerSubProgram.GpuProgramType} unityType={unityType} platform={resolvedPlatform} keywords=[{string.Join(",", playerSubProgram.KeywordIndices ?? [])}]");
                }
            }
        }

        for (int i = 0; i < program.SubPrograms.Count; i++)
        {
            ISerializedSubProgram subProgram = program.SubPrograms[i];
            ShaderGpuProgramType unityType = ToUnityProgramType(version, (sbyte)subProgram.GpuProgramType);
            GPUPlatform resolvedPlatform = ProgramTypeToPlatform(unityType);
            Console.WriteLine($"[ShaderEnum]   Flat index={i} blob={subProgram.BlobIndex} rawType={(sbyte)subProgram.GpuProgramType} unityType={unityType} platform={resolvedPlatform} keywords=[{string.Join(",", subProgram.KeywordIndices ?? [])}]");
        }
    }

    private static bool MatchesPlatform(UnityVersion version, sbyte rawType, GPUPlatform platform)
    {
        ShaderGpuProgramType ut = ToUnityProgramType(version, rawType);
        return ProgramTypeToPlatform(ut) == platform;
    }

    private static ShaderGpuProgramType ToUnityProgramType(UnityVersion version, sbyte rawType)
    {
		int value = rawType;
		if (value < 0)
		{
			throw new NotSupportedException($"Unsupported negative gpu program type {value}");
		}

		if (GpuProgramTypeExtensions.GpuProgramType55Relevant(version))
		{
			if (Enum.IsDefined(typeof(ShaderGpuProgramType55), value))
			{
				return ((ShaderGpuProgramType55)value).ToGpuProgramType();
			}

			if (Enum.IsDefined(typeof(ShaderGpuProgramType), value))
			{
				return (ShaderGpuProgramType)value;
			}
		}
		else if (Enum.IsDefined(typeof(ShaderGpuProgramType53), value))
		{
			return ((ShaderGpuProgramType53)value).ToGpuProgramType();
		}

		throw new NotSupportedException($"Unsupported gpu program type {value} for Unity {version}");
    }

    private static GPUPlatform ProgramTypeToPlatform(ShaderGpuProgramType type)
    {
        return type switch
        {
            ShaderGpuProgramType.SPIRV => GPUPlatform.Vulkan,
            ShaderGpuProgramType.MetalVS or ShaderGpuProgramType.MetalFS => GPUPlatform.Metal,
            ShaderGpuProgramType.DX11VertexSM40
                or ShaderGpuProgramType.DX11VertexSM50
                or ShaderGpuProgramType.DX11PixelSM40
                or ShaderGpuProgramType.DX11PixelSM50
                or ShaderGpuProgramType.DX11GeometrySM40
                or ShaderGpuProgramType.DX11GeometrySM50
                or ShaderGpuProgramType.DX11HullSM50
                or ShaderGpuProgramType.DX11DomainSM50 => GPUPlatform.D3D11,
            ShaderGpuProgramType.DX10Level9Vertex
                or ShaderGpuProgramType.DX10Level9Pixel => GPUPlatform.D3D11_9x,
            ShaderGpuProgramType.DX9VertexSM20
                or ShaderGpuProgramType.DX9VertexSM30
                or ShaderGpuProgramType.DX9PixelSM20
                or ShaderGpuProgramType.DX9PixelSM30 => GPUPlatform.D3D9,
            ShaderGpuProgramType.GLES => GPUPlatform.Gles20,
            ShaderGpuProgramType.GLES3
                or ShaderGpuProgramType.GLES31
                or ShaderGpuProgramType.GLES31AEP => GPUPlatform.Gles3x,
            ShaderGpuProgramType.GLCore32
                or ShaderGpuProgramType.GLCore41
                or ShaderGpuProgramType.GLCore43 => GPUPlatform.GlCore,
            ShaderGpuProgramType.GLLegacy => GPUPlatform.OpenGL,
            ShaderGpuProgramType.PS5NGGC => GPUPlatform.PS5NGGC,
            ShaderGpuProgramType.RayTracing => GPUPlatform.Unknown,
            _ => GPUPlatform.Unknown,
        };
    }

    /// <summary>
    /// 单个 pass 的读取上下文，随 <see cref="IShaderExportObserver.OnPassSymbolsRead"/> 传给观察者。
    /// </summary>
    /// <param name="ShaderName">Shader 资产名。</param>
    /// <param name="SubShaderIndex">所在的 SubShader 序号。</param>
    /// <param name="PassIndex">所在的 Pass 序号。</param>
    /// <param name="BlobIndex">blob 索引（对应平台 blob 内的 subprogram 条目）。</param>
    /// <param name="Version">资产所属 Unity 版本。</param>
    /// <param name="Stage">着色器阶段名（Vertex/Fragment/Geometry/Hull/Domain/RayTracing）。</param>
    /// <param name="Platform">本 pass 实际使用的 GPU 平台。</param>
    /// <param name="CommonSymbols">来自 program 公共参数表的符号。</param>
    /// <param name="ParameterSymbols">来自 subprogram 自身参数表的符号。</param>
    public readonly record struct ShaderReadContext(
        string ShaderName,
        int SubShaderIndex,
        int PassIndex,
        uint BlobIndex,
        UnityVersion Version,
        string Stage,
        GPUPlatform Platform,
        SerializedProgramData CommonSymbols,
        SerializedProgramData ParameterSymbols);

    /// <summary>
    /// 读到的单个 pass 的只读视图，随 <see cref="IShaderExportObserver.OnShaderSymbolsRead"/> 传给观察者。
    /// </summary>
    public sealed record ShaderPassView(SerializedProgramData Symbols, int SubShaderIndex, int PassIndex, string Stage, bool IsDxbc, uint BlobIndex, byte[] Binary, IReadOnlyList<ushort> KeywordIndices);

    /// <summary>
    /// 单个 pass 的反编译结果视图，随 <see cref="IShaderExportObserver.OnShaderDecompiled"/> 传给观察者。
    /// </summary>
    public sealed record ShaderPassResultView(SerializedProgramData Symbols, byte[] Binary, string PassStem, bool Success);

    private sealed record ShaderReadSource(uint BlobIndex, uint? ParameterBlobIndex, List<ushort> KeywordIndices, ISerializedProgramParameters? Parameters);

    private sealed record ShaderReadPass(
        string PassName,
        int SubShaderIndex,
        int PassIndex,
        string Stage,
        uint BlobIndex,
        uint? ParameterBlobIndex,
        List<ushort> KeywordIndices,
        ShaderSubProgram SubProgram,
        SerializedProgramData CommonSymbols,
        SerializedProgramData ParameterSymbols,
        byte[] Binary,
        string ShaderName,
        UnityVersion Version);

    private sealed record ShaderSymbolPass(ShaderReadPass Read, SerializedProgramData Symbols);
}
