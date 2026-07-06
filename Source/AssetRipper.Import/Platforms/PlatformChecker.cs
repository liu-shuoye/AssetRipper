using AssetRipper.Import.Configuration;
using AssetRipper.Import.Logging;
using AssetRipper.Import.Structure.Platforms;
using AssetRipper.IO.Files;

namespace AssetRipper.Import.Platforms;

public static class PlatformChecker
{
	public static bool CheckPlatform(List<string> paths, FileSystem fileSystem, [NotNullWhen(true)] out PlatformGameStructure? platformStructure, [NotNullWhen(true)] out MixedGameStructure? mixedStructure) => CheckPlatform(paths, fileSystem, null, out platformStructure, out mixedStructure);

	public static bool CheckPlatform(List<string> paths, FileSystem fileSystem, ImportSettings? importSettings, [NotNullWhen(true)] out PlatformGameStructure? platformStructure, [NotNullWhen(true)] out MixedGameStructure? mixedStructure)
	{
		platformStructure = null;
		mixedStructure = null;

		if (CheckWindows(paths, fileSystem, out WindowsGameStructure? pcGameStructure))
		{
			platformStructure = pcGameStructure;
		}
		else if (CheckLinux(paths, fileSystem, out LinuxGameStructure? linuxGameStructure))
		{
			platformStructure = linuxGameStructure;
		}
		else if (CheckMac(paths, fileSystem, out MacGameStructure? macGameStructure))
		{
			platformStructure = macGameStructure;
		}
		else if (CheckAndroid(paths, fileSystem, out AndroidGameStructure? androidGameStructure))
		{
			platformStructure = androidGameStructure;
		}
		else if (CheckiOS(paths, fileSystem, out iOSGameStructure? iosGameStructure))
		{
			platformStructure = iosGameStructure;
		}
		else if (CheckSwitch(paths, fileSystem, out SwitchGameStructure? switchGameStructure))
		{
			platformStructure = switchGameStructure;
		}
		else if (CheckPS4(paths, fileSystem, out PS4GameStructure? ps4GameStructure))
		{
			platformStructure = ps4GameStructure;
		}
		else if (CheckWebGL(paths, fileSystem, out WebGLGameStructure? webglGameStructure))
		{
			platformStructure = webglGameStructure;
		}
		else if (CheckWebPlayer(paths, fileSystem, out WebPlayerGameStructure? webplayerGameStructure))
		{
			platformStructure = webplayerGameStructure;
		}
		else if (CheckWiiU(paths, fileSystem, out WiiUGameStructure? wiiUGameStructure))
		{
			platformStructure = wiiUGameStructure;
		}
		else if (CheckWindowsPhone(paths, fileSystem, out WindowsPhoneGameStructure? windowsPhoneGameStructure))
		{
			platformStructure = windowsPhoneGameStructure;
		}

		// MixedGameStructure scans directories during construction, so the limits must be
		// supplied via its constructor. The other platform structures scan later through
		// CollectFiles, so ApplyRecursionLimits is sufficient for them.
		if (CheckMixed(paths, fileSystem, importSettings, out MixedGameStructure? mixedGameStructure))
		{
			mixedStructure = mixedGameStructure;
		}

		platformStructure?.ApplyRecursionLimits(importSettings);

		return platformStructure != null || mixedStructure != null;
	}


	private static bool CheckWindows(List<string> paths, FileSystem fileSystem, [NotNullWhen(true)] out WindowsGameStructure? gameStructure)
	{
		foreach (string path in paths)
		{
			if (WindowsGameStructure.Exists(path, fileSystem))
			{
				gameStructure = new WindowsGameStructure(path, fileSystem);
				paths.Remove(path);
				Logger.Info(LogCategory.Import, $"在 '{path}' 找到了 Windows 游戏结构");
				return true;
			}
		}
		gameStructure = null;
		return false;
	}

	private static bool CheckLinux(List<string> paths, FileSystem fileSystem, [NotNullWhen(true)] out LinuxGameStructure? gameStructure)
	{
		foreach (string path in paths)
		{
			if (LinuxGameStructure.Exists(path, fileSystem))
			{
				gameStructure = new LinuxGameStructure(path, fileSystem);
				paths.Remove(path);
				Logger.Info(LogCategory.Import, $"Linux game structure has been found at '{path}'");
				return true;
			}
		}
		gameStructure = null;
		return false;
	}

	private static bool CheckMac(List<string> paths, FileSystem fileSystem, [NotNullWhen(true)] out MacGameStructure? gameStructure)
	{
		foreach (string path in paths)
		{
			if (MacGameStructure.Exists(path, fileSystem))
			{
				gameStructure = new MacGameStructure(path, fileSystem);
				paths.Remove(path);
				Logger.Info(LogCategory.Import, $"Mac game structure has been found at '{path}'");
				return true;
			}
		}
		gameStructure = null;
		return false;
	}

