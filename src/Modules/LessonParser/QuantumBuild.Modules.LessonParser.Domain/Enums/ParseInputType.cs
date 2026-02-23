namespace QuantumBuild.Modules.LessonParser.Domain.Enums;

/// <summary>
/// Type of input document used for lesson parsing
/// </summary>
public enum ParseInputType
{
    /// <summary>
    /// PDF document
    /// </summary>
    Pdf = 1,

    /// <summary>
    /// Word document (DOCX)
    /// </summary>
    Docx = 2,

    /// <summary>
    /// URL to web content
    /// </summary>
    Url = 3,

    /// <summary>
    /// Raw text content
    /// </summary>
    Text = 4
}
