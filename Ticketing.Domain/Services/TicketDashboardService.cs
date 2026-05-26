using System.Runtime.CompilerServices;
using Ticketing.Data.Models;
using Ticketing.Data.Stores;
using Ticketing.Domain.Exceptions;
using Ticketing.Domain.Models;

namespace Ticketing.Domain.Services;

internal sealed class TicketDashboardService : ITicketDashboardService
{
	private static readonly TicketStatus[] DashboardStatuses =
	[
		TicketStatus.Open,
		TicketStatus.InProgress,
		TicketStatus.PendingRequester,
		TicketStatus.PendingVendor,
		TicketStatus.Resolved,
		TicketStatus.Closed,
		TicketStatus.Cancelled
	];

	private static readonly TicketStatus[] ActiveStatuses =
	[
		TicketStatus.Open,
		TicketStatus.InProgress,
		TicketStatus.PendingRequester,
		TicketStatus.PendingVendor,
		TicketStatus.Resolved
	];

	private readonly CurrentUserService _currentUser;
	private readonly ITicketQueryStore _ticketQueryStore;
	private readonly ITeamStore _teamStore;
	private readonly ITicketPermissionService _permissions;

	public TicketDashboardService(
		CurrentUserService currentUser,
		ITicketQueryStore ticketQueryStore,
		ITeamStore teamStore,
		ITicketPermissionService permissions)
	{
		_currentUser = currentUser;
		_ticketQueryStore = ticketQueryStore;
		_teamStore = teamStore;
		_permissions = permissions;
	}

	public Task<DomainResult<TicketDashboardSummary>> GetSummaryAsync(
		string? teamId = null,
		CancellationToken cancellationToken = default) =>
		DomainResult<TicketDashboardSummary>.TryAsync(async () =>
		{
			var userOid = _currentUser.RequireUserOid();
			var normalizedTeamId = string.IsNullOrWhiteSpace(teamId) ? null : teamId.Trim();

			if (normalizedTeamId is not null
				&& !_permissions.CanManageTeams()
				&& !await _teamStore.IsUserOnTeamAsync(userOid, normalizedTeamId, cancellationToken))
			{
				throw new TicketingForbiddenException("You do not have access to this team's dashboard.");
			}

			var statusCounts = new List<TicketStatusCount>();
			foreach (var status in DashboardStatuses)
			{
				statusCounts.Add(new TicketStatusCount
				{
					Status = status,
					Count = await CountVisibleAsync(GetDashboardTicketsAsync(normalizedTeamId, status, cancellationToken), cancellationToken)
				});
			}

			var teamCounts = await GetTeamCountsAsync(userOid, cancellationToken);

			return new TicketDashboardSummary
			{
				StatusCounts = statusCounts,
				MyOpenTicketCount = await CountSubmittedActiveAsync(userOid, cancellationToken),
				AssignedToMeCount = _permissions.IsTechnicianOrAbove()
					? await CountAssignedActiveAsync(userOid, cancellationToken)
					: 0,
				UnassignedOpenCount = _permissions.CanViewAllTickets()
					? await CountRawAsync(_ticketQueryStore.GetUnassignedAsync(TicketStatus.Open, null, cancellationToken), cancellationToken)
					: 0,
				PendingRequesterCount = statusCounts.First(count => count.Status == TicketStatus.PendingRequester).Count,
				PendingVendorCount = statusCounts.First(count => count.Status == TicketStatus.PendingVendor).Count,
				ResolvedCount = statusCounts.First(count => count.Status == TicketStatus.Resolved).Count,
				TeamCounts = teamCounts
			};
		});

