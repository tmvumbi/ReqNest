using System.Globalization;
using System.IO.Compression;
using System.Text;
using ReqNest.Core.Reports;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ReqNest.Infrastructure.Reports;

/// <summary>
/// Renders branded, paginated report PDFs (A4) without external PDF dependencies:
/// header band with company and title, metadata strip, definitions, and a proper
/// table with sized columns, alternating rows, and page footers.
/// </summary>
public sealed class SimpleReportPdfGenerator : IReportPdfGenerator
{
    private const double PageWidth = 595;
    private const double PageHeight = 842;
    private const double Margin = 46;
    private const double ContentWidth = PageWidth - (Margin * 2);
    private const double FooterHeight = 34;
    private const double TableHeaderHeight = 21;
    private const double TableRowHeight = 17;

    private static readonly (double R, double G, double B) Brand = Rgb(0xD0471B);
    private static readonly (double R, double G, double B) BrandDark = Rgb(0x933112);
    private static readonly (double R, double G, double B) BrandTint = Rgb(0xFAE0D3);
    private static readonly (double R, double G, double B) TextDark = Rgb(0x1F2430);
    private static readonly (double R, double G, double B) TextMuted = Rgb(0x6B7280);
    private static readonly (double R, double G, double B) RowTint = Rgb(0xF7F7FB);
    private static readonly (double R, double G, double B) HairLine = Rgb(0xE4E7EE);

    public byte[] Generate(ReportPdfContent content)
    {
        var logo = CreateLogo(content.LogoBytes);
        var columns = ComputeColumns(content);
        var pages = new List<StringBuilder>();
        var page = NewPage(pages);

        var y = DrawFirstPageHeader(page, content, logo);
        y = DrawTableHeader(page, columns, y);
        var rowIndex = 0;
        if (content.Rows.Count == 0)
        {
            FillRect(page, Margin, y - TableRowHeight, ContentWidth, TableRowHeight, RowTint);
            DrawText(page, "—", Margin + 6, y - TableRowHeight + 5, 8, false, TextMuted);
            y -= TableRowHeight;
        }

        while (rowIndex < content.Rows.Count)
        {
            if (y - TableRowHeight < Margin + FooterHeight)
            {
                page = NewPage(pages);
                y = DrawContinuationHeader(page, content);
                y = DrawTableHeader(page, columns, y);
            }

            DrawRow(page, columns, content.Rows[rowIndex], rowIndex, y);
            y -= TableRowHeight;
            rowIndex++;
        }

        for (var index = 0; index < pages.Count; index++)
        {
            DrawFooter(pages[index], content.Footer, index + 1, pages.Count);
        }

        return Assemble(pages, logo);
    }

    // ---- Layout sections -------------------------------------------------

    private static double DrawFirstPageHeader(StringBuilder page, ReportPdfContent content, PdfLogo? logo)
    {
        var top = PageHeight - Margin;
        DrawText(page, Truncate(content.CompanyName, 14, true, ContentWidth - (logo is null ? 0 : 130)),
            Margin, top - 12, 14, true, TextDark);
        if (!string.IsNullOrWhiteSpace(content.CompanyContact))
        {
            DrawText(page, Truncate(content.CompanyContact, 8.5, false, ContentWidth - (logo is null ? 0 : 130)),
                Margin, top - 26, 8.5, false, TextMuted);
        }

        if (logo is not null)
        {
            const double boxWidth = 110;
            const double boxHeight = 34;
            var scale = Math.Min(boxWidth / logo.Width, boxHeight / logo.Height);
            var width = logo.Width * scale;
            var height = logo.Height * scale;
            page.Append(CultureInfo.InvariantCulture,
                $"q {width:0.##} 0 0 {height:0.##} {PageWidth - Margin - width:0.##} {top - height:0.##} cm /Logo Do Q\n");
        }

        var y = top - 44;
        page.Append(CultureInfo.InvariantCulture,
            $"{Brand.R:0.###} {Brand.G:0.###} {Brand.B:0.###} rg {Margin:0.##} {y:0.##} {ContentWidth:0.##} 1.6 re f\n");
        y -= 30;
        DrawText(page, Truncate(content.ReportTitle, 19, true, ContentWidth), Margin, y, 19, true, TextDark);
        y -= 30;
        var metaColumn = ContentWidth / 3;
        DrawMeta(page, content.GeneratedLabel, content.GeneratedValue, Margin, y, metaColumn - 12);
        DrawMeta(page, content.TimeZoneLabel, content.TimeZoneValue, Margin + metaColumn, y, metaColumn - 12);
        DrawMeta(page, content.RequestedByLabel, content.RequestedByValue, Margin + (metaColumn * 2), y, metaColumn - 12);
        y -= 32;
        DrawMeta(page, content.FiltersLabel, content.FiltersValue, Margin, y, ContentWidth);
        y -= 30;

        if (content.Definitions.Count > 0)
        {
            DrawText(page, content.DefinitionsLabel.ToUpperInvariant(), Margin, y, 7.5, true, TextMuted);
            y -= 13;
            foreach (var definition in content.Definitions)
            {
                foreach (var line in Wrap(definition, 8, false, ContentWidth - 10))
                {
                    DrawText(page, "•  " + line, Margin, y, 8, false, TextMuted);
                    y -= 11.5;
                }
            }

            y -= 8;
        }

        return y;
    }

    private static double DrawContinuationHeader(StringBuilder page, ReportPdfContent content)
    {
        var top = PageHeight - Margin;
        DrawText(page, Truncate($"{content.CompanyName}  ·  {content.ReportTitle}", 9.5, true, ContentWidth),
            Margin, top - 8, 9.5, true, TextMuted);
        StrokeLine(page, Margin, top - 16, PageWidth - Margin, top - 16, HairLine);
        return top - 30;
    }

    private static double DrawTableHeader(StringBuilder page, IReadOnlyList<PdfColumn> columns, double y)
    {
        FillRect(page, Margin, y - TableHeaderHeight, ContentWidth, TableHeaderHeight, BrandTint);
        foreach (var column in columns)
        {
            var label = Truncate(column.Header.ToUpperInvariant(), 7.5, true, column.Width - 12);
            var x = column.RightAligned
                ? column.X + column.Width - 6 - Measure(label, 7.5, true)
                : column.X + 6;
            DrawText(page, label, x, y - TableHeaderHeight + 7, 7.5, true, BrandDark);
        }

        return y - TableHeaderHeight;
    }

    private static void DrawRow(StringBuilder page, IReadOnlyList<PdfColumn> columns, string[] row, int rowIndex, double y)
    {
        if (rowIndex % 2 == 1)
        {
            FillRect(page, Margin, y - TableRowHeight, ContentWidth, TableRowHeight, RowTint);
        }

        StrokeLine(page, Margin, y - TableRowHeight, Margin + ContentWidth, y - TableRowHeight, HairLine);
        for (var index = 0; index < columns.Count; index++)
        {
            var column = columns[index];
            var value = Truncate(index < row.Length ? row[index] : string.Empty, 8, false, column.Width - 12);
            var x = column.RightAligned
                ? column.X + column.Width - 6 - Measure(value, 8, false)
                : column.X + 6;
            DrawText(page, value, x, y - TableRowHeight + 5, 8, false, TextDark);
        }
    }

    private static void DrawFooter(StringBuilder page, string footer, int pageNumber, int pageCount)
    {
        StrokeLine(page, Margin, Margin + 16, PageWidth - Margin, Margin + 16, HairLine);
        DrawText(page, Truncate(footer, 7.5, false, ContentWidth - 90), Margin, Margin + 5, 7.5, false, TextMuted);
        var label = $"{pageNumber} / {pageCount}";
        DrawText(page, label, PageWidth - Margin - Measure(label, 7.5, false), Margin + 5, 7.5, false, TextMuted);
    }

    private static void DrawMeta(StringBuilder page, string label, string value, double x, double y, double width)
    {
        DrawText(page, label.ToUpperInvariant(), x, y, 7, true, TextMuted);
        DrawText(page, Truncate(value, 9, false, width), x, y - 12.5, 9, false, TextDark);
    }

    // ---- Columns ----------------------------------------------------------

    private sealed record PdfColumn(string Header, double X, double Width, bool RightAligned);

    private static List<PdfColumn> ComputeColumns(ReportPdfContent content)
    {
        var count = Math.Max(1, content.Columns.Count);
        var desired = new double[count];
        var numeric = new bool[count];
        for (var index = 0; index < count; index++)
        {
            var header = index < content.Columns.Count ? content.Columns[index] : string.Empty;
            desired[index] = Measure(header.ToUpperInvariant(), 7.5, true) + 14;
            numeric[index] = true;
            foreach (var row in content.Rows.Take(200))
            {
                var value = index < row.Length ? row[index] : string.Empty;
                desired[index] = Math.Max(desired[index], Measure(value, 8, false) + 14);
                if (value.Length > 0 && !double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                {
                    numeric[index] = false;
                }
            }

            desired[index] = Math.Clamp(desired[index], 42, 220);
        }

        var total = desired.Sum();
        var factor = ContentWidth / total;
        var columns = new List<PdfColumn>(count);
        var x = Margin;
        for (var index = 0; index < count; index++)
        {
            var width = index == count - 1 ? Margin + ContentWidth - x : desired[index] * factor;
            columns.Add(new PdfColumn(
                index < content.Columns.Count ? content.Columns[index] : string.Empty,
                x,
                width,
                numeric[index]));
            x += width;
        }

        return columns;
    }

    // ---- Primitive drawing -------------------------------------------------

    private static StringBuilder NewPage(List<StringBuilder> pages)
    {
        var page = new StringBuilder();
        pages.Add(page);
        return page;
    }

    private static void FillRect(StringBuilder page, double x, double y, double width, double height, (double R, double G, double B) color)
    {
        page.Append(CultureInfo.InvariantCulture,
            $"{color.R:0.###} {color.G:0.###} {color.B:0.###} rg {x:0.##} {y:0.##} {width:0.##} {height:0.##} re f\n");
    }

    private static void StrokeLine(StringBuilder page, double x1, double y1, double x2, double y2, (double R, double G, double B) color)
    {
        page.Append(CultureInfo.InvariantCulture,
            $"{color.R:0.###} {color.G:0.###} {color.B:0.###} RG 0.5 w {x1:0.##} {y1:0.##} m {x2:0.##} {y2:0.##} l S\n");
    }

    private static void DrawText(StringBuilder page, string text, double x, double y, double size, bool bold, (double R, double G, double B) color)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        page.Append(CultureInfo.InvariantCulture,
            $"BT {color.R:0.###} {color.G:0.###} {color.B:0.###} rg /{(bold ? "F2" : "F1")} {size:0.##} Tf {x:0.##} {y:0.##} Td ({Escape(text)}) Tj ET\n");
    }

    // ---- Text metrics (standard Helvetica AFM widths, units per 1000) ------

    private static readonly short[] RegularWidths = BuildWidths(false);
    private static readonly short[] BoldWidths = BuildWidths(true);

    private static short[] BuildWidths(bool bold)
    {
        var widths = new short[224];
        Array.Fill(widths, (short)556);
        var ascii = bold
            ? "278 333 474 556 556 889 722 238 333 333 389 584 278 333 278 278 556 556 556 556 556 556 556 556 556 556 333 333 584 584 584 611 975 722 722 722 722 667 611 778 722 278 556 722 611 833 722 778 667 778 722 667 611 722 667 944 667 667 611 333 278 333 584 556 333 556 611 556 611 556 333 611 611 278 278 556 278 889 611 611 611 611 389 556 333 611 556 778 556 556 500 389 280 389 584"
            : "278 278 355 556 556 889 667 191 333 333 389 584 278 333 278 278 556 556 556 556 556 556 556 556 556 556 278 278 584 584 584 556 1015 667 667 722 722 667 611 778 722 278 500 667 556 833 722 778 667 778 722 667 611 722 667 944 667 667 611 278 278 278 469 556 333 556 556 500 556 556 278 556 556 222 222 500 222 833 556 556 556 556 333 500 278 556 500 722 500 500 500 334 260 334 584";
        var values = ascii.Split(' ');
        for (var index = 0; index < values.Length; index++)
        {
            widths[index] = short.Parse(values[index], CultureInfo.InvariantCulture);
        }

        return widths;
    }

    private static double Measure(string text, double size, bool bold)
    {
        var widths = bold ? BoldWidths : RegularWidths;
        double total = 0;
        foreach (var character in text)
        {
            var index = character - 32;
            total += index >= 0 && index < widths.Length ? widths[index] : 556;
        }

        return total * size / 1000;
    }

    private static string Truncate(string text, double size, bool bold, double maxWidth)
    {
        if (Measure(text, size, bold) <= maxWidth)
        {
            return text;
        }

        var ellipsisWidth = Measure("…", size, bold);
        var result = new StringBuilder();
        double total = 0;
        foreach (var character in text)
        {
            var width = Measure(character.ToString(), size, bold);
            if (total + width + ellipsisWidth > maxWidth)
            {
                break;
            }

            result.Append(character);
            total += width;
        }

        return result + "…";
    }

    private static IEnumerable<string> Wrap(string text, double size, bool bold, double maxWidth)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var line = new StringBuilder();
        foreach (var word in words)
        {
            var candidate = line.Length == 0 ? word : $"{line} {word}";
            if (Measure(candidate, size, bold) > maxWidth && line.Length > 0)
            {
                yield return line.ToString();
                line.Clear();
                line.Append(word);
            }
            else
            {
                line.Clear();
                line.Append(candidate);
            }
        }

        if (line.Length > 0)
        {
            yield return line.ToString();
        }
    }

    // ---- Document assembly --------------------------------------------------

    private static byte[] Assemble(List<StringBuilder> pages, PdfLogo? logo)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = Encoding.GetEncoding(1252, EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback);
        var pageCount = pages.Count;
        var regularFontId = 3 + (pageCount * 2);
        var boldFontId = regularFontId + 1;
        var logoId = logo is null ? (int?)null : boldFontId + 1;
        var finalObjectId = logoId ?? boldFontId;
        var objects = new Dictionary<int, byte[]>
        {
            [1] = encoding.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"),
        };
        var kids = string.Join(' ', Enumerable.Range(0, pageCount).Select(index => $"{3 + (index * 2)} 0 R"));
        objects[2] = encoding.GetBytes($"<< /Type /Pages /Kids [{kids}] /Count {pageCount} >>");

        for (var index = 0; index < pageCount; index++)
        {
            var pageId = 3 + (index * 2);
            var streamId = pageId + 1;
            var imageResource = logoId is null ? string.Empty : $" /XObject << /Logo {logoId} 0 R >>";
            objects[pageId] = encoding.GetBytes(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth} {PageHeight}] /Resources << /Font << /F1 {regularFontId} 0 R /F2 {boldFontId} 0 R >>{imageResource} >> /Contents {streamId} 0 R >>");
            var stream = encoding.GetBytes(pages[index].ToString());
            using var objectBody = new MemoryStream();
            Write(objectBody, encoding.GetBytes($"<< /Length {stream.Length} >>\nstream\n"));
            Write(objectBody, stream);
            Write(objectBody, encoding.GetBytes("\nendstream"));
            objects[streamId] = objectBody.ToArray();
        }

        objects[regularFontId] = encoding.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        objects[boldFontId] = encoding.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");
        if (logoId is not null && logo is not null)
        {
            using var imageBody = new MemoryStream();
            Write(imageBody, encoding.GetBytes(
                $"<< /Type /XObject /Subtype /Image /Width {logo.Width} /Height {logo.Height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode /Length {logo.CompressedRgb.Length} >>\nstream\n"));
            Write(imageBody, logo.CompressedRgb);
            Write(imageBody, encoding.GetBytes("\nendstream"));
            objects[logoId.Value] = imageBody.ToArray();
        }

        using var output = new MemoryStream();
        Write(output, Encoding.ASCII.GetBytes("%PDF-1.7\n%ReqNest\n"));
        var offsets = new long[finalObjectId + 1];
        for (var id = 1; id <= finalObjectId; id++)
        {
            offsets[id] = output.Position;
            Write(output, Encoding.ASCII.GetBytes($"{id} 0 obj\n"));
            Write(output, objects[id]);
            Write(output, Encoding.ASCII.GetBytes("\nendobj\n"));
        }

        var xref = output.Position;
        Write(output, Encoding.ASCII.GetBytes($"xref\n0 {finalObjectId + 1}\n0000000000 65535 f \n"));
        for (var id = 1; id <= finalObjectId; id++)
        {
            Write(output, Encoding.ASCII.GetBytes(offsets[id].ToString("0000000000", CultureInfo.InvariantCulture) + " 00000 n \n"));
        }

        Write(output, Encoding.ASCII.GetBytes(
            $"trailer\n<< /Size {finalObjectId + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF"));
        return output.ToArray();
    }

    private static PdfLogo? CreateLogo(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        try
        {
            var info = Image.Identify(bytes);
            if (info is null || info.Width <= 0 || info.Height <= 0 || (long)info.Width * info.Height > 16_000_000)
            {
                return null;
            }

            using var image = Image.Load<Rgba32>(bytes);
            if (image.Width > 800 || image.Height > 400)
            {
                image.Mutate(context => context.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(800, 400),
                }));
            }

            var rgb = new byte[checked(image.Width * image.Height * 3)];
            image.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (var x = 0; x < row.Length; x++)
                    {
                        var pixel = row[x];
                        var offset = ((y * image.Width) + x) * 3;
                        rgb[offset] = Flatten(pixel.R, pixel.A);
                        rgb[offset + 1] = Flatten(pixel.G, pixel.A);
                        rgb[offset + 2] = Flatten(pixel.B, pixel.A);
                    }
                }
            });

            using var compressed = new MemoryStream();
            using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                zlib.Write(rgb);
            }

            return new PdfLogo(image.Width, image.Height, compressed.ToArray());
        }
        catch (UnknownImageFormatException)
        {
            return null;
        }
        catch (InvalidImageContentException)
        {
            return null;
        }
    }

    private static (double R, double G, double B) Rgb(int hex) =>
        (((hex >> 16) & 0xFF) / 255d, ((hex >> 8) & 0xFF) / 255d, (hex & 0xFF) / 255d);

    private static byte Flatten(byte channel, byte alpha) =>
        (byte)(((channel * alpha) + (255 * (255 - alpha)) + 127) / 255);

    private static string Escape(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("(", "\\(", StringComparison.Ordinal)
        .Replace(")", "\\)", StringComparison.Ordinal);

    private static void Write(Stream output, ReadOnlySpan<byte> data) => output.Write(data);

    private sealed record PdfLogo(int Width, int Height, byte[] CompressedRgb);
}
