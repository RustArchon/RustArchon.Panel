// Copyright ©2026 Scott Blomfield

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Refit;
using RustArchon.Shared.DTOs;

namespace RustArchon.Panel.Clients;

/// <summary>
/// Refit client for <c>TicketsController</c> - the caller's own Organization's support tickets.
/// </summary>
public interface ITicketApiClient
{
    [Get("/queues")]
    Task<List<QueueDto>> GetQueuesAsync();

    [Get("/")]
    Task<List<TicketSummaryDto>> ListAsync();

    [Get("/{id}")]
    Task<TicketDetailDto> GetAsync(Guid id);

    [Post("/")]
    Task<TicketDetailDto> CreateAsync([Body] CreateTicketRequestDto request);

    [Post("/{id}/messages")]
    Task<TicketMessageDto> AddMessageAsync(Guid id, [Body] SaveTicketMessageRequestDto request);
}
