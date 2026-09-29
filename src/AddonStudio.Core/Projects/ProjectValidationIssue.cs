namespace AddonStudio.Core.Projects;

public enum ProjectValidationSeverity
{
    Warning,
    Error
}

public sealed record ProjectValidationIssue(
    ProjectValidationSeverity Severity,
    string Code,
    string Message);
