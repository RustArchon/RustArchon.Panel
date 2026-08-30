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
}
