using Avalonia.Controls;
using Avalonia.Platform.Storage;

using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xceed.Document.NET;
using Xceed.Words.NET;

namespace MeetingScribe.Logic.Services;

public static class MeetingSummarySaver
{
    // Colors used for H1 / H2 headings.
    private static readonly Xceed.Drawing.Color HeadingColorH1 = Xceed.Drawing.Color.Parse(183, 233, 126);
    private static readonly Xceed.Drawing.Color HeadingColorH2 = Xceed.Drawing.Color.Parse(129, 207, 255);

    public static async Task SaveGeneralSummaryAsync(string rawMarkdown, string meetingName, string meetingDate, Window ownerWindow)
    {
        var templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Template", "VerbaleRiunione_Gen.docx");

        if (!File.Exists(templatePath))
        {
            LogService.Instance.LogError($"Template not found: {templatePath}");
            return;
        }

        var topLevel = TopLevel.GetTopLevel(ownerWindow);
        if (topLevel?.StorageProvider is not { } storageProvider)
        {
            LogService.Instance.LogInfo("StorageProvider is not available on this platform.");
            return;
        }

        var suggestedFileName = $"Verbale_{meetingDate}.docx";
   
        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Salva verbale riunione",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "docx",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Word Document")
                {
                    Patterns = new[] { "*.docx" }
                }
            }
        });

        if (file is null)
        {
            // User cancelled the dialog — nothing to save or open.
            return;
        }

        string outputPath = file.Path.LocalPath;

        FillAndSaveTemplate(templatePath, outputPath, meetingName, meetingDate, rawMarkdown);

        Console.WriteLine($"Файл успешно сохранен: {outputPath}");

        OpenFile(outputPath);
    }

    // Opens the saved .docx with whatever application the OS has associated with
    // that extension (Word, LibreOffice, etc.). 
    private static void OpenFile(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            LogService.Instance.LogError($"Failed to open file {path}: {ex.Message}");
        }
    }

    private static void FillAndSaveTemplate(string templatePath, string outputPath, string meetingName, string date, string markdownContent)
    {
        using (var doc = DocX.Load(templatePath))
        {
            doc.ReplaceText("{MEETING_NAME}", meetingName);
            doc.ReplaceText("{DATE}", date);

            var targetParagraph = doc.Paragraphs.FirstOrDefault(p => p.Text.Contains("{TEXT}"));
            if (targetParagraph != null)
            {
                // Strip leading whitespace / non-breaking spaces the model sometimes emits.
                string cleanMarkdown = markdownContent.TrimStart('\r', '\n', ' ', '\t', '\xa0');

                AppendMarkdownToDocX(targetParagraph, cleanMarkdown);

                // Remove the now-empty {TEXT} placeholder paragraph itself.
                targetParagraph.Remove(false);
            }

            doc.SaveAs(outputPath);
        }
    }

    private static void AppendMarkdownToDocX(Paragraph insertAfterParagraph, string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        var markdownDoc = Markdig.Markdown.Parse(markdown, pipeline);

        Paragraph anchor = insertAfterParagraph;

        foreach (var block in markdownDoc)
        {
            if (block is HeadingBlock heading)
            {
                anchor = InsertHeading(anchor, heading);
            }
            else if (block is ParagraphBlock paragraphBlock)
            {
                anchor = InsertParagraphBlock(anchor, paragraphBlock);
            }
            else if (block is ListBlock listBlock)
            {
                anchor = InsertList(anchor, listBlock);
            }
            else if (block is Markdig.Extensions.Tables.Table tableBlock)
            {
                anchor = InsertTable(anchor, tableBlock);
            }
            // Other markdown constructs (blockquotes, code fences, thematic breaks, links)
            // are not produced by the summarizer today, so they're intentionally skipped.
            // Add another "else if" branch here if that ever changes.
        }
    }

    private static Paragraph InsertHeading(Paragraph anchor, HeadingBlock heading)
    {
        var p = anchor.InsertParagraphAfterSelf(string.Empty);
        AppendInlinesToParagraph(p, heading.Inline);

        switch (heading.Level)
        {
            case 1:
                p.FontSize(18).Bold().Color(HeadingColorH1).SpacingBefore(12).SpacingAfter(6);
                break;
            case 2:
                p.FontSize(14).Bold().Color(HeadingColorH2).SpacingBefore(10).SpacingAfter(4);
                break;
            default:
                p.FontSize(12).Bold().Color(Xceed.Drawing.Color.DimGray).SpacingBefore(8).SpacingAfter(2);
                break;
        }

        return p;
    }

    private static Paragraph InsertParagraphBlock(Paragraph anchor, ParagraphBlock paragraphBlock)
    {
        var p = anchor.InsertParagraphAfterSelf(string.Empty);
        p.FontSize(11).SpacingAfter(4);
        AppendInlinesToParagraph(p, paragraphBlock.Inline);
        return p;
    }

    private static Paragraph InsertList(Paragraph anchor, ListBlock listBlock)
    {
        int itemNumber = 1;

        foreach (var item in listBlock)
        {
            if (item is not ListItemBlock listItem)
            {
                continue;
            }

            foreach (var subBlock in listItem)
            {
                if (subBlock is not ParagraphBlock subPara)
                {
                    continue;
                }

                string marker = listBlock.IsOrdered ? $"{itemNumber}. " : "• ";

                var p = anchor.InsertParagraphAfterSelf(string.Empty);
                p.FontSize(11).SpacingAfter(2);
                p.IndentationBefore = 15;

                p.Append(marker).Bold();
                AppendInlinesToParagraph(p, subPara.Inline);

                anchor = p;
            }

            itemNumber++;
        }

        return anchor;
    }

    private static Paragraph InsertTable(Paragraph anchor, Markdig.Extensions.Tables.Table tableBlock)
    {
        int rowCount = tableBlock.Count;
        int colCount = tableBlock.FirstOrDefault() is Markdig.Extensions.Tables.TableRow firstRow ? firstRow.Count : 0;

        if (rowCount == 0 || colCount == 0)
        {
            return anchor;
        }

        var table = anchor.InsertTableAfterSelf(rowCount, colCount);
        table.Design = TableDesign.TableGrid;
        table.Alignment = Alignment.center;

        for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            var markdownRow = (Markdig.Extensions.Tables.TableRow)tableBlock[rowIndex];

            for (int colIndex = 0; colIndex < colCount; colIndex++)
            {
                var cell = markdownRow[colIndex] as Markdig.Extensions.Tables.TableCell;
                var cellParagraph = table.Rows[rowIndex].Cells[colIndex].Paragraphs[0];
                cellParagraph.Append(GetInlineText(cell));

                if (rowIndex == 0 || markdownRow.IsHeader)
                {
                    cellParagraph.Bold();
                    table.Rows[rowIndex].Cells[colIndex].FillColor = Xceed.Drawing.Color.LightGray;
                }
            }
        }

        // A table needs a paragraph right after it so later content (and our anchor
        // pointer) has somewhere valid to attach to.
        var spacer = table.InsertParagraphAfterSelf(string.Empty);
        return spacer;
    }

    /// <summary>
    /// Renders inline markdown (bold / italic / line breaks) into a Word paragraph.
    /// </summary>
    private static void AppendInlinesToParagraph(Paragraph p, ContainerInline inlines, Formatting inherited = null)
    {
        if (inlines == null)
        {
            return;
        }

        foreach (var inline in inlines)
        {
            if (inline is LiteralInline literal)
            {
                if (inherited != null)
                {
                    p.Append(literal.Content.ToString(), inherited);
                }
                else
                {
                    p.Append(literal.Content.ToString());
                }
            }
            else if (inline is EmphasisInline emphasis)
            {
                var formatting = new Formatting
                {
                    Bold = (inherited?.Bold ?? false) || emphasis.DelimiterCount is 2 or 3,
                    Italic = (inherited?.Italic ?? false) || emphasis.DelimiterCount is 1 or 3,
                };

                AppendInlinesToParagraph(p, emphasis, formatting);
            }
            else if (inline is LineBreakInline)
            {
                p.AppendLine();
            }
        }
    }

    /// <summary>
    /// Extracts plain text from a table cell for use in Word table cells.
    /// </summary>
    private static string GetInlineText(Markdig.Extensions.Tables.TableCell cell)
    {
        if (cell == null)
        {
            return string.Empty;
        }

        var para = cell.FirstOrDefault() as ParagraphBlock;
        if (para?.Inline == null)
        {
            return string.Empty;
        }

        return ExtractPlainText(para.Inline);
    }

    private static string ExtractPlainText(ContainerInline inlines)
    {
        var sb = new StringBuilder();

        foreach (var inline in inlines)
        {
            if (inline is LiteralInline literal)
            {
                sb.Append(literal.Content.ToString());
            }
            else if (inline is EmphasisInline emphasis)
            {
                sb.Append(ExtractPlainText(emphasis));
            }
            else if (inline is LineBreakInline)
            {
                sb.Append(' ');
            }
        }
        return sb.ToString();
    }
}