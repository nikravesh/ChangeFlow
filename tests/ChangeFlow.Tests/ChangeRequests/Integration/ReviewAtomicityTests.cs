using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChangeFlow.Core.Domain.ChangeRequests;
using ChangeFlow.Infra.DataAccess.Common;
using ChangeFlow.Tests.ChangeRequests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static ChangeFlow.Tests.ChangeRequests.Contract.ReviewWorkflowContractTests;

namespace ChangeFlow.Tests.ChangeRequests.Integration;

public sealed class ReviewAtomicityTests(ChangeRequestApiFixture fixture)
    : IClassFixture<ChangeRequestApiFixture>
{
    [Fact]
    public async Task Two_reviewers_on_one_version_persist_one_decision()
    {
        using var owner = fixture.CreateAuthenticatedClient(fixture.Requester);
        using var first = fixture.CreateAuthenticatedClient(fixture.Reviewer);
        using var second = fixture.CreateAuthenticatedClient(fixture.SecondReviewer);
        var path = await CreatePendingAsync(owner);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var approve = DecideAsync(first, "approve");
        var requestChanges = DecideAsync(second, "request-changes");
        start.SetResult();
        await Task.WhenAll(approve, requestChanges);
        using var firstResponse = await approve;
        using var secondResponse = await requestChanges;
        var approved = firstResponse.StatusCode == HttpStatusCode.OK;
        Assert.Equal(HttpStatusCode.OK, (approved ? firstResponse : secondResponse).StatusCode);
        await AssertProblemAsync(approved ? secondResponse : firstResponse, HttpStatusCode.Conflict, "version_conflict");

        var (request, history) = await ReadStoredAsync(path);
        Assert.Equal(3L, request.Version);
        Assert.Equal(approved ? ChangeRequestStatus.Approved : ChangeRequestStatus.ChangesRequested, request.Status);
        var decision = Assert.Single(history);
        Assert.Equal(approved ? fixture.Reviewer : fixture.SecondReviewer, decision.ReviewerId);
        Assert.Equal(3L, decision.ResultingVersion);

        async Task<HttpResponseMessage> DecideAsync(HttpClient client, string command)
        {
            await start.Task;
            return await client.PostAsJsonAsync($"{path}/{command}", new { expectedVersion = "2", feedback = "Revise" });
        }
    }

    [Fact]
    public async Task Failure_to_insert_a_decision_rolls_back_the_request()
    {
        using var owner = fixture.CreateAuthenticatedClient(fixture.Requester);
        using var reviewer = fixture.CreateAuthenticatedClient(fixture.Reviewer);
        var path = await CreatePendingAsync(owner);
        await using var context = await CreateContextAsync();
        await context.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER fail_test_decision_insert BEFORE INSERT ON review_decisions
            BEGIN SELECT RAISE(ABORT, 'Test decision insert failure'); END;
            """);
        try
        {
            await AssertProblemAsync(await reviewer.PostAsJsonAsync($"{path}/approve", new { expectedVersion = "2" }),
                HttpStatusCode.InternalServerError, "unexpected_failure");
            var (request, history) = await ReadStoredAsync(path);
            Assert.Equal((ChangeRequestStatus.PendingReview, 2L), (request.Status, request.Version));
            Assert.Empty(history);
        }
        finally
        {
            await context.Database.ExecuteSqlRawAsync("DROP TRIGGER fail_test_decision_insert");
        }
    }

    [Fact]
    public async Task Revocation_committed_before_review_is_observed_with_the_same_token()
    {
        using var owner = fixture.CreateAuthenticatedClient(fixture.Requester);
        using var reviewer = fixture.CreateAuthenticatedClient(fixture.Reviewer);
        var path = await CreatePendingAsync(owner);
        await using var context = await CreateContextAsync();
        var reviewerId = fixture.Reviewer.Value;
        try
        {
            await using var revoke = await context.Database.BeginTransactionAsync();
            Assert.Equal(1, await context.Database.ExecuteSqlAsync(
                $"DELETE FROM user_roles WHERE user_id = {reviewerId} AND role = 'Reviewer'"));
            var review = Task.Run(() => reviewer.PostAsJsonAsync($"{path}/approve", new { expectedVersion = "2" }));
            await revoke.CommitAsync();
            await AssertProblemAsync(await review, HttpStatusCode.Forbidden, "forbidden");
            var (request, history) = await ReadStoredAsync(path);
            Assert.Equal((ChangeRequestStatus.PendingReview, 2L), (request.Status, request.Version));
            Assert.Empty(history);
        }
        finally
        {
            await RestoreReviewerAsync(context);
        }
    }

    [Fact]
    public async Task Review_that_holds_the_write_transaction_commits_before_revocation()
    {
        using var owner = fixture.CreateAuthenticatedClient(fixture.Requester);
        var path = await CreatePendingAsync(owner);
        var clock = new PausingClock(fixture.Clock.GetUtcNow());
        await using var factory = fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(clock);
            }));
        using var reviewer = factory.CreateClient();
        reviewer.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", fixture.CreateAccessToken(fixture.Reviewer));
        await using var context = await CreateContextAsync();
        var reviewerId = fixture.Reviewer.Value;
        var review = Task.Run(() => reviewer.PostAsJsonAsync($"{path}/approve", new { expectedVersion = "2" }));
        try
        {
            await clock.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var revocationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var revocation = Task.Run(async () =>
            {
                revocationStarted.SetResult();
                return await context.Database.ExecuteSqlAsync(
                    $"DELETE FROM user_roles WHERE user_id = {reviewerId} AND role = 'Reviewer'");
            });
            await revocationStarted.Task;
            clock.Resume.TrySetResult();
            using var response = await review;
            Assert.Equal(1, await revocation);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await AssertProblemAsync(await reviewer.GetAsync($"{path}/review-history"),
                HttpStatusCode.Forbidden, "forbidden");
            var (request, history) = await ReadStoredAsync(path);
            Assert.Equal((ChangeRequestStatus.Approved, 3L), (request.Status, request.Version));
            Assert.Equal(fixture.Reviewer, Assert.Single(history).ReviewerId);
        }
        finally
        {
            clock.Resume.TrySetResult();
            await RestoreReviewerAsync(context);
        }
    }

    private Task<ChangeFlowDbContext> CreateContextAsync() =>
        fixture.Factory.Services.GetRequiredService<IDbContextFactory<ChangeFlowDbContext>>().CreateDbContextAsync();

    private Task<int> RestoreReviewerAsync(ChangeFlowDbContext context) =>
        context.Database.ExecuteSqlAsync(
            $"INSERT OR IGNORE INTO user_roles (user_id, role) VALUES ({fixture.Reviewer.Value}, 'Reviewer')");

    private async Task<(ChangeRequest Request, ReviewDecision[] History)> ReadStoredAsync(string path)
    {
        var id = Guid.Parse(path[(path.LastIndexOf('/') + 1)..]);
        await using var context = await CreateContextAsync();
        var request = await context.Set<ChangeRequest>().SingleAsync(request => request.Id == id);
        var history = await context.Set<ReviewDecision>().Where(decision => decision.ChangeRequestId == id).ToArrayAsync();
        return (request, history);
    }

    private sealed class PausingClock(DateTimeOffset now) : TimeProvider
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override DateTimeOffset GetUtcNow()
        {
            Entered.TrySetResult();
            Resume.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
            return now;
        }
    }
}
