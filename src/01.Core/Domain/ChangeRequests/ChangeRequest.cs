using ChangeFlow.Core.Domain.Users;

namespace ChangeFlow.Core.Domain.ChangeRequests;

public sealed class ChangeRequest
{
    private ChangeRequest() { }

    public Guid Id { get; private set; }
    private Guid RequesterKey { get; set; }
    public UserId RequesterId => new(RequesterKey);
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public ChangeRequestStatus Status { get; private set; }
    public long Version { get; private set; }

    public static ChangeRequest Create(UserId requester, DraftContent content)
    {
        if (requester.Value == Guid.Empty)
        {
            throw new ArgumentException("A requester identity is required.", nameof(requester));
        }

        var request = new ChangeRequest
        {
            Id = Guid.NewGuid(),
            RequesterKey = requester.Value,
            Status = ChangeRequestStatus.Draft,
            Version = 1
        };
        request.ReplaceContent(content);
        return request;
    }

    public bool Edit(DraftContent content)
    {
        if (!CanRevise())
        {
            return false;
        }

        ReplaceContent(content);
        Version++;
        return true;
    }

    public bool Submit()
    {
        if (!CanRevise())
        {
            return false;
        }

        Status = ChangeRequestStatus.PendingReview;
        Version++;
        return true;
    }

    public ReviewDecision? Approve(UserId reviewer, DateTimeOffset decidedAt) =>
        CompleteReview(reviewer, decidedAt, ReviewAction.Approve, null, ChangeRequestStatus.Approved);

    public ReviewDecision? RequestChanges(UserId reviewer, string feedback, DateTimeOffset decidedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(feedback);
        return CompleteReview(reviewer, decidedAt, ReviewAction.RequestChanges, feedback.Trim(),
            ChangeRequestStatus.ChangesRequested);
    }

    private ReviewDecision? CompleteReview(UserId reviewer, DateTimeOffset decidedAt,
        ReviewAction action, string? feedback, ChangeRequestStatus nextStatus)
    {
        if (Status != ChangeRequestStatus.PendingReview || Version == long.MaxValue)
        {
            return null;
        }

        var decision = new ReviewDecision(Id, Version + 1, reviewer, decidedAt, action, feedback);
        Status = nextStatus;
        Version = decision.ResultingVersion;
        return decision;
    }

    private void ReplaceContent(DraftContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        Title = content.Title;
        Description = content.Description;
        Reason = content.Reason;
    }

    private bool CanRevise() =>
        (Status is ChangeRequestStatus.Draft or ChangeRequestStatus.ChangesRequested)
        && Version < long.MaxValue;
}

public sealed class DraftContent
{
    public DraftContent(string title, string description, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Title = title.Trim();
        Description = description.Trim();
        Reason = reason.Trim();
    }

    public string Title { get; }
    public string Description { get; }
    public string Reason { get; }
}
