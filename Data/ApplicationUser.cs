// Copyright ©2026 Scott Blomfield

using JumpStart.Data;
using Microsoft.AspNetCore.Identity;

namespace RustArchon.Panel.Data;

/// <summary>
/// Application user entity with a Guid identifier, satisfying both ASP.NET Core Identity and
/// JumpStart's <see cref="IUser"/> interface.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>, IUser
{
    // IUser.Id is satisfied by IdentityUser<Guid>.Id.

    /// <summary>
    /// The culture (e.g. <c>"en-US"</c>) this user last browsed the Panel in - captured at sign-up and
    /// refreshed every time they use the language switcher (see <c>Program.cs</c>'s <c>/Culture/Set</c>
    /// endpoint), so an email sent about their account can be worded in the language they actually read
    /// rather than assuming English. Passed as-is to RustArchon.Api's
    /// <c>SendTemplatedEmailRequestDto.Culture</c> by <c>QueuedEmailSender</c> - never validated here
    /// against the set of cultures actually compiled in, since a language later removed from
    /// <c>Resources/</c> should degrade to Api's own fallback chain, not throw.
    /// </summary>
    public string? PreferredCulture { get; set; }
}
