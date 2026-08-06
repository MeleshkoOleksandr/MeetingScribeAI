using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xceed.Document.NET;
using Xceed.Words.NET;
using Xceed.Drawing;


namespace MeetingScribe.Logic.Services;

public static class MeetingSummarySaver
{
    public static void SaveGeneralSummary(string rawMarkdown, string meetingName, string meetingDate)
    {

        // 2. Декодируем и очищаем текст (Markdig / PlainText conversion)
        // Преобразуем Markdown в простой форматированный текст для вставки в Word (или HTML при необходимости)
        //string plainTextSummary = ConvertMarkdownToPlainText(rawMarkdown);

        var templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Template", "VerbaleRiunione_Gen.docx");
        //var templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Template", $"Verbale_{DateTime.Now:yyyyMMdd_HHmm}.docx");
        string outputPath = $"Verbale_{DateTime.Now:yyyyMMdd_HHmm}.docx";

        // 4. Заполнение шаблона и сохранение
        FillAndSaveTemplate(templatePath, outputPath, meetingName, meetingDate, rawMarkdown);

        Console.WriteLine($"Файл успешно сохранен: {outputPath}");
    }


    static void FillAndSaveTemplate(string templatePath, string outputPath, string meetingName, string date, string markdownContent)
    {
        using (var doc = DocX.Load(templatePath))
        {
            // 1. Заменяем обычные текстовые метаданные
            doc.ReplaceText("{MEETING_NAME}", meetingName);
            doc.ReplaceText("{DATE}", date);
        
            var targetParagraph = doc.Paragraphs.FirstOrDefault(p => p.Text.Contains("{TEXT}"));
            if (targetParagraph != null)
            {
                // Очищаем входную строку markdown от начальных пробелов и переносов (включая \xa0 / &nbsp;)
                string cleanMarkdown = markdownContent.TrimStart('\r', '\n', ' ', '\t', '\xa0');

                // Вставляем блоки
                AppendMarkdownToDocX(doc, targetParagraph, cleanMarkdown);

                // Удаляем сам пустой параграф {TEXT}, чтобы от него не оставалось лишнего переноса
                targetParagraph.Remove(false);
            }

            doc.SaveAs(outputPath);
        }
    }

    private static void AppendMarkdownToDocX(DocX doc, Paragraph insertAfterParagraph, string markdown)
    {
        // Подключаем расширения Markdig (включая поддержки таблиц Pipe Tables)
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        var markdownDoc = Markdig.Markdown.Parse(markdown, pipeline);

        Paragraph currentParagraph = insertAfterParagraph;

        foreach (var block in markdownDoc)
        {
            // --- 1. Заголовки (H1, H2, H3...) ---
            if (block is HeadingBlock heading)
            {
                currentParagraph = doc.InsertParagraph();

                // Форматирование текста внутри заголовка (Bold, Italic)
                AppendInlinesToParagraph(currentParagraph, heading.Inline);

                // Стилизуем размер и цвета заголовков
                switch (heading.Level)
                {
                    case 1:
                        currentParagraph.FontSize(18).Bold().Color(Xceed.Drawing.Color.Parse(183, 233, 126)).SpacingBefore(12).SpacingAfter(6);
                        break;
                    case 2:
                        currentParagraph.FontSize(14).Bold().Color(Xceed.Drawing.Color.Parse(129, 207, 255)).SpacingBefore(10).SpacingAfter(4);
                        break;
                    case 3:
                    default:
                        currentParagraph.FontSize(12).Bold().Color(Xceed.Drawing.Color.DimGray).SpacingBefore(8).SpacingAfter(2);
                        break;
                }
            }
            // --- 2. Обычные абзацы ---
            else if (block is ParagraphBlock paragraphBlock)
            {
                currentParagraph = doc.InsertParagraph();
                currentParagraph.FontSize(11).SpacingAfter(4);
                AppendInlinesToParagraph(currentParagraph, paragraphBlock.Inline);
            }
            // --- 3. Списки (Маркированные) ---
            else if (block is ListBlock listBlock)
            {
                foreach (var item in listBlock)
                {
                    if (item is ListItemBlock listItem)
                    {
                        foreach (var subBlock in listItem)
                        {
                            if (subBlock is ParagraphBlock subPara)
                            {
                                currentParagraph = doc.InsertParagraph();
                                currentParagraph.FontSize(11).SpacingAfter(2);

                                // Добавляем маркер списка (буллит)
                                currentParagraph.Append("• ").Bold();
                                AppendInlinesToParagraph(currentParagraph, subPara.Inline);
                                currentParagraph.IndentationBefore = 15; // Небольшой отступ
                            }
                        }
                    }
                }
            }
            // --- 4. Таблицы ---
            else if (block is Markdig.Extensions.Tables.Table tableBlock)
            {
                int rowCount = tableBlock.Count;
                int colCount = tableBlock.FirstOrDefault() is Markdig.Extensions.Tables.TableRow r ? r.Count : 0;

                if (rowCount > 0 && colCount > 0)
                {
                    var docTable = doc.AddTable(rowCount, colCount);
                    docTable.Design = TableDesign.TableGrid;
                    docTable.Alignment = Alignment.center;

                    for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
                    {
                        var markdownRow = (Markdig.Extensions.Tables.TableRow)tableBlock[rowIndex];
                        for (int colIndex = 0; colIndex < colCount; colIndex++)
                        {
                            var cell = markdownRow[colIndex] as Markdig.Extensions.Tables.TableCell;
                            string cellText = GetInlineText(cell);
                            var cellParagraph = docTable.Rows[rowIndex].Cells[colIndex].Paragraphs[0];
                            cellParagraph.Append(cellText);

                            // Заголовок таблицы делаем жирным и с серым фоном
                            if (rowIndex == 0 || markdownRow.IsHeader)
                            {
                                cellParagraph.Bold();
                                docTable.Rows[rowIndex].Cells[colIndex].FillColor = Xceed.Drawing.Color.LightGray;
                            }
                        }
                    }

                    doc.InsertTable(docTable);
                    currentParagraph = doc.InsertParagraph(); // Отступ после таблицы
                }
            }
        }
    }

    // Вспомогательный метод: рендерит жирный шрифт, курсив и обычный текст внутри строки
    private static void AppendInlinesToParagraph(Paragraph p, ContainerInline inlines)
    {
        if (inlines == null) return;

        foreach (var inline in inlines)
        {
            if (inline is LiteralInline literal)
            {
                p.Append(literal.Content.ToString());
            }
            else if (inline is EmphasisInline emphasis)
            {
                // Выясняем, курсив или жирный шрифт (**bold** / *italic*)
                bool isBold = emphasis.DelimiterCount == 2;
                bool isItalic = emphasis.DelimiterCount == 1;

                foreach (var subInline in emphasis)
                {
                    if (subInline is LiteralInline subLiteral)
                    {
                        var formatting = new Formatting();
                        if (isBold) formatting.Bold = true;
                        if (isItalic) formatting.Italic = true;

                        p.Append(subLiteral.Content.ToString(), formatting);
                    }
                }
            }
            else if (inline is LineBreakInline)
            {
                p.AppendLine();
            }
        }
    }

    // Извлечение текста из ячейки таблицы
    private static string GetInlineText(Markdig.Extensions.Tables.TableCell cell)
    {
        if (cell == null) return string.Empty;
        var para = cell.FirstOrDefault() as ParagraphBlock;
        if (para?.Inline == null) return string.Empty;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (var inline in para.Inline)
        {
            if (inline is LiteralInline literal)
                sb.Append(literal.Content.ToString());
        }
        return sb.ToString();
    }

}
