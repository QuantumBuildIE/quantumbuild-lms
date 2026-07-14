using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.UpdateLastEditedStep;

public class UpdateLastEditedStepCommandHandler : IRequestHandler<UpdateLastEditedStepCommand>
{
    private readonly IToolboxTalksDbContext _context;

    public UpdateLastEditedStepCommandHandler(IToolboxTalksDbContext context)
    {
        _context = context;
    }

    public async Task Handle(UpdateLastEditedStepCommand request, CancellationToken cancellationToken)
    {
        var talk = await _context.ToolboxTalks
            .FirstOrDefaultAsync(t => t.Id == request.TalkId && t.TenantId == request.TenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Learning {request.TalkId} not found.");

        talk.LastEditedStep = request.Step;
        await _context.SaveChangesAsync(cancellationToken);
    }
}
