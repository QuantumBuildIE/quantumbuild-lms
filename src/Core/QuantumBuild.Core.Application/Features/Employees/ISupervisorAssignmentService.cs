using QuantumBuild.Core.Application.Features.Employees.DTOs;
using QuantumBuild.Core.Application.Models;

namespace QuantumBuild.Core.Application.Features.Employees;

public interface ISupervisorAssignmentService
{
    Task<Result<List<SupervisorOperatorDto>>> GetAssignedOperatorsAsync(Guid supervisorEmployeeId);
    Task<Result<List<SupervisorOperatorDto>>> GetAvailableOperatorsAsync(Guid supervisorEmployeeId);
    Task<Result<List<SupervisorAssignmentDto>>> AssignOperatorsAsync(Guid supervisorEmployeeId, AssignOperatorsDto dto);
    Task<Result> UnassignOperatorAsync(Guid supervisorEmployeeId, Guid operatorEmployeeId);
    Task<Result<List<Guid>>> GetAssignedOperatorIdsAsync(Guid supervisorEmployeeId);
}
