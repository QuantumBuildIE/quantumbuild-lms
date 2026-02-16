namespace QuantumBuild.Tests.Unit.Builders;

/// <summary>
/// Tests for the ToolboxTalkBuilder to ensure it creates valid entities.
/// </summary>
public class ToolboxTalkBuilderTests
{
    [Fact]
    public void Build_WithDefaults_CreatesValidTalk()
    {
        // Arrange & Act
        var talk = new ToolboxTalkBuilder().Build();

        // Assert
        talk.Id.Should().NotBeEmpty();
        talk.TenantId.Should().Be(TestTenantConstants.TenantId);
        talk.Title.Should().NotBeNullOrEmpty();
        talk.IsActive.Should().BeTrue();
        talk.RequiresQuiz.Should().BeFalse();
    }

    [Fact]
    public void Build_WithTitle_SetsTitle()
    {
        // Arrange
        const string expectedTitle = "Safety Training Talk";

        // Act
        var talk = new ToolboxTalkBuilder()
            .WithTitle(expectedTitle)
            .Build();

        // Assert
        talk.Title.Should().Be(expectedTitle);
    }

    [Fact]
    public void Build_WithQuiz_EnablesQuizAndSetPassingScore()
    {
        // Arrange
        const int expectedPassingScore = 85;

        // Act
        var talk = new ToolboxTalkBuilder()
            .WithQuiz(expectedPassingScore)
            .Build();

        // Assert
        talk.RequiresQuiz.Should().BeTrue();
        talk.PassingScore.Should().Be(expectedPassingScore);
    }

    [Fact]
    public void Build_WithSections_AddsSectionsToTalk()
    {
        // Arrange & Act
        var talk = new ToolboxTalkBuilder()
            .WithSection("Introduction", "<p>Intro content</p>")
            .WithSection("Main Content", "<p>Main content</p>")
            .Build();

        // Assert
        talk.Sections.Should().HaveCount(2);
        talk.Sections.First().Title.Should().Be("Introduction");
        talk.Sections.First().SectionNumber.Should().Be(1);
        talk.Sections.Last().SectionNumber.Should().Be(2);
    }

    [Fact]
    public void Build_WithQuestions_AddsQuestionsToTalk()
    {
        // Arrange & Act
        var talk = new ToolboxTalkBuilder()
            .WithQuiz(80)
            .WithMultipleChoiceQuestion(
                "What is correct?",
                new[] { "A", "B", "C", "D" },
                "A")
            .WithTrueFalseQuestion("Safety is important", true)
            .Build();

        // Assert
        talk.Questions.Should().HaveCount(2);
        talk.Questions.First().QuestionNumber.Should().Be(1);
        talk.Questions.Last().QuestionNumber.Should().Be(2);
    }

    [Fact]
    public void Build_AsInactive_SetsIsActiveFalse()
    {
        // Arrange & Act
        var talk = new ToolboxTalkBuilder()
            .AsInactive()
            .Build();

        // Assert
        talk.IsActive.Should().BeFalse();
    }

    [Fact]
    public void CreateBasicTalk_CreatesValidTalkWithSections()
    {
        // Act
        var talk = ToolboxTalkBuilder.CreateBasicTalk(title: "My Basic Talk");

        // Assert
        talk.Title.Should().Be("My Basic Talk");
        talk.Sections.Should().HaveCount(2);
        talk.RequiresQuiz.Should().BeFalse();
    }

    [Fact]
    public void CreateTalkWithQuiz_CreatesValidTalkWithQuestionsAndPassingScore()
    {
        // Act
        var talk = ToolboxTalkBuilder.CreateTalkWithQuiz(passingScore: 75);

        // Assert
        talk.RequiresQuiz.Should().BeTrue();
        talk.PassingScore.Should().Be(75);
        talk.Questions.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public void CreateTalkWithVideo_CreatesValidTalkWithVideoSettings()
    {
        // Act
        var talk = ToolboxTalkBuilder.CreateTalkWithVideo(videoUrl: "https://youtube.com/watch?v=abc123");

        // Assert
        talk.VideoUrl.Should().Be("https://youtube.com/watch?v=abc123");
        talk.VideoSource.Should().Be(QuantumBuild.Modules.ToolboxTalks.Domain.Enums.VideoSource.YouTube);
        talk.MinimumVideoWatchPercent.Should().Be(90);
    }
}
