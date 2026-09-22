// Copyright ©2026 Scott Blomfield

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using RustArchon.Panel.Data;

namespace RustArchon.Panel.Services;

/// <summary>
/// Turns the user ids the Api speaks in into something a person can read. The Api has no users table - names and email addresses
/// live in the Panel's Identity store - so the Panel is the only place that can do this join.
/// </summary>
public interface IUserDisplayNames
{
    /// <summary>
    /// A readable name for each of <paramref name="userIds"/>: the account's email, else its user name, else the start of the id
    /// (an account since deleted). Every id asked about is in the answer.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(IEnumerable<Guid> userIds);
}

/// <inheritdoc />
public class UserDisplayNames(UserManager<ApplicationUser> userManager) : IUserDisplayNames
{
    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(IEnumerable<Guid> userIds)
    {
        var names = new Dictionary<Guid, string>();
        foreach (var id in userIds.Distinct())
        {
            var account = await userManager.FindByIdAsync(id.ToString());
            names[id] = account?.Email ?? account?.UserName ?? id.ToString()[..8];
        }

        return names;
    }
}
