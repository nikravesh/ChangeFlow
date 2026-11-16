using ChangeFlow.Core.Domain.Users;

namespace ChangeFlow.Core.Domain.ChangeRequests;

public enum ReviewAction
{
    Approve,
    RequestChanges
}

public sealed class ReviewDecision
{
    private ReviewDecision() { }

    internal ReviewDecision(Guid requestId, long resultingVersion, UserId reviewer,
        DateTimeOffset decidedAt, ReviewAction action, string? feedback)
    {
        if (reviewer.Value == Guid.Empty)
        {
            throw new ArgumentException("A reviewer identity is required.", nameof(reviewer));
        }

        ChangeRequestId = requestId;
        ResultingVersion = resultingVersion;
        ReviewerKey = reviewer.Value;
        DecidedAtUtc = decidedAt.ToUniversalTime();
        Action = action;
        Feedback = feedback;
    }

    public Guid ChangeRequestId { get; private set; }
    public long ResultingVersion { get; private set; }
    private Guid ReviewerKey { get; set; }
    public UserId ReviewerId => new(ReviewerKey);
    public DateTimeOffset DecidedAtUtc { get; private set; }
    public ReviewAction Action { get; private set; }
    public string? Feedback { get; private set; }
}
