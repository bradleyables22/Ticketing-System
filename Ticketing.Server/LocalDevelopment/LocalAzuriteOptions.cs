namespace Ticketing.Server.LocalDevelopment;

internal sealed class LocalAzuriteOptions
{
	public bool Enabled { get; set; } = true;

	public bool UseNpxFallback { get; set; } = true;

	public bool StopOnShutdown { get; set; } = true;

	public bool SkipApiVersionCheck { get; set; } = true;

	public string DataPath { get; set; } = "../.azurite";

	public int BlobPort { get; set; } = 10000;

	public int QueuePort { get; set; } = 10001;

	public int TablePort { get; set; } = 10002;

	public int StartupTimeoutSeconds { get; set; } = 90;
}