	private static bool CheckAndroid(List<string> paths, FileSystem fileSystem, [NotNullWhen(true)] out AndroidGameStructure? gameStructure)
	{
		string? androidStructure = null;
		string? obbStructure = null;
		foreach (string path in paths)
		{
			if (AndroidGameStructure.IsAndroidStructure(path, fileSystem))
			{
				if (androidStructure == null)
				{
					androidStructure = path;
				}
				else
				{
					throw new Exception("已找到2个安卓游戏结构");
				}
			}
			else if (AndroidGameStructure.IsAndroidObbStructure(path, fileSystem))
			{
				if (obbStructure == null)
				{
					obbStructure = path;
				}
				else
				{
					throw new Exception("2 Android obb game stuctures has been found");
				}
			}
		}

		if (androidStructure != null)
		{
			gameStructure = new AndroidGameStructure(androidStructure, obbStructure, fileSystem);
			paths.Remove(androidStructure);
			Logger.Info(LogCategory.Import, $"在 '{androidStructure}' 中已找到 Android 游戏结构");
			if (obbStructure != null)
			{
				paths.Remove(obbStructure);
				Logger.Info(LogCategory.Import, $"在 '{obbStructure}' 中已找到 Android OBB 游戏结构");
			}
			return true;
		}

		gameStructure = null;
		return false;
	}

	private static bool CheckiOS(List<string> paths, FileSystem fileSystem, [NotNullWhen(true)] out iOSGameStructure? gameStructure)
	{
		foreach (string path in paths)
		{
			if (iOSGameStructure.Exists(path, fileSystem))
			{
				gameStructure = new iOSGameStructure(path, fileSystem);
				paths.Remove(path);
				Logger.Info(LogCategory.Import, $"iOS game structure has been found at '{path}'");
				return true;
			}
		}
		gameStructure = null;
		return false;
	}

	private static bool CheckPS4(List<string> paths, FileSystem fileSystem, [NotNullWhen(true)] out PS4GameStructure? gameStructure)
	{
		foreach (string path in paths)
		{
			if (PS4GameStructure.Exists(path, fileSystem))
			{
				gameStructure = new PS4GameStructure(path, fileSystem);
				paths.Remove(path);
				Logger.Info(LogCategory.Import, $"PS4 game structure has been found at '{path}'");
				return true;
			}
		}
		gameStructure = null;
		return false;
	}

	private static bool CheckSwitch(List<string> paths, FileSystem fileSystem, [NotNullWhen(true)] out SwitchGameStructure? gameStructure)
	{
		foreach (string path in paths)
		{
			if (SwitchGameStructure.Exists(path, fileSystem))
			{
				gameStructure = new SwitchGameStructure(path, fileSystem);
				paths.Remove(path);
				Logger.Info(LogCategory.Import, $"Switch game structure has been found at '{path}'");
				return true;
			}
		}
		gameStructure = null;
		return false;
	}

	private static bool CheckWebGL(List<string> paths, FileSystem fileSystem, [NotNullWhen(true)] out WebGLGameStructure? gameStructure)
	{
		foreach (string path in paths)
		{
			if (WebGLGameStructure.Exists(path, fileSystem))
			{
				gameStructure = new WebGLGameStructure(path, fileSystem);
				paths.Remove(path);
				Logger.Info(LogCategory.Import, $"WebPlayer game structure has been found at '{path}'");
				return true;
			}
		}
		gameStructure = null;
		return false;
	}

	private static bool CheckWebPlayer(List<string> paths, FileSystem fileSystem, [NotNullWhen(true)] out WebPlayerGameStructure? gameStructure)
	{
		foreach (string path in paths)
		{
			if (WebPlayerGameStructure.Exists(path, fileSystem))
			{
				gameStructure = new WebPlayerGameStructure(path, fileSystem);
				paths.Remove(path);
				Logger.Info(LogCategory.Import, $"WebPlayer game structure has been found at '{path}'");
				return true;
			}
		}
		gameStructure = null;
		return false;
	}

	private static bool CheckWiiU(List<string> paths, FileSystem fileSystem, [NotNullWhen(true)] out WiiUGameStructure? gameStructure)
	{
		foreach (string path in paths)
		{
			if (WiiUGameStructure.Exists(path, fileSystem))
			{
				gameStructure = new WiiUGameStructure(path, fileSystem);
				paths.Remove(path);
				Logger.Info(LogCategory.Import, $"WiiU game structure has been found at '{path}'");
				return true;
			}
		}
		gameStructure = null;
		return false;
	}

	private static bool CheckWindowsPhone(List<string> paths, FileSystem fileSystem, [NotNullWhen(true)] out WindowsPhoneGameStructure? gameStructure)
	{
		foreach (string path in paths)
		{
			if (WindowsPhoneGameStructure.Exists(path, fileSystem))
			{
				gameStructure = new WindowsPhoneGameStructure(path, fileSystem);
				paths.Remove(path);
				Logger.Info(LogCategory.Import, $"Windows Phone game structure has been found at '{path}'");
				return true;
			}
		}
		gameStructure = null;
		return false;
	}

	private static bool CheckMixed(List<string> paths, FileSystem fileSystem, ImportSettings? importSettings, [NotNullWhen(true)] out MixedGameStructure? gameStructure)
	{
		if (paths.Count > 0)
		{
			gameStructure = new MixedGameStructure(paths, fileSystem, importSettings);
			if (paths.Count == 1)
			{
				Logger.Info(LogCategory.Import, $"Mixed game structure has been found at {paths[0]}");
			}
			else
			{
				Logger.Info(LogCategory.Import, $"Mixed game structure has been found for {paths.Count} paths");
			}

			paths.Clear();
			return true;
		}
		gameStructure = null;
		return false;
	}
}