	private async IAsyncEnumerable<TicketSummary> GetDashboardTicketsAsync(
		string? teamId,
		TicketStatus status,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		if (!string.IsNullOrWhiteSpace(teamId))
		{
			await foreach (var ticket in _ticketQueryStore.GetByTeamAsync(teamId, status, null, cancellationToken))
			{
				yield return ticket;
			}

			yield break;
		}

		if (_permissions.CanViewAllTickets())
		{
			await foreach (var ticket in _ticketQueryStore.GetByStatusAsync(status, null, cancellationToken))
			{
				yield return ticket;
			}

			yield break;
		}

		var userOid = _currentUser.RequireUserOid();
		await foreach (var ticket in _ticketQueryStore.GetSubmittedAsync(userOid, status, null, cancellationToken))
		{
			yield return ticket;
		}

		if (!_permissions.IsTechnicianOrAbove())
		{
			yield break;
		}

		await foreach (var ticket in _ticketQueryStore.GetAssignedAsync(userOid, status, null, cancellationToken))
		{
			yield return ticket;
		}

		await foreach (var membership in _teamStore.GetMembershipsForUserAsync(userOid, false, null, cancellationToken))
		{
			await foreach (var ticket in _ticketQueryStore.GetByTeamAsync(membership.TeamId, status, null, cancellationToken))
			{
				yield return ticket;
			}
		}
	}

	private async Task<IReadOnlyList<TeamTicketCount>> GetTeamCountsAsync(string userOid, CancellationToken cancellationToken)
	{
		var teamIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		if (_permissions.CanViewAllTickets())
		{
			await foreach (var team in _teamStore.GetTeamsAsync(false, 100, cancellationToken))
			{
				teamIds.Add(team.TeamId);
			}
		}
		else if (_permissions.IsTechnicianOrAbove())
		{
			await foreach (var membership in _teamStore.GetMembershipsForUserAsync(userOid, false, 100, cancellationToken))
			{
				teamIds.Add(membership.TeamId);
			}
		}

		var counts = new List<TeamTicketCount>();
		foreach (var teamId in teamIds.Order(StringComparer.OrdinalIgnoreCase))
		{
			counts.Add(new TeamTicketCount
			{
				TeamId = teamId,
				OpenCount = await CountRawAsync(_ticketQueryStore.GetByTeamAsync(teamId, TicketStatus.Open, null, cancellationToken), cancellationToken),
				InProgressCount = await CountRawAsync(_ticketQueryStore.GetByTeamAsync(teamId, TicketStatus.InProgress, null, cancellationToken), cancellationToken),
				PendingCount =
					await CountRawAsync(_ticketQueryStore.GetByTeamAsync(teamId, TicketStatus.PendingRequester, null, cancellationToken), cancellationToken)
					+ await CountRawAsync(_ticketQueryStore.GetByTeamAsync(teamId, TicketStatus.PendingVendor, null, cancellationToken), cancellationToken)
			});
		}

		return counts;
	}

	private async Task<int> CountSubmittedActiveAsync(string userOid, CancellationToken cancellationToken)
	{
		var count = 0;
		foreach (var status in ActiveStatuses)
		{
			count += await CountRawAsync(_ticketQueryStore.GetSubmittedAsync(userOid, status, null, cancellationToken), cancellationToken);
		}

		return count;
	}

	private async Task<int> CountAssignedActiveAsync(string userOid, CancellationToken cancellationToken)
	{
		var count = 0;
		foreach (var status in ActiveStatuses)
		{
			count += await CountRawAsync(_ticketQueryStore.GetAssignedAsync(userOid, status, null, cancellationToken), cancellationToken);
		}

		return count;
	}

	private async Task<int> CountVisibleAsync(IAsyncEnumerable<TicketSummary> tickets, CancellationToken cancellationToken)
	{
		var count = 0;
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		await foreach (var ticket in tickets.WithCancellation(cancellationToken))
		{
			if (seen.Add(ticket.TicketId)
				&& await _permissions.CanViewTicketSummaryAsync(ticket, cancellationToken))
			{
				count++;
			}
		}

		return count;
	}

	private static async Task<int> CountRawAsync(IAsyncEnumerable<TicketSummary> tickets, CancellationToken cancellationToken)
	{
		var count = 0;
		await foreach (var _ in tickets.WithCancellation(cancellationToken))
		{
			count++;
		}

		return count;
	}
}
