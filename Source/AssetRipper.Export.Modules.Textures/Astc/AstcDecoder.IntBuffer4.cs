using System.Runtime.CompilerServices;

namespace AssetRipper.Export.Modules.Textures.Astc;

public static partial class AstcDecoder
{
	[InlineArray(4)]
	private struct IntBuffer4
	{
		private int _element0;
	}
}
