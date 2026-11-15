using ChangeFlow.Core.Domain.Users;

namespace ChangeFlow.Tests.Users.Domain;

public sealed class UserRejectionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_display_name_is_rejected(string? displayName)
    {
        Assert.ThrowsAny<ArgumentException>(() => User.Create(
            displayName!,
            "alice",
            [UserRole.Requester]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_login_identifier_is_rejected(string? loginIdentifier)
    {
        Assert.ThrowsAny<ArgumentException>(() => User.Create(
            "Alice",
            loginIdentifier!,
            [UserRole.Requester]));
    }

    [Fact]
    public void Missing_roles_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => User.Create("Alice", "alice", []));
    }

    [Fact]
    public void Unknown_role_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => User.Create(
            "Alice",
            "alice",
            [(UserRole)999]));
    }
}
