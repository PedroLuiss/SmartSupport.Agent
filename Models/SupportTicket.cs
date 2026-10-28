using System.ComponentModel.DataAnnotations;

namespace SmartSupport.Agent.Models;

public sealed class IncomingTicketDto
{
    [Required]
    [StringLength(20_000, MinimumLength = 1)]
    public string RawContent { get; set; } = string.Empty;
}

public sealed class AutomatedTicketResponse
{
    public string ClientName { get; set; } = string.Empty;
    public string IssueCategory { get; set; } = string.Empty;
    public string UrgencyLevel { get; set; } = string.Empty;
    public string Sentiment { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public Dictionary<string, string> ExtractedData { get; set; } = [];
    public string SuggestedAutoReply { get; set; } = string.Empty;
    public AccountingEntryDraft? SuggestedAccountingEntry { get; set; }
}

public sealed class AccountingEntryDraft
{
    public string Description { get; set; } = string.Empty;
    public string DebitAccount { get; set; } = string.Empty;
    public string CreditAccount { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
}