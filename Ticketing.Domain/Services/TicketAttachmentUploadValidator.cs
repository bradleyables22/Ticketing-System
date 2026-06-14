using Ticketing.Domain.Configuration;
using Ticketing.Domain.Exceptions;
using Ticketing.Domain.Models;

namespace Ticketing.Domain.Services;

internal sealed class TicketAttachmentUploadValidator
{
	private const int MaxHeaderBytes = 64;
	private const string OctetStreamContentType = "application/octet-stream";

	private readonly TicketAttachmentUploadOptions _options;

	public TicketAttachmentUploadValidator(TicketAttachmentUploadOptions options)
	{
		_options = options;
	}

	public async Task<ValidatedTicketAttachmentUpload> ValidateAsync(
		UploadTicketAttachmentCommand command,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(command.OriginalFileName);
		ArgumentNullException.ThrowIfNull(command.Content);

		var originalFileName = Path.GetFileName(command.OriginalFileName);
		if (string.IsNullOrWhiteSpace(originalFileName))
		{
			throw new TicketingValidationException("Attachment file name is required.");
		}

		var sizeBytes = GetKnownSize(command);
		if (!sizeBytes.HasValue)
		{
			throw new TicketingValidationException("Attachment size is required.");
		}

		if (sizeBytes.Value <= 0)
		{
			throw new TicketingValidationException("Attachment content is required.");
		}

		if (sizeBytes.Value > _options.MaxSizeBytes)
		{
			throw new TicketingPayloadTooLargeException(
				$"Attachment size must be {FormatBytes(_options.MaxSizeBytes)} or less.");
		}

		var allowedContentTypes = NormalizeContentTypes(_options.AllowedContentTypes);
		var allowedExtensions = NormalizeExtensions(_options.AllowedExtensions);
		if (allowedContentTypes.Count == 0 || allowedExtensions.Count == 0)
		{
			throw new TicketingValidationException("Attachment upload policy must allow at least one image type and extension.");
		}

		var extension = Path.GetExtension(originalFileName);
		if (string.IsNullOrWhiteSpace(extension) || !allowedExtensions.Contains(NormalizeExtension(extension)))
		{
			throw new TicketingValidationException(
				$"Attachment file extension must be one of: {string.Join(", ", allowedExtensions.Order(StringComparer.OrdinalIgnoreCase))}.");
		}

		var declaredContentType = NormalizeContentType(command.ContentType);
		if (IsMeaningfulContentType(declaredContentType) && !allowedContentTypes.Contains(declaredContentType))
		{
			throw new TicketingValidationException(
				$"Attachment content type must be one of: {string.Join(", ", allowedContentTypes.Order(StringComparer.OrdinalIgnoreCase))}.");
		}

		var content = command.Content;
		var contentType = declaredContentType;
		if (_options.ValidateImageSignatures)
		{
			var inspection = await InspectHeaderAsync(command.Content, cancellationToken);
			content = inspection.Content;

			var imageType = ImageSignature.Detect(inspection.Header)
				?? throw new TicketingValidationException("Attachment content must be a supported image file.");

			if (!allowedContentTypes.Contains(imageType.ContentType))
			{
				throw new TicketingValidationException($"Attachment image type '{imageType.ContentType}' is not allowed.");
			}

			if (!imageType.Extensions.Contains(NormalizeExtension(extension)))
			{
				throw new TicketingValidationException("Attachment file extension does not match the image content.");
			}

			if (IsMeaningfulContentType(declaredContentType) && !ContentTypeMatches(declaredContentType, imageType.ContentType))
			{
				throw new TicketingValidationException("Attachment content type does not match the image content.");
			}

			contentType = imageType.ContentType;
		}
		else if (!IsMeaningfulContentType(contentType))
		{
			throw new TicketingValidationException("Attachment image content type is required.");
		}

		return new ValidatedTicketAttachmentUpload
		{
			OriginalFileName = originalFileName,
			ContentType = contentType,
			Content = content,
			SizeBytes = sizeBytes.Value
		};
	}

	private static long? GetKnownSize(UploadTicketAttachmentCommand command)
	{
		if (command.SizeBytes.HasValue)
		{
			return command.SizeBytes.Value;
		}

		return command.Content.CanSeek
			? Math.Max(0, command.Content.Length - command.Content.Position)
			: null;
	}

