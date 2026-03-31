using MediatR;
using QuantumBuild.Core.Application.Models;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands;

public record ResetTenantDataCommand(Guid TenantId) : IRequest<Result>;
