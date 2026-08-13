namespace Asnan.Application.Common;

/// <summary>
/// The authenticated caller, for the "owning doctor or admin" authorization
/// pattern (ARCHITECTURE.md §2.2's object-level authorization) — resolved
/// from claims at the controller boundary so services stay framework-agnostic.
/// </summary>
public record CallerContext(Guid UserId, bool IsAdmin);
