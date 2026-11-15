using ChangeFlow.Core.Domain.Users;

namespace ChangeFlow.Tests.Users.Domain;

public sealed class UserCreationTests
{
    [Fact]
    public void Login_identifier_is_trimmed_and_normalized_invariantly()
    {
        var identifier = LoginIdentifier.Create("  Alice.User  ");

        Assert.Equal("Alice.User", identifier.Original);
        Assert.Equal("ALICE.USER", identifier.Normalized);
    }

    [Fact]
    public void User_creation_trims_name_assigns_stable_id_and_deduplicates_roles()
    {
        var user = User.Create(
            "  Alice  ",
            " alice ",
            [UserRole.Requester, UserRole.Requester, UserRole.Reviewer]);

        Assert.NotEqual(Guid.Empty, user.Id.Value);
        Assert.Equal("Alice", user.DisplayName);
        Assert.Equal("alice", user.LoginIdentifier.Original);
        Assert.Equal([UserRole.Requester, UserRole.Reviewer], user.Roles.Order());
    }

    [Fact]
    public void Role_contract_contains_only_the_two_approved_values()
    {
        Assert.Equal(["Requester", "Reviewer"], Enum.GetNames<UserRole>());
    }
}
