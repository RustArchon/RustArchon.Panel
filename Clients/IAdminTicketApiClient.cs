// Copyright ©2026 Scott Blomfield

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Refit;
using RustArchon.Shared.DTOs;

namespace RustArchon.Panel.Clients;

/// <summary>
/// Refit client for <c>AdminTicketsController</c> - the staff console's ticket queues, across every
/// tenant.
/// </summary>
public interface IAdminTicketApiClient
{
    [Get("/queues")]
    Task<List<QueueDto>> GetQueuesAsync();

    [Get("/statuses")]
    Task<List<TicketStatusDto>> GetStatusesAsync();

    [Get("/")]
    Task<List<TicketSummaryDto>> ListAsync(Guid? queueId, Guid? statusId, bool? isClosed);

    [Get("/{id}")]
    Task<TicketDetailDto> GetAsync(Guid id);

    [Put("/{id}")]
    Task<TicketSummaryDto> UpdateAsync(Guid id, [Body] UpdateTicketRequestDto request);

    [Post("/{id}/messages")]
    Task<TicketMessageDto> AddMessageAsync(Guid id, [Body] SaveTicketMessageRequestDto request);

    [Post("/{id}/notes")]
    Task<TicketNoteDto> AddNoteAsync(Guid id, [Body] SaveTicketNoteRequestDto request);
}
