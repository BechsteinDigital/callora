using Callora.Core.Application.Security;
using Xunit;

namespace Callora.Core.Tests.Application.Security;

/// <summary>
/// Locks the caller-facing contract of <see cref="BackendUserException"/> (R4).
/// The production throw lives behind a database round-trip in the EF-backed
/// store (Docker-gated integration territory), so the stable code and HTTP
/// status the API layer depends on are asserted directly on the factory.
/// </summary>
public sealed class BackendUserExceptionTests
{
    [Fact]
    public void PasswordRequired_IsCalloraExceptionWithCodeAndStatus()
    {
        var ex = BackendUserException.PasswordRequired();

        Assert.IsAssignableFrom<CalloraException>(ex);
        Assert.Equal(BackendUserException.PasswordRequiredCode, ex.ErrorCode);
        Assert.Equal(400, ex.StatusCode);
    }
}