	private static async Task<AttachmentHeaderInspection> InspectHeaderAsync(
		Stream content,
		CancellationToken cancellationToken)
	{
		var header = new byte[MaxHeaderBytes];
		if (content.CanSeek)
		{
			var originalPosition = content.Position;
			var bytesRead = await content.ReadAsync(header, cancellationToken);
			content.Position = originalPosition;

			return new AttachmentHeaderInspection(header[..bytesRead], content);
		}

		var read = 0;
		while (read < header.Length)
		{
			var bytesRead = await content.ReadAsync(header.AsMemory(read), cancellationToken);
			if (bytesRead == 0)
			{
				break;
			}

			read += bytesRead;
		}

		return new AttachmentHeaderInspection(header[..read], new PrefixReplayStream(header[..read], content));
	}

	private static bool IsMeaningfulContentType(string? contentType) =>
		!string.IsNullOrWhiteSpace(contentType)
		&& !contentType.Equals(OctetStreamContentType, StringComparison.OrdinalIgnoreCase);

	private static bool ContentTypeMatches(string declaredContentType, string detectedContentType) =>
		declaredContentType.Equals(detectedContentType, StringComparison.OrdinalIgnoreCase)
		|| (detectedContentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase)
			&& declaredContentType.Equals("image/jpg", StringComparison.OrdinalIgnoreCase));

	private static HashSet<string> NormalizeContentTypes(IEnumerable<string> contentTypes) =>
		contentTypes
			.Select(NormalizeContentType)
			.Where(contentType => !string.IsNullOrWhiteSpace(contentType))
			.ToHashSet(StringComparer.OrdinalIgnoreCase);

	private static string NormalizeContentType(string? contentType)
	{
		if (string.IsNullOrWhiteSpace(contentType))
		{
			return string.Empty;
		}

		var separatorIndex = contentType.IndexOf(';', StringComparison.Ordinal);
		var normalized = separatorIndex >= 0
			? contentType[..separatorIndex]
			: contentType;

		return normalized.Trim().ToLowerInvariant();
	}

	private static HashSet<string> NormalizeExtensions(IEnumerable<string> extensions) =>
		extensions
			.Select(NormalizeExtension)
			.Where(extension => !string.IsNullOrWhiteSpace(extension))
			.ToHashSet(StringComparer.OrdinalIgnoreCase);

	private static string NormalizeExtension(string extension)
	{
		var normalized = extension.Trim().ToLowerInvariant();
		return normalized.StartsWith('.')
			? normalized
			: $".{normalized}";
	}

	private static string FormatBytes(long bytes)
	{
		const long mebibyte = 1024 * 1024;
		return bytes >= mebibyte && bytes % mebibyte == 0
			? $"{bytes / mebibyte} MiB"
			: $"{bytes:N0} bytes";
	}

	private sealed record AttachmentHeaderInspection(byte[] Header, Stream Content);

	private sealed record ImageSignature(string ContentType, IReadOnlyCollection<string> Extensions)
	{
		private static readonly ImageSignature Jpeg = new("image/jpeg", [".jpg", ".jpeg"]);
		private static readonly ImageSignature Png = new("image/png", [".png"]);
		private static readonly ImageSignature Gif = new("image/gif", [".gif"]);
		private static readonly ImageSignature Webp = new("image/webp", [".webp"]);
		private static readonly ImageSignature Bmp = new("image/bmp", [".bmp"]);
		private static readonly ImageSignature Tiff = new("image/tiff", [".tif", ".tiff"]);
		private static readonly ImageSignature Heif = new("image/heif", [".heif"]);
		private static readonly ImageSignature Heic = new("image/heic", [".heic"]);
		private static readonly ImageSignature Avif = new("image/avif", [".avif"]);

		public static ImageSignature? Detect(ReadOnlySpan<byte> header)
		{
			if (StartsWith(header, [0xFF, 0xD8, 0xFF]))
			{
				return Jpeg;
			}

			if (StartsWith(header, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
			{
				return Png;
			}

			if (StartsWithAscii(header, "GIF87a") || StartsWithAscii(header, "GIF89a"))
			{
				return Gif;
			}

			if (header.Length >= 12 && StartsWithAscii(header, "RIFF") && HasAsciiAt(header, 8, "WEBP"))
			{
				return Webp;
			}

			if (StartsWithAscii(header, "BM"))
			{
				return Bmp;
			}

			if (StartsWith(header, [0x49, 0x49, 0x2A, 0x00]) || StartsWith(header, [0x4D, 0x4D, 0x00, 0x2A]))
			{
				return Tiff;
			}

			if (header.Length >= 12 && HasAsciiAt(header, 4, "ftyp"))
			{
				if (ContainsIsoBrand(header, "avif") || ContainsIsoBrand(header, "avis"))
				{
					return Avif;
				}

				if (ContainsIsoBrand(header, "heic")
					|| ContainsIsoBrand(header, "heix")
					|| ContainsIsoBrand(header, "hevc")
					|| ContainsIsoBrand(header, "hevx"))
				{
					return Heic;
				}

				if (ContainsIsoBrand(header, "mif1") || ContainsIsoBrand(header, "msf1"))
				{
					return Heif;
				}
			}

			return null;
		}

		private static bool StartsWith(ReadOnlySpan<byte> header, ReadOnlySpan<byte> signature) =>
			header.Length >= signature.Length && header[..signature.Length].SequenceEqual(signature);

		private static bool StartsWithAscii(ReadOnlySpan<byte> header, string value) =>
			HasAsciiAt(header, 0, value);

		private static bool HasAsciiAt(ReadOnlySpan<byte> header, int offset, string value) =>
			header.Length >= offset + value.Length && ReadAscii(header.Slice(offset, value.Length)).Equals(value, StringComparison.Ordinal);

		private static bool ContainsIsoBrand(ReadOnlySpan<byte> header, string brand)
		{
			if (HasAsciiAt(header, 8, brand))
			{
				return true;
			}

			for (var offset = 16; offset + brand.Length <= header.Length; offset += 4)
			{
				if (HasAsciiAt(header, offset, brand))
				{
					return true;
				}
			}

			return false;
		}

		private static string ReadAscii(ReadOnlySpan<byte> bytes) =>
			string.Create(bytes.Length, bytes, static (chars, source) =>
			{
				for (var i = 0; i < source.Length; i++)
				{
					chars[i] = (char)source[i];
				}
			});
	}

	private sealed class PrefixReplayStream : Stream
	{
		private readonly byte[] _prefix;
		private readonly Stream _inner;
		private int _prefixPosition;

		public PrefixReplayStream(byte[] prefix, Stream inner)
		{
			_prefix = prefix;
			_inner = inner;
		}

		public override bool CanRead => true;

		public override bool CanSeek => false;

		public override bool CanWrite => false;

		public override long Length => throw new NotSupportedException();

		public override long Position
		{
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		public override void Flush()
		{
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			ArgumentNullException.ThrowIfNull(buffer);
			var readFromPrefix = ReadPrefix(buffer.AsSpan(offset, count));
			if (readFromPrefix == count)
			{
				return readFromPrefix;
			}

			return readFromPrefix + _inner.Read(buffer, offset + readFromPrefix, count - readFromPrefix);
		}

		public override async ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default)
		{
			var readFromPrefix = ReadPrefix(buffer.Span);
			if (readFromPrefix == buffer.Length)
			{
				return readFromPrefix;
			}

			return readFromPrefix + await _inner.ReadAsync(buffer[readFromPrefix..], cancellationToken);
		}

		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

		public override void SetLength(long value) => throw new NotSupportedException();

		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

		private int ReadPrefix(Span<byte> buffer)
		{
			if (_prefixPosition >= _prefix.Length || buffer.Length == 0)
			{
				return 0;
			}

			var bytesToCopy = Math.Min(buffer.Length, _prefix.Length - _prefixPosition);
			_prefix.AsSpan(_prefixPosition, bytesToCopy).CopyTo(buffer);
			_prefixPosition += bytesToCopy;
			return bytesToCopy;
		}
	}
}

internal sealed record ValidatedTicketAttachmentUpload
{
	public required string OriginalFileName { get; init; }

	public required string ContentType { get; init; }

	public required Stream Content { get; init; }

	public required long SizeBytes { get; init; }
}
