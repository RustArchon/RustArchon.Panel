// Copyright ©2026 Scott Blomfield

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Refit;
using RustArchon.Shared.DTOs;

namespace RustArchon.Panel.Clients;

/// <summary>
/// Refit client for <c>NotesController</c> - a site admin's own annotations on Organizations and
/// people.
/// </summary>
/// <remarks>
/// Gated by <c>Platform.ManageOrganizations</c> at the Api, the same permission that already gates
/// the Organizations console and the user directory this is used from.
/// </remarks>
public interface INoteApiClient
{
    /// <summary>Notes about an Organization, a person, or both.</summary>
    [Get("/")]
    Task<List<NoteDto>> ListAsync(Guid? tenantId, Guid? userId);

    [Post("/")]
    Task<NoteDto> CreateAsync([Body] SaveNoteRequestDto request);

    [Put("/{id}")]
    Task<NoteDto> UpdateAsync(Guid id, [Body] SaveNoteRequestDto request);

    [Delete("/{id}")]
    Task DeleteAsync(Guid id);
}
