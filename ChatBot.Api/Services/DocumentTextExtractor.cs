using System.Text;
using UglyToad.PdfPig;

namespace ChatBot.Api.Services;

public sealed class DocumentTextExtractor
{
    public async Task<string> ExtractAsync(
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName);
        if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            using var memory = new MemoryStream();
            await content.CopyToAsync(memory, cancellationToken);
            using var pdf = PdfDocument.Open(memory.ToArray());
            return string.Join(
                Environment.NewLine,
                pdf.GetPages().Select(page => page.Text));
        }

        if (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".md", StringComparison.OrdinalIgnoreCase))
        {
            using var reader = new StreamReader(
                content,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                leaveOpen: true);
            return await reader.ReadToEndAsync(cancellationToken);
        }

        throw new NotSupportedException("Only PDF, TXT, and MD files are supported.");
    }
}
