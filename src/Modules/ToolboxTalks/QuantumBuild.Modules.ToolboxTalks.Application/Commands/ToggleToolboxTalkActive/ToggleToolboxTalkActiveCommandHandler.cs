using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.ToggleToolboxTalkActive;

public class ToggleToolboxTalkActiveCommandHandler : IRequestHandler<ToggleToolboxTalkActiveCommand, bool>
{
    private readonly IToolboxTalksDbContext _context;

    public ToggleToolboxTalkActiveCommandHandler(IToolboxTalksDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(ToggleToolboxTalkActiveCommand request, CancellationToken cancellationToken)
    {
        var talk = await _context.ToolboxTalks
            .FirstOrDefaultAsync(t => t.Id == request.TalkId && t.TenantId == request.TenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Learning {request.TalkId} not found.");

        talk.IsActive = request.Active;
        await _context.SaveChangesAsync(cancellationToken);

        return talk.IsActive;
    }
}
