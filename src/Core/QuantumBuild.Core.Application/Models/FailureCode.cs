namespace QuantumBuild.Core.Application.Models;

public enum FailureCode
{
    DuplicateEmail,
    WorkflowTokenInvalid,
    WorkflowTokenAlreadyUsed,
    WorkflowTokenExpired,
    WorkflowInvalidState,
    WorkflowConfirmationRequired
}
