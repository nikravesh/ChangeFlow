using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ChangeFlow.Core.Domain.ChangeRequests;
using ChangeFlow.Core.Domain.Users;
using ChangeFlow.Infra.DataAccess.Common;
using ChangeFlow.Tests.ChangeRequests.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ChangeFlow.Tests.ChangeRequests.Contract.ReviewWorkflowContractTests;

namespace ChangeFlow.Tests.ChangeRequests.Integration;

public sealed class SyntheticCohortTests
{
    [Fact]
    public async Task Twenty_requesters_and_twenty_reviewers_complete_and_clean_up_the_workflow()
    {
        var fixture = new ChangeRequestApiFixture();
        string databasePath;
        await using (var context = await fixture.Factory.Services
            .GetRequiredService<IDbContextFactory<ChangeFlowDbContext>>().CreateDbContextAsync())
        {
            databasePath = new SqliteConnectionStringBuilder(context.Database.GetConnectionString()).DataSource;
        }

        Assert.True(File.Exists(databasePath));
        var accounts = new HashSet<UserId>();
        for (var index = 1; index <= 20; index++)
        {
            var requester = await fixture.ProvisionAsync($"cohort.requester{index:D2}", UserRole.Requester);
            var reviewer = await fixture.ProvisionAsync($"cohort.reviewer{index:D2}", UserRole.Reviewer);
            Assert.True(accounts.Add(requester));
            Assert.True(accounts.Add(reviewer));
            using var owner = fixture.CreateAuthenticatedClient(requester);
            using var reviewerClient = fixture.CreateAuthenticatedClient(reviewer);
            var path = await CreatePendingAsync(owner);
            var reviewable = await reviewerClient.GetFromJsonAsync<JsonElement>(path);
            Assert.Equal("PendingReview", reviewable.GetProperty("status").GetString());
            using var returned = await reviewerClient.PostAsJsonAsync($"{path}/request-changes",
                new { expectedVersion = "2", feedback = "Clarify the requested change" });
            Assert.Equal(HttpStatusCode.OK, returned.StatusCode);
            var pendingCorrection = await owner.GetFromJsonAsync<JsonElement>(path);
            Assert.Equal("Clarify the requested change", pendingCorrection.GetProperty("currentFeedback").GetString());
            using var edit = await owner.PutAsJsonAsync(path,
                new { title = "Revised", description = "Clarified change", reason = "Need", expectedVersion = "3" });
            Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
            using var submit = await owner.PostAsJsonAsync($"{path}/submit", new { expectedVersion = "4" });
            Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
            using var approve = await reviewerClient.PostAsJsonAsync($"{path}/approve", new { expectedVersion = "5" });
            Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
            var history = await owner.GetFromJsonAsync<JsonElement>($"{path}/review-history");
            Assert.Equal(new[] { "RequestChanges", "Approve" },
                history.EnumerateArray().Select(decision => decision.GetProperty("action").GetString()));
        }

        await using (var context = await fixture.Factory.Services
            .GetRequiredService<IDbContextFactory<ChangeFlowDbContext>>().CreateDbContextAsync())
        {
            Assert.Equal(20, await context.Set<ChangeRequest>()
                .CountAsync(request => request.Status == ChangeRequestStatus.Approved && request.Version == 6));
            Assert.Equal(40, await context.Set<ReviewDecision>().CountAsync());
        }

        Assert.Equal(40, accounts.Count);
        await fixture.DisposeAsync();
        Assert.False(File.Exists(databasePath));
    }
}
