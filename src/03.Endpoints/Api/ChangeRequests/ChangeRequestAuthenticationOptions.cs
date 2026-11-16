namespace ChangeFlow.Endpoints.Api.ChangeRequests;

internal sealed class ChangeRequestAuthenticationOptions
{
    public const string SectionName = "ChangeRequestsAuthentication";

    public string Authority { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
}
