param(
	[string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$build = Join-Path $root "build"

$cmakeCandidates = @(
	"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe",
	"C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe",
	"cmake"
)
$cmake = $null
foreach ($candidate in $cmakeCandidates) {
	if ($candidate -eq "cmake") {
		$cmd = Get-Command cmake -ErrorAction SilentlyContinue
		if ($cmd) { $cmake = $cmd.Source; break }
	} elseif (Test-Path $candidate) {
		$cmake = $candidate
		break
	}
}
if (-not $cmake) {
	throw "cmake not found"
}

& $cmake -S $root -B $build -A x64
if ($LASTEXITCODE -ne 0) { throw "cmake configure failed" }

& $cmake --build $build --config $Configuration --parallel
if ($LASTEXITCODE -ne 0) { throw "cmake build failed" }

$output = Join-Path $build "$Configuration\acl-native.dll"
if (-not (Test-Path $output)) {
	throw "acl-native.dll not produced: $output"
}

$binDir = Join-Path $root "bin"
New-Item -ItemType Directory -Force -Path $binDir | Out-Null
Copy-Item $output (Join-Path $binDir "acl-native.dll") -Force

Write-Output "built: $output"
Write-Output "copied: $(Join-Path $binDir 'acl-native.dll')"
