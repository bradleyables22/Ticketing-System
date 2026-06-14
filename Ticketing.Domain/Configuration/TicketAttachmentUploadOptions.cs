namespace Ticketing.Domain.Configuration;

public sealed class TicketAttachmentUploadOptions
{
	public const long DefaultMaxSizeBytes = 10 * 1024 * 1024;

	public long MaxSizeBytes { get; set; } = DefaultMaxSizeBytes;

	public IReadOnlyCollection<string> AllowedContentTypes { get; set; } =
	[
		"image/jpeg",
		"image/png",
		"image/gif",
		"image/webp",
		"image/bmp",
		"image/tiff",
		"image/heic",
		"image/heif",
		"image/avif"
	];

	public IReadOnlyCollection<string> AllowedExtensions { get; set; } =
	[
		".jpg",
		".jpeg",
		".png",
		".gif",
		".webp",
		".bmp",
		".tif",
		".tiff",
		".heic",
		".heif",
		".avif"
	];

	public bool ValidateImageSignatures { get; set; } = true;
}
