namespace QuantumBuild.Core.Application.Models;

public enum FailureCode
{
    DuplicateEmail,
    WorkflowInvitationNotFound,
    WorkflowTokenInvalid,
    WorkflowTokenAlreadyUsed,
    WorkflowTokenExpired,
    WorkflowInvalidState,
    WorkflowConfirmationRequired,
    WorkflowReasonRequired,
    TitleNotUnique
}
