using System.Text.Json;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities.Workflows;

namespace QuantumBuild.Tests.Unit.ToolboxTalks.Workflows;

/// <summary>
/// EditableSectionIndicesJson follows the same codebase convention as
/// ContentCreationSession.ValidationRunIds/TranslationJobIds: a plain nullable JSON string
/// column with serialize/deserialize done by the caller, not an entity-level wrapper property
/// (no such wrapper pattern exists anywhere else in this codebase's entities).
/// </summary>
public class ExternalParticipantInvitationTests
{
    [Fact]
    public void EditableSectionIndicesJson_SetIndicesThenReadBack_RoundTripsExactList()
    {
        var invitation = new ExternalParticipantInvitation();
        var indices = new List<int> { 1, 3, 5 };

        invitation.EditableSectionIndicesJson = JsonSerializer.Serialize(indices);

        var roundTripped = JsonSerializer.Deserialize<List<int>>(invitation.EditableSectionIndicesJson);
        roundTripped.Should().Equal(1, 3, 5);
    }

    [Fact]
    public void EditableSectionIndicesJson_Default_IsNull()
    {
        var invitation = new ExternalParticipantInvitation();

        invitation.EditableSectionIndicesJson.Should().BeNull();
    }

    [Fact]
    public void EditableSectionIndicesJson_ExplicitlySetToNull_StaysNull()
    {
        var invitation = new ExternalParticipantInvitation
        {
            EditableSectionIndicesJson = JsonSerializer.Serialize(new List<int> { 0, 1 })
        };

        invitation.EditableSectionIndicesJson = null;

        invitation.EditableSectionIndicesJson.Should().BeNull();
    }
}
