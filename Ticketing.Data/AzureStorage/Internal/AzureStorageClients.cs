using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.Extensions.Options;
using Ticketing.Data.Configuration;

namespace Ticketing.Data.AzureStorage.Internal;

internal sealed class AzureStorageClients
{
	private readonly TicketingDataOptions _options;

	public AzureStorageClients(
		TableServiceClient tableServiceClient,
		BlobServiceClient blobServiceClient,
		QueueServiceClient queueServiceClient,
		IOptions<TicketingDataOptions> options)
	{
		TableServiceClient = tableServiceClient;
		BlobServiceClient = blobServiceClient;
		QueueServiceClient = queueServiceClient;
		_options = options.Value;
	}

	public TableServiceClient TableServiceClient { get; }

	public BlobServiceClient BlobServiceClient { get; }

	public QueueServiceClient QueueServiceClient { get; }

	public TableClient Tickets => TableServiceClient.GetTableClient(_options.Tables.Tickets);

	public TableClient TicketLookups => TableServiceClient.GetTableClient(_options.Tables.TicketLookups);

	public TableClient TicketNotes => TableServiceClient.GetTableClient(_options.Tables.TicketNotes);

	public TableClient TicketAudit => TableServiceClient.GetTableClient(_options.Tables.TicketAudit);

	public TableClient TicketAttachments => TableServiceClient.GetTableClient(_options.Tables.TicketAttachments);

	public TableClient TicketTaxonomy => TableServiceClient.GetTableClient(_options.Tables.TicketTaxonomy);

	public TableClient UserProfiles => TableServiceClient.GetTableClient(_options.Tables.UserProfiles);

	public TableClient TicketsByAssignee => TableServiceClient.GetTableClient(_options.Tables.TicketsByAssignee);

	public TableClient TicketsBySubmitter => TableServiceClient.GetTableClient(_options.Tables.TicketsBySubmitter);

	public TableClient TicketsByStatus => TableServiceClient.GetTableClient(_options.Tables.TicketsByStatus);

	public TableClient TicketsByQueue => TableServiceClient.GetTableClient(_options.Tables.TicketsByQueue);

	public TableClient TicketsByTag => TableServiceClient.GetTableClient(_options.Tables.TicketsByTag);

	public TableClient TicketsByTeam => TableServiceClient.GetTableClient(_options.Tables.TicketsByTeam);

	public TableClient Teams => TableServiceClient.GetTableClient(_options.Tables.Teams);

	public TableClient TeamMembers => TableServiceClient.GetTableClient(_options.Tables.TeamMembers);

	public TableClient TeamRouting => TableServiceClient.GetTableClient(_options.Tables.TeamRouting);

	public BlobContainerClient AttachmentsContainer => BlobServiceClient.GetBlobContainerClient(_options.AttachmentsContainerName);

	public QueueClient WorkQueue => QueueServiceClient.GetQueueClient(_options.WorkQueueName);

	public QueueClient EmailNotificationQueue => QueueServiceClient.GetQueueClient(_options.EmailNotificationQueueName);

	public IEnumerable<TableClient> AllTables()
	{
		yield return Tickets;
		yield return TicketLookups;
		yield return TicketNotes;
		yield return TicketAudit;
		yield return TicketAttachments;
		yield return TicketTaxonomy;
		yield return UserProfiles;
		yield return TicketsByAssignee;
		yield return TicketsBySubmitter;
		yield return TicketsByStatus;
		yield return TicketsByQueue;
		yield return TicketsByTag;
		yield return TicketsByTeam;
		yield return Teams;
		yield return TeamMembers;
		yield return TeamRouting;
	}
}
