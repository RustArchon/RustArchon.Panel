// Copyright ©2026 Scott Blomfield

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Refit;
using RustArchon.Shared.DTOs;

namespace RustArchon.Panel.Clients;

/// <summary>Refit client for <c>AdminTicketStatusesController</c> - the admin ticket-status management
/// page.</summary>
public interface IAdminTicketStatusApiClient
{
    [Get("/")]
    Task<List<TicketStatusDto>> ListAsync();

    [Get("/{id}")]
    Task<TicketStatusDto> GetAsync(Guid id);

    [Post("/")]
    Task<TicketStatusDto> CreateAsync([Body] CreateTicketStatusRequestDto request);

    [Put("/{id}")]
    Task<TicketStatusDto> UpdateAsync(Guid id, [Body] UpdateTicketStatusRequestDto request);

    [Delete("/{id}")]
    Task DeleteAsync(Guid id);
}
