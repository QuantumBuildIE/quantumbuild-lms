using System.IO.Compression;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Services.Scorm;

public class ScormPackageService(IToolboxTalksDbContext context) : IScormPackageService
{
    // SCORM 1.2 namespaces — verified against the pipwerks/SCORM-Manifests reference manifest,
    // the de facto community template for hand-rolled SCORM 1.2 packages (see docs/scorm-export-recon.md Part 1).
    private static readonly XNamespace ImsCpNs = "http://www.imsproject.org/xsd/imscp_rootv1p1p2";
    private static readonly XNamespace AdlCpNs = "http://www.adlnet.org/xsd/adlcp_rootv1p2";

    public async Task<ScormPackageResult?> GenerateMinimalPackageAsync(
        Guid talkId,
        Guid tenantId,
        string language,
        CancellationToken ct = default)
    {
        // IgnoreQueryFilters + explicit tenant check: this service has no HTTP-context dependency
        // by design, so it stays safe if a future chunk calls it from a Hangfire job (see CLAUDE.md Note 22/23).
        var talk = await context.ToolboxTalks
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == talkId && t.TenantId == tenantId && !t.IsDeleted, ct);

        if (talk == null)
        {
            return null;
        }

        var firstSection = await context.ToolboxTalkSections
            .IgnoreQueryFilters()
            .Where(s => s.ToolboxTalkId == talkId && !s.IsDeleted)
            .OrderBy(s => s.SectionNumber)
            .FirstOrDefaultAsync(ct);

        var manifestBytes = BuildManifestXml(talk.Id, talk.Title);
        var indexHtmlBytes = BuildIndexHtml(talk.Title, firstSection);
        var zipBytes = BuildZip(manifestBytes, indexHtmlBytes);

