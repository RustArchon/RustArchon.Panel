// Copyright ©2026 Scott Blomfield

using System.Threading;
using System.Threading.Tasks;
using JumpStart.Services.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace RustArchon.Panel.Services;

/// <summary>
/// Copies the JWT <see cref="JwtExchangeHandler"/> just resolved into the real Blazor circuit's own
/// <see cref="ITokenStore"/>, so code outside any API client's HTTP pipeline - <see cref="RconHubClient"/>
/// in particular - can read the same token.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why this exists:</strong> <see cref="IHttpClientFactory"/> builds each client's
/// <c>DelegatingHandler</c> chain in its own DI scope, separate from the circuit's - see
/// <see cref="CircuitServicesAccessor"/>'s remarks. That means the <see cref="ITokenStore"/>
/// <see cref="JwtExchangeHandler"/> populates (constructor-injected, resolved from that separate
/// scope) is a <em>different object</em> from the one an ordinarily-injected service in the same
/// circuit (e.g. <c>@inject ITokenStore</c>, or a constructor-injected one in a plain scoped service
/// like <see cref="RconHubClient"/>) receives - confirmed by hand while building the live-console
/// feature: a token demonstrably reached the API successfully on every call, yet
/// <c>RconHubClient</c>'s own <see cref="ITokenStore"/> reference stayed permanently <c>null</c>.
/// </para>
/// <para>
/// Registered as the innermost handler on <see cref="Clients.IRustServerApiClient"/>'s pipeline (last
/// in the chain, right after <see cref="JwtAuthenticationHandler"/> - see <c>Program.cs</c>) so it
/// runs after a real token is guaranteed to exist, and bridges it into the actual circuit using the
/// exact same <see cref="CircuitServicesAccessor"/> mechanism <see cref="JwtExchangeHandler"/> itself
/// uses to reach back into the circuit for its own dependencies.
/// </para>
/// </remarks>
public class TokenBridgeHandler(CircuitServicesAccessor circuitServicesAccessor, ITokenStore pipelineTokenStore)
    : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        var token = pipelineTokenStore.GetToken();
        if (token is not null)
        {
            var circuitTokenStore = circuitServicesAccessor.Services?.GetService<ITokenStore>();
            circuitTokenStore?.SetToken(token);
        }

        return response;
    }
}
