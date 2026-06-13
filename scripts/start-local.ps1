$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$azuriteDataPath = Join-Path $repoRoot ".azurite"
$azuriteOutputLog = Join-Path $azuriteDataPath "azurite.log"
$azuriteErrorLog = Join-Path $azuriteDataPath "azurite.err.log"

New-Item -ItemType Directory -Force -Path $azuriteDataPath | Out-Null

function Test-LocalPort {
	param([int] $Port)

	$client = [System.Net.Sockets.TcpClient]::new()
	try {
		$connectTask = $client.ConnectAsync("127.0.0.1", $Port)
		return $connectTask.Wait(300) -and $client.Connected
	}
	catch {
		return $false
	}
	finally {
		$client.Dispose()
	}
}

function Wait-LocalPort {
	param(
		[int] $Port,
		[int] $TimeoutSeconds = 30
	)

	$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
	do {
		if (Test-LocalPort -Port $Port) {
			return
		}

		Start-Sleep -Milliseconds 300
	}
	while ((Get-Date) -lt $deadline)

	throw "Timed out waiting for 127.0.0.1:$Port. Check $azuriteErrorLog for Azurite startup errors."
}

$azuriteAlreadyRunning =
	(Test-LocalPort -Port 10000) -and
	(Test-LocalPort -Port 10001) -and
	(Test-LocalPort -Port 10002)

$azuriteProcess = $null
if ($azuriteAlreadyRunning) {
	Write-Host "Azurite is already listening on ports 10000, 10001, and 10002."
}
else {
	$azuriteCommand = Get-Command azurite.cmd -ErrorAction SilentlyContinue
	if ($azuriteCommand) {
		$filePath = $azuriteCommand.Source
		$argumentList = @("--location", $azuriteDataPath, "--skipApiVersionCheck")
	}
	else {
		$npxCommand = Get-Command npx.cmd -ErrorAction SilentlyContinue
		if (-not $npxCommand) {
			throw "Could not find azurite.cmd or npx.cmd. Install Node.js, then run npm.cmd install -g azurite."
		}

		$filePath = $npxCommand.Source
		$argumentList = @("-y", "azurite", "--location", $azuriteDataPath, "--skipApiVersionCheck")
	}

	Write-Host "Starting Azurite..."
	$azuriteProcess = Start-Process `
		-FilePath $filePath `
		-ArgumentList $argumentList `
		-WorkingDirectory $repoRoot `
		-RedirectStandardOutput $azuriteOutputLog `
		-RedirectStandardError $azuriteErrorLog `
		-WindowStyle Hidden `
		-PassThru

	Wait-LocalPort -Port 10000
	Wait-LocalPort -Port 10001
	Wait-LocalPort -Port 10002
	Write-Host "Azurite is ready."
}

try {
	dotnet run --project (Join-Path $repoRoot "Ticketing.Server")
}
finally {
	if ($azuriteProcess -and -not $azuriteProcess.HasExited) {
		Write-Host "Stopping Azurite..."
		Stop-Process -Id $azuriteProcess.Id -Force
	}
}
