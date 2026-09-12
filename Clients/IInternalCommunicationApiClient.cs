// Copyright ©2026 Scott Blomfield

using System;
using System.Threading.Tasks;
using Refit;

namespace RustArchon.Panel.Clients;

/// <summary>
/// Refit client for the one internal (shared-secret, non-JWT) endpoint the tracking-pixel route calls
/// - same channel and reasoning as <see cref="IInternalEmailApiClient"/>. Reachable from an anonymous
/// GET with nobody signed in, so it can never authenticate any other way.
/// </summary>
/// <remarks>
/// Not called directly from any page or component - only from the <c>/track/email/{id}.gif</c> minimal
/// API endpoint in <c>Program.cs</c>. RustArchon.Api is never reachable from outside the Docker
/// network (see its own <c>InternalController</c> remarks), so a tracking pixel a recipient's mail
/// client can actually load has to be served from somewhere public - this Panel - which then makes
/// this one internal call to record the view.
/// </remarks>
public interface IInternalCommunicationApiClient
{
    [Post("/internal/communications/{id}/viewed")]
    Task MarkViewedAsync(Guid id);
}
