using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PropertyTransferManagement.Services.Reporting
{
    /// <summary>A simple table: title + column headers + rows.</summary>
    public class TableReport
    {
        public string Title { get; init; } = string.Empty;
        public string? Subtitle { get; init; }
        public string[] Columns { get; init; } = Array.Empty<string>();
        public List<object?[]> Rows { get; } = new();
        public List<(string Label, string Value)> Summary { get; } = new();
    }

    /// <summary>
    /// Exports admin tables to Excel (ClosedXML, MIT) and PDF
    /// (QuestPDF Community licence, free for this use).
    /// </summary>
    public static class ReportExporter
    {
        public const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        public static byte[] ToExcel(TableReport report)
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add(SafeSheetName(report.Title));

            sheet.Cell(1, 1).Value = report.Title;
            sheet.Cell(1, 1).Style.Font.Bold = true;
            sheet.Cell(1, 1).Style.Font.FontSize = 14;
            sheet.Cell(2, 1).Value = report.Subtitle ?? $"Generated {DateTime.Now:g}";

            int row = 4;
            foreach (var (label, value) in report.Summary)
            {
                sheet.Cell(row, 1).Value = label;
                sheet.Cell(row, 1).Style.Font.Bold = true;
                sheet.Cell(row, 2).Value = value;
                row++;
            }
            if (report.Summary.Count > 0) row++;

            int headerRow = row;
            for (int c = 0; c < report.Columns.Length; c++)
            {
                var cell = sheet.Cell(headerRow, c + 1);
                cell.Value = report.Columns[c];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#142B49");
                cell.Style.Font.FontColor = XLColor.White;
            }

            foreach (var values in report.Rows)
            {
                row++;
                for (int c = 0; c < values.Length; c++)
                {
                    var cell = sheet.Cell(row, c + 1);
                    switch (values[c])
                    {
                        case null: break;
                        case decimal d: cell.Value = d; cell.Style.NumberFormat.Format = "#,##0.00"; break;
                        case int i: cell.Value = i; break;
                        case long l: cell.Value = l; break;
                        case double db: cell.Value = db; break;
                        case bool b: cell.Value = b ? "Yes" : "No"; break;
                        case DateTime dt: cell.Value = dt.ToLocalTime(); cell.Style.DateFormat.Format = "yyyy-mm-dd hh:mm"; break;
                        default: cell.Value = values[c]!.ToString(); break;
                    }
                }
            }

            if (report.Rows.Count > 0)
                sheet.Range(headerRow, 1, row, report.Columns.Length).SetAutoFilter();

            sheet.Columns().AdjustToContents(1, 60);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        public static byte[] ToPdf(TableReport report)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(report.Columns.Length > 6 ? PageSizes.A4.Landscape() : PageSizes.A4);
                    page.Margin(28);
                    page.DefaultTextStyle(t => t.FontSize(8.5f));

                    page.Header().Column(col =>
                    {
                        col.Item().Text(report.Title).FontSize(16).Bold().FontColor("#142B49");
                        col.Item().Text(report.Subtitle ?? $"Generated {DateTime.Now:g}").FontColor(Colors.Grey.Darken1);
                        foreach (var (label, value) in report.Summary)
                            col.Item().Text(t => { t.Span(label + ": ").Bold(); t.Span(value); });
                        col.Item().PaddingBottom(6);
                    });

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            foreach (var _ in report.Columns) cols.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            foreach (var name in report.Columns)
                                header.Cell().Background("#142B49").Padding(3).Text(name).FontColor(Colors.White).Bold();
                        });

                        bool shade = false;
                        foreach (var values in report.Rows)
                        {
                            var bg = shade ? Colors.Grey.Lighten4 : Colors.White;
                            foreach (var v in values)
                                table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                                    .Padding(3).Text(Format(v));
                            shade = !shade;
                        }
                    });

                    page.Footer().AlignRight().Text(t =>
                    {
                        t.Span("Page ");
                        t.CurrentPageNumber();
                        t.Span(" / ");
                        t.TotalPages();
                    });
                });
            }).GeneratePdf();
        }

        public static ExportFile Export(TableReport report, string? format, string fileBaseName)
        {
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmm");
            return string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase)
                ? new ExportFile(ToPdf(report), "application/pdf", $"{fileBaseName}-{stamp}.pdf")
                : new ExportFile(ToExcel(report), ExcelContentType, $"{fileBaseName}-{stamp}.xlsx");
        }

        private static string Format(object? v) => v switch
        {
            null => "",
            decimal d => d.ToString("N2"),
            bool b => b ? "Yes" : "No",
            DateTime dt => dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
            _ => v.ToString() ?? ""
        };

        private static string SafeSheetName(string title)
        {
            var name = new string(title.Where(ch => !"[]:*?/\\".Contains(ch)).ToArray());
            return name.Length > 31 ? name[..31] : (name.Length == 0 ? "Report" : name);
        }
    }

    public record ExportFile(byte[] Content, string ContentType, string FileName);
}