        return new ScormPackageResult(zipBytes, talk.Title, talk.Code);
    }

    /// <summary>
    /// Builds imsmanifest.xml for a single-SCO SCORM 1.2 package.
    /// Deliberately omits the schemas/ folder + xsi:schemaLocation attribute that some hand-rolled
    /// manifests bundle: SCORM Cloud and every mainstream LMS validate against the well-known
    /// ADL/IMS namespace URIs baked into their own validators, not by resolving a local schema
    /// file path, so the risk of shipping a subtly-wrong copied XSD outweighs the benefit for this
    /// minimal package. Revisit if SCORM Cloud validation (Part E, manual step) surfaces a gap.
    /// </summary>
    private static byte[] BuildManifestXml(Guid talkId, string title)
    {
        var idSuffix = talkId.ToString("N");
        var manifestIdentifier = $"com.quantumbuild.scorm.{idSuffix}";
        var orgIdentifier = $"ORG-{idSuffix}";
        var itemIdentifier = $"ITEM-{idSuffix}";
        var resourceIdentifier = $"RES-{idSuffix}";

        var manifest = new XElement(ImsCpNs + "manifest",
            new XAttribute("identifier", manifestIdentifier),
            new XAttribute("version", "1"),
            new XAttribute(XNamespace.Xmlns + "adlcp", AdlCpNs.NamespaceName),
            new XElement(ImsCpNs + "metadata",
                new XElement(ImsCpNs + "schema", "ADL SCORM"),
                new XElement(ImsCpNs + "schemaversion", "1.2")
            ),
            new XElement(ImsCpNs + "organizations",
                new XAttribute("default", orgIdentifier),
                new XElement(ImsCpNs + "organization",
                    new XAttribute("identifier", orgIdentifier),
                    new XElement(ImsCpNs + "title", title),
                    new XElement(ImsCpNs + "item",
                        new XAttribute("identifier", itemIdentifier),
                        new XAttribute("identifierref", resourceIdentifier),
                        new XElement(ImsCpNs + "title", title)
                    )
                )
            ),
            new XElement(ImsCpNs + "resources",
                new XElement(ImsCpNs + "resource",
                    new XAttribute("identifier", resourceIdentifier),
                    new XAttribute("type", "webcontent"),
                    new XAttribute(AdlCpNs + "scormtype", "sco"),
                    new XAttribute("href", "index.html"),
                    new XElement(ImsCpNs + "file",
                        new XAttribute("href", "index.html")
                    )
                )
            )
        );

        var doc = new XDocument(manifest);

        using var memoryStream = new MemoryStream();
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = true,
            OmitXmlDeclaration = false
        };
        using (var writer = XmlWriter.Create(memoryStream, settings))
        {
            doc.Save(writer);
        }

        return memoryStream.ToArray();
    }

    private static byte[] BuildIndexHtml(string talkTitle, ToolboxTalkSection? firstSection)
    {
        var encodedTitle = WebUtility.HtmlEncode(talkTitle);
        var sectionHtml = firstSection != null
            ? $"<h2>{WebUtility.HtmlEncode(firstSection.Title)}</h2>\n        <div class=\"section-content\">{firstSection.Content}</div>"
            : "<p><em>No content available for this learning yet.</em></p>";

        var html = $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
            <meta charset="UTF-8" />
            <title>{{encodedTitle}}</title>
            <style>
              body { font-family: Arial, Helvetica, sans-serif; max-width: 800px; margin: 2rem auto; padding: 0 1rem; line-height: 1.5; color: #1a1a1a; }
              #complete-btn { margin-top: 2rem; padding: 0.75rem 1.5rem; font-size: 1rem; cursor: pointer; }
              #scorm-status { margin-top: 1rem; font-size: 0.85rem; color: #555; }
            </style>
            </head>
            <body>
            <h1>{{encodedTitle}}</h1>
            <div id="content">
                {{sectionHtml}}
            </div>
            <button id="complete-btn" type="button">Mark Complete</button>
            <div id="scorm-status"></div>
            <script>
            {{ScormBridgeScript}}
            </script>
            </body>
            </html>
            """;

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(html);
    }

    /// <summary>
    /// Minimal inline SCORM 1.2 JS bridge — window.API discovery + Initialize/SetValue/Commit/Finish only.
    /// Deliberately hand-rolled and inline per Chunk 1 scope; Chunk 3 evaluates scorm-again as a replacement.
    /// Walks the window.parent chain (and window.opener as a fallback) looking for the LMS-injected API
    /// object, per the standard SCORM 1.2 discovery algorithm. No-ops with a status message (not a throw)
    /// when no API is found, so the package can be opened directly in a browser for QA.
    /// </summary>
    private const string ScormBridgeScript = """
        (function () {
          var API = null;

          function findAPI(win) {
            var attempts = 0;
            while (win && !win.API && win.parent && win.parent !== win && attempts < 500) {
              attempts++;
              win = win.parent;
            }
            return (win && win.API) || null;
          }

          function getAPI() {
            var found = findAPI(window);
            if (!found && window.opener) {
              found = findAPI(window.opener);
            }
            return found;
          }

          function setStatus(message) {
            var el = document.getElementById('scorm-status');
            if (el) { el.textContent = message; }
          }

          function initScorm() {
            API = getAPI();
            if (!API) {
              setStatus('SCORM API not found - running in standalone/preview mode.');
              return;
            }
            var result = API.LMSInitialize('');
            if (result === 'false' || result === false) {
              console.warn('LMSInitialize returned failure; error: ' + (API.LMSGetLastError ? API.LMSGetLastError() : 'unknown'));
            }
            setStatus('Connected to LMS.');
          }

          function completeScorm() {
            if (!API) {
              setStatus('No LMS connection - completion not reported.');
              return;
            }
            API.LMSSetValue('cmi.core.lesson_status', 'completed');
            API.LMSCommit('');
            setStatus('Completion reported to LMS.');
          }

          function finishScorm() {
            if (API) {
              API.LMSFinish('');
            }
          }

          window.addEventListener('load', initScorm);
          window.addEventListener('beforeunload', finishScorm);
          document.addEventListener('DOMContentLoaded', function () {
            var btn = document.getElementById('complete-btn');
            if (btn) { btn.addEventListener('click', completeScorm); }
          });
        })();
        """;

    /// <summary>
    /// Zips imsmanifest.xml + index.html at the ZIP root (no wrapping folder — the single most
    /// common SCORM packaging error per the recon's LMS-compatibility survey). Entry names are
    /// plain root-level filenames so the forward-slash-vs-backslash concern doesn't arise yet;
    /// later chunks adding subfolders (schemas/, assets/) must build entry names with '/' explicitly.
    /// </summary>
    private static byte[] BuildZip(byte[] manifestBytes, byte[] indexHtmlBytes)
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "imsmanifest.xml", manifestBytes);
            WriteEntry(archive, "index.html", indexHtmlBytes);
        }

        return memoryStream.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string entryName, byte[] content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        entryStream.Write(content, 0, content.Length);
    }
}
