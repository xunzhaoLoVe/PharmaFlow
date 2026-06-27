namespace PharmaFlow.Application.Models;

public class SmartCleanResult
{
    public string? CleanedDataJson { get; set; }
    public string? TargetEntityType { get; set; }
    public string? ErrorMessage { get; set; }
    public List<ValidationIssue> ValidationIssues { get; set; } = new();
    public List<MatchResult> MatchResults { get; set; } = new();
    public List<AnomalyInfo> Anomalies { get; set; } = new();
    public bool IsDuplicate { get; set; }

    public bool HasIssues => ValidationIssues.Count > 0
                          || MatchResults.Any(m => m.Confidence < 1.0)
                          || Anomalies.Count > 0
                          || IsDuplicate;
}

public class ValidationIssue
{
    public string Field { get; set; } = "";
    public string Issue { get; set; } = "";
    public string Severity { get; set; } = "error";
}

public class MatchResult
{
    public string EntityType { get; set; } = "";
    public string SourceValue { get; set; } = "";
    public Guid? MatchedId { get; set; }
    public string? MatchedName { get; set; }
    public double Confidence { get; set; }
    public string MatchType { get; set; } = "";
}

public class AnomalyInfo
{
    public string Field { get; set; } = "";
    public string Reason { get; set; } = "";
}