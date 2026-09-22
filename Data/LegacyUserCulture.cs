// Copyright ©2026 Scott Blomfield

using System.ComponentModel.DataAnnotations;

namespace RustArchon.Panel.Data;

/// <summary>
/// A person's language as the identity database held it, waiting to be handed to the Api. The language used to be a column on the identity user; it now
/// lives in the Api's <c>UserProfile</c>. The migration that removed the column first copied every language here, so nobody who chose one before the move
/// loses it, and <c>UserProfileBackfillService</c> hands these over and deletes each row once the Api has it. When the table is empty, on every instance,
/// it and its service can be removed.
/// </summary>
public class LegacyUserCulture
{
    /// <summary>The person (the identity user's id).</summary>
    [Key]
    public Guid UserId { get; set; }

    /// <summary>The culture name as it was stored, unvalidated - the Api decides whether it is one it will keep.</summary>
    public string Culture { get; set; } = string.Empty;
}
