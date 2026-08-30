// Copyright ©2026 Scott Blomfield

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace RustArchon.Panel.Data;

/// <summary>
/// Database context for the Blazor project. Deliberately a plain <see cref="IdentityDbContext{TUser, TRole, TKey}"/>,
/// not a <c>JumpStartDbContext</c> - this project has no JumpStart entities of its own. Everything
/// else (RustServer, Tenant, Role, ...) is reached only through API clients calling RustArchon.Api -
/// see the README and JumpStart's samples documentation for why.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
}
