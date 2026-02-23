using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.LessonParser.Domain.Entities;

namespace QuantumBuild.Modules.LessonParser.Application.Common.Interfaces;

/// <summary>
/// Database context interface for the Lesson Parser module
/// </summary>
public interface ILessonParserDbContext
{
    DbSet<ParseJob> ParseJobs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
