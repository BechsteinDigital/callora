using Callora.Core.Application.Policies;
using Callora.Core.Application.Security;
using Callora.Core.Infrastructure.Security;
using Xunit;

namespace Callora.Core.Tests.Infrastructure.Security;

/// <summary>
/// The in-memory RBAC store rejects caller-facing faults with a typed
/// <see cref="BackendRbacException"/> carrying a stable code and HTTP status,
/// identical to the EF-backed store (R4). Covers the three rejection paths.
/// </summary>
public sealed class InMemoryBackendRbacStoreTests
{
    private static InMemoryBackendRbacStore NewStore() =>
        new(new BackendHostOptions());

    [Fact]
    public async Task UpsertRole_ForFixedSuperAdmin_ThrowsRoleFixedConflict()
    {
        var store = NewStore();

        var ex = await Assert.ThrowsAsync<BackendRbacException>(() =>
            store.UpsertRoleAsync(BackendRoles.SuperAdmin, ["contact.read"]));

        Assert.Equal(BackendRbacException.RoleFixedCode, ex.ErrorCode);
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task UpsertUserRole_ForUndefinedRole_ThrowsRoleNotFound()
    {
        var store = NewStore();

        var ex = await Assert.ThrowsAsync<BackendRbacException>(() =>
            store.UpsertUserRoleAsync("user-1", "ghost-role"));

        Assert.Equal(BackendRbacException.RoleNotFoundCode, ex.ErrorCode);
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task UpsertRole_WithInvalidPermissionKey_ThrowsPermissionInvalid()
    {
        var store = NewStore();

        var ex = await Assert.ThrowsAsync<BackendRbacException>(() =>
            store.UpsertRoleAsync("editor", ["not-a-valid-key"]));

        Assert.Equal(BackendRbacException.PermissionInvalidCode, ex.ErrorCode);
        Assert.Equal(400, ex.StatusCode);
    }
}
