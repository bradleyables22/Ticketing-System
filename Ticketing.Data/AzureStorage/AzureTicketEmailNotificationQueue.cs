using System.Text.Json;
using System.Text.Json.Serialization;
using Ticketing.Data.AzureStorage.Internal;
using Ticketing.Data.Models;
using Ticketing.Data.Stores;

namespace Ticketing.Data.AzureStorage;

internal sealed class AzureTicketEmailNotificationQueue : ITicketEmailNotificationQueue
{
	private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

	private readonly AzureStorageClients _clients;

	public AzureTicketEmailNotificationQueue(AzureStorageClients clients)
	{
		_clients = clients;
	}

	public Task EnqueueAsync(
		QueueTicketEmailNotificationRequest request,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(request.EventName);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.TemplateKey);
		ArgumentNullException.ThrowIfNull(request.Ticket);
		ArgumentNullException.ThrowIfNull(request.Actor);

		if (request.Recipients.Count == 0)
		{
			return Task.CompletedTask;
		}

		var message = new TicketEmailNotificationQueueMessage
		{
			NotificationId = StorageKeys.NewId(),
			EventName = request.EventName.Trim(),
			TemplateKey = request.TemplateKey.Trim(),
			CreatedUtc = DateTimeOffset.UtcNow,
			Ticket = request.Ticket,
			Actor = request.Actor,
			Recipients = request.Recipients,
			Data = request.Data
		};

		return _clients.EmailNotificationQueue.SendMessageAsync(
			JsonSerializer.Serialize(message, SerializerOptions),
			cancellationToken);
	}

	private static JsonSerializerOptions CreateSerializerOptions()
	{
		var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
		options.Converters.Add(new JsonStringEnumConverter());
		return options;
	}
}
