// Copyright ©2026 Scott Blomfield

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RustArchon.Panel.Clients;
using RustArchon.Panel.Data;
using RustArchon.Shared.DTOs;

namespace RustArchon.Panel.Services;

/// <summary>Tells the Api what a person's settings are, so the Api - which cannot see the identity database - knows them.</summary>
public interface IUserProfileWriter
{
    /// <summary>
    /// Records the person's language with the Api. Best effort, and never throws: it is called from sign-up and from the language switcher, and neither may
    /// fail because the Api could not be reached. A miss is corrected the next time the person switches language (and by the backfill).
    /// </summary>
    Task SetPreferredCultureAsync(Guid userId, string? culture);
}

/// <inheritdoc />
public class UserProfileWriter(IInternalUserProfileApiClient client, ILogger<UserProfileWriter> logger) : IUserProfileWriter
{
    public async Task SetPreferredCultureAsync(Guid userId, string? culture)
    {
        try
        {
            await client.SetAsync(userId, new UpdateUserProfileDto { PreferredCulture = culture });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not record user {UserId}'s language with the Api; it will be corrected the next time they switch.", userId);
        }
    }
}

/// <summary>
/// Hands the Api the languages the identity database held before they moved (see <see cref="LegacyUserCulture"/>), once the Panel is up, so people who chose
/// one before profiles existed have one there. Each row is deleted once the Api has it, so a pass that fails part way resumes where it stopped, and once
/// every instance has emptied its table this service goes away. The Api only fills in people with no profile and never overwrites, so what a person has
/// chosen since always stands.
/// </summary>
public class UserProfileBackfillService(IServiceScopeFactory scopeFactory, ILogger<UserProfileBackfillService> logger) : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(30);
    private const int BatchSize = 500;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(InitialDelay, stoppingToken);
            await RunOnceAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Handing user languages to the Api did not finish; the next start will try again.");
        }
    }

    /// <summary>One pass over every language still waiting. Returns how many profiles the Api created.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var client = scope.ServiceProvider.GetRequiredService<IInternalUserProfileApiClient>();

        var created = 0;
        while (true)
        {
            var batch = await db.LegacyUserCultures.OrderBy(c => c.UserId).Take(BatchSize).ToListAsync(cancellationToken);
            if (batch.Count == 0)
            {
                return created;
            }

            created += await client.BackfillAsync(new BackfillUserProfilesDto
            {
                Items = batch.Select(c => new BackfillUserProfileItemDto { UserId = c.UserId, PreferredCulture = c.Culture }).ToList()
            });

            // Only now that the Api has them: a failure above leaves these rows for the next start.
            db.LegacyUserCultures.RemoveRange(batch);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
