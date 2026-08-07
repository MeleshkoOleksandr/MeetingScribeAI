using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xceed.Document.NET;
using Xceed.Words.NET;

namespace MeetingScribe.Logic.Services;

public static class MeetingSummarySaver
{
    // Colors used for H1 / H2 headings. Defined once so they are easy to tweak
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

        string outputPath = await PickSaveFileAsync(ownerWindow, $"Verbale_Gen_{meetingDate}.docx");
        if (outputPath == null)
        {
            // User cancelled the dialog — nothing to save or open.
            return;
        }

        FillAndSaveTemplate(templatePath, outputPath, meetingName, meetingDate, rawMarkdown);
        LogService.Instance.LogInfo($"Summary file is saved : {outputPath}");
        OpenFile(outputPath);
    }

    /// <summary>
    /// Saves the "structured" verbale (Direzione / Gestionale / Operativa / Eventuali) template.
    /// </summary>
    public static async Task SaveTemplateSummaryAsync(string rawMarkdown, string meetingDate, string present, string absent, string topics, Window ownerWindow)
    {
        var templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Template", "VerbaleRiunione_Template.docx");

        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException($"Template not found: {templatePath}");
        }

        string outputPath = await PickSaveFileAsync(ownerWindow, $"Verbale_{meetingDate}.docx");
        if (outputPath == null)
        {
            return;
        }

        // rawMarkdown looks like:
        //   ## 1. Informazioni dalla Direzione
        //   ...content...
        //   ## 2. Parte Gestionale
        //   ...content...
        // Split it into { 1: "...", 2: "...", 3: "...", 4: "..." } so each part can go
        // into its own placeholder ({DEREZ}, {GEST}, {OPER}, {EVENT}).
        var sections = SplitMarkdownIntoNumberedSections(rawMarkdown);

        using (var doc = DocX.Load(templatePath))
        {
            doc.ReplaceText(new StringReplaceTextOptions { SearchValue = "{DATE}", NewValue = meetingDate });
            doc.ReplaceText(new StringReplaceTextOptions { SearchValue = "{PARTS}", NewValue = present });
            doc.ReplaceText(new StringReplaceTextOptions { SearchValue = "{ASSEN}", NewValue = absent });
            doc.ReplaceText(new StringReplaceTextOptions { SearchValue = "{TOPICS}", NewValue = topics });

            ReplacePlaceholderWithMarkdown(doc, "{DEREZ}", sections.GetValueOrDefault(1, string.Empty));
            ReplacePlaceholderWithMarkdown(doc, "{GEST}", sections.GetValueOrDefault(2, string.Empty));
            ReplacePlaceholderWithMarkdown(doc, "{OPER}", sections.GetValueOrDefault(3, string.Empty));
            ReplacePlaceholderWithMarkdown(doc, "{EVENT}", sections.GetValueOrDefault(4, string.Empty));

            doc.SaveAs(outputPath);
        }

        Console.WriteLine($"Файл успешно сохранен: {outputPath}");

        OpenFile(outputPath);
    }

    /// <summary>
    /// Splits a markdown document into sections keyed by the number in headings shaped like
    /// "## 1. Some Title". The heading line itself is dropped — only the content between one
    /// numbered heading and the next (or end of string) is kept.
    /// </summary>
    private static Dictionary<int, string> SplitMarkdownIntoNumberedSections(string markdown)
    {
        var sections = new Dictionary<int, string>();

        // Matches "## 1." / "##  2." etc. at the start of a line; RegexOptions.Multiline
        // makes ^ match after every \n, not just at the start of the whole string.
        var headingRegex = new Regex(@"^##\s*(\d+)\.", RegexOptions.Multiline);
        var matches = headingRegex.Matches(markdown);

        for (int i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            if (!int.TryParse(match.Groups[1].Value, out int sectionNumber))
            {
                continue;
            }

            // Content starts on the line right after the heading...
            int contentStart = markdown.IndexOf('\n', match.Index);
            contentStart = contentStart == -1 ? markdown.Length : contentStart + 1;

            // ...and runs up to the next numbered heading (or end of the document).
            int contentEnd = i + 1 < matches.Count ? matches[i + 1].Index : markdown.Length;

            sections[sectionNumber] = markdown[contentStart..contentEnd].Trim();
        }

        return sections;
    }

    /// <summary>
    /// Shows the Avalonia "Save file" dialog and returns the chosen local path,
    /// or null if the user cancelled. Shared by every SaveXxxSummaryAsync method.
    /// </summary>
    private static async Task<string> PickSaveFileAsync(Window ownerWindow, string suggestedFileName)
    {
        var topLevel = TopLevel.GetTopLevel(ownerWindow);
        if (topLevel?.StorageProvider is not { } storageProvider)
        {
            LogService.Instance.LogError("StorageProvider is not available on this platform.");
            return "";
        }

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save meeting summary",
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

        return file?.Path.LocalPath;
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
            Console.WriteLine($"Не удалось открыть файл автоматически: {ex.Message}");
        }
    }

    private static void FillAndSaveTemplate(string templatePath, string outputPath, string meetingName, string date, string markdownContent)
    {
        using (var doc = DocX.Load(templatePath))
        {
            // ReplaceText(string, string) is obsolete in newer Xceed.Words.NET versions;
            // the recommended replacement takes a StringReplaceTextOptions object instead.
            doc.ReplaceText(new StringReplaceTextOptions
            {
                SearchValue = "{MEETING_NAME}",
                NewValue = meetingName
            });
            doc.ReplaceText(new StringReplaceTextOptions
            {
                SearchValue = "{DATE}",
                NewValue = date
            });

            ReplacePlaceholderWithMarkdown(doc, "{TEXT}", markdownContent);

            doc.SaveAs(outputPath);
        }
    }

    /// <summary>
    /// Finds the paragraph containing <paramref name="placeholder"/>, inserts the rendered
    /// markdown right after it, then removes the (now empty) placeholder paragraph.
    /// Shared by FillAndSaveTemplate ({TEXT}) and SaveTemplateSummaryAsync ({DEREZ}/{GEST}/{OPER}/{EVENT}).
    /// </summary>
    private static void ReplacePlaceholderWithMarkdown(DocX doc, string placeholder, string markdown)
    {
        var targetParagraph = doc.Paragraphs.FirstOrDefault(p => p.Text.Contains(placeholder));
        if (targetParagraph == null)
        {
            return;
        }

        // Strip leading whitespace / non-breaking spaces the model sometimes emits.
        string cleanMarkdown = (markdown ?? string.Empty).TrimStart('\r', '\n', ' ', '\t', '\xa0');

        AppendMarkdownToDocX(targetParagraph, cleanMarkdown);

        targetParagraph.Remove(false);
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