using System.Globalization;
using System.IO.Compression;
using System.Text;
using ReqNest.Core.Reports;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ReqNest.Infrastructure.Reports;

public sealed class SimpleReportPdfGenerator : IReportPdfGenerator
{
    private const int LinesPerPage = 46;

    public byte[] Generate(ReportPdfContent content)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = Encoding.GetEncoding(1252, EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback);
        var lines = new List<string>
        {
            content.CompanyName,
            content.ReportTitle,
            content.GeneratedLabel,
            content.TimeZoneLabel,
            content.RequestedByLabel,
            content.FiltersLabel,
            string.Empty,
            content.DefinitionsLabel,
        };
        lines.AddRange(content.Definitions.Select(definition => $"- {definition}"));
        lines.Add(string.Empty);
        lines.AddRange(content.TableLines);
        var pageBodies = lines.Chunk(LinesPerPage).ToArray();
        var pageCount = Math.Max(1, pageBodies.Length);
        var fontId = 3 + (pageCount * 2);
        var logo = CreateLogo(content.LogoBytes);
        var logoId = logo is null ? (int?)null : fontId + 1;
        var finalObjectId = logoId ?? fontId;
        var objects = new Dictionary<int, byte[]>();
        objects[1] = encoding.GetBytes("<< /Type /Catalog /Pages 2 0 R >>");
        var kids = string.Join(' ', Enumerable.Range(0, pageCount).Select(index => $"{3 + (index * 2)} 0 R"));
        objects[2] = encoding.GetBytes($"<< /Type /Pages /Kids [{kids}] /Count {pageCount} >>");

        for (var index = 0; index < pageCount; index++)
        {
            var pageId = 3 + (index * 2);
            var streamId = pageId + 1;
            var imageResource = logoId is null ? string.Empty : $" /XObject << /Logo {logoId} 0 R >>";
            objects[pageId] = encoding.GetBytes(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 {fontId} 0 R >>{imageResource} >> /Contents {streamId} 0 R >>");
            var body = new StringBuilder();
            if (logo is not null)
            {
                const double boxWidth = 110;
                const double boxHeight = 42;
                var scale = Math.Min(boxWidth / logo.Width, boxHeight / logo.Height);
                var displayWidth = logo.Width * scale;
                var displayHeight = logo.Height * scale;
                body.Append(CultureInfo.InvariantCulture, $"q {displayWidth:0.##} 0 0 {displayHeight:0.##} {535 - displayWidth:0.##} {790 - displayHeight:0.##} cm /Logo Do Q\n");
            }

            body.Append("BT\n/F1 10 Tf\n50 790 Td\n13 TL\n");
            var pageLines = pageBodies.Length == 0 ? [] : pageBodies[index];
            foreach (var line in pageLines)
            {
                body.Append('(').Append(Escape(line)).Append(") Tj\nT*\n");
            }

            body.Append($"({Escape(content.Footer)} - {index + 1}/{pageCount}) Tj\nET");
            var stream = encoding.GetBytes(body.ToString());
            using var objectBody = new MemoryStream();
            Write(objectBody, encoding.GetBytes($"<< /Length {stream.Length} >>\nstream\n"));
            Write(objectBody, stream);
            Write(objectBody, encoding.GetBytes("\nendstream"));
            objects[streamId] = objectBody.ToArray();
        }

        objects[fontId] = encoding.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
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

    private static byte Flatten(byte channel, byte alpha) =>
        (byte)(((channel * alpha) + (255 * (255 - alpha)) + 127) / 255);

    private static string Escape(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("(", "\\(", StringComparison.Ordinal)
        .Replace(")", "\\)", StringComparison.Ordinal);

    private static void Write(Stream output, ReadOnlySpan<byte> data) => output.Write(data);

    private sealed record PdfLogo(int Width, int Height, byte[] CompressedRgb);
}
