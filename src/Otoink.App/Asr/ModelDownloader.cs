using System.IO;
using System.Net.Http;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace Otoink.App.Asr;

/// <summary>
/// Build-time helper. Runtime must not call this — models ship next to the exe.
/// </summary>
public static class ModelDownloader
{
    public const string ArchiveUrl =
        "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-sense-voice-zh-en-ja-ko-yue-2024-07-17.tar.bz2";

    public static async Task InstallAsync(HttpClient http, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(ModelLocator.Root);

        var tempBz2 = Path.Combine(Path.GetTempPath(), $"otoink-sense-voice-{Guid.NewGuid():N}.tar.bz2");

        try
        {
            await using (var file = File.Create(tempBz2))
            {
                using var response = await http.GetAsync(
                    ArchiveUrl,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                await response.Content.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
            }

            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                ExtractModelFiles(tempBz2);
            }, cancellationToken).ConfigureAwait(false);

            if (!ModelLocator.IsInstalled())
                throw new InvalidOperationException("archive-missing-model");
        }
        finally
        {
            TryDelete(tempBz2);
        }
    }

    private static void ExtractModelFiles(string archivePath)
    {
        using var stream = File.OpenRead(archivePath);
        using var reader = ReaderFactory.OpenReader(stream);

        while (reader.MoveToNextEntry())
        {
            var entry = reader.Entry;
            if (entry.IsDirectory || string.IsNullOrEmpty(entry.Key))
                continue;

            var fileName = Path.GetFileName(entry.Key.Replace('\\', '/'));
            var dest = fileName switch
            {
                "model.int8.onnx" => ModelLocator.ModelOnnx,
                "tokens.txt" => ModelLocator.Tokens,
                _ => null
            };

            if (dest is null)
                continue;

            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            reader.WriteEntryToFile(dest, new ExtractionOptions { Overwrite = true });
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup of temp archive files.
        }
    }
}
