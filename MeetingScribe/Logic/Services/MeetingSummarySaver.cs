using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System;
using System.IO;
using System.Linq;
using System.Text;
using Xceed.Document.NET;
using Xceed.Words.NET;

namespace MeetingScribe.Logic.Services;

public static class MeetingSummarySaver
{
    // Colors used for H1 / H2 headings.
    private static readonly Xceed.Drawing.Color HeadingColorH1 = Xceed.Drawing.Color.Parse(183, 233, 126);
    private static readonly Xceed.Drawing.Color HeadingColorH2 = Xceed.Drawing.Color.Parse(129, 207, 255);

    public static void SaveGeneralSummary(string rawMarkdown, string meetingName, string meetingDate)
    {
        var templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Template", "VerbaleRiunione_Gen.docx");
        string outputPath = $"Verbale_{DateTime.Now:yyyyMMdd_HHmm}.docx";

        if (!File.Exists(templatePath))
        {
            // Fail loudly with a clear message instead of letting DocX.Load throw
            // a generic "file not found" exception deep inside a third-party library.
            throw new FileNotFoundException($"Template not found: {templatePath}");
        }

        FillAndSaveTemplate(templatePath, outputPath, meetingName, meetingDate, rawMarkdown);

        Console.WriteLine($"Файл успешно сохранен: {outputPath}");
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

    /// <summary>
    /// Walks the parsed markdown tree and inserts each block right after <paramref name="insertAfterParagraph"/>,
    /// advancing an "anchor" pointer as it goes. This keeps blocks in the original order and preserves any
    /// template content that comes AFTER the {TEXT} placeholder (e.g. a signature block), which the previous
    /// implementation (doc.InsertParagraph() / doc.InsertTable()) did not — those always appended to the very
    /// end of the document, regardless of where the placeholder actually was.
    /// </summary>
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
        // NOTE: if your installed Xceed.Words.NET version doesn't expose
        // Table.InsertParagraphAfterSelf, replace the line below with:
        //     var spacer = doc.InsertParagraph();  // (falls back to end-of-document, like before)
        var spacer = table.InsertParagraphAfterSelf(string.Empty);
        return spacer;
    }

    /// <summary>
    /// Renders inline markdown (bold / italic / line breaks) into a Word paragraph.
    /// Rewritten to be recursive so nested emphasis (e.g. "**bold *and italic***")
    /// is handled correctly, and so DelimiterCount == 3 (i.e. ***bold italic***) is no
    /// longer silently dropped, which is what the original if/else chain did.
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
    /// BUG FIX: the original version only read direct LiteralInline children, so any
    /// bold/italic text inside a table cell (e.g. "**Scadenza**") was silently dropped.
    /// This version recurses into EmphasisInline so nothing gets lost.
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