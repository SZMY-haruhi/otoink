using System.IO;
using System.Net.Http;
using SharpCompress.Archives;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;

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
        var tempTar = Path.ChangeExtension(tempBz2, ".tar");

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
                DecompressBz2ToTar(tempBz2, tempTar);
                cancellationToken.ThrowIfCancellationRequested();
                ExtractModelFiles(tempTar);
            }, cancellationToken).ConfigureAwait(false);

            if (!ModelLocator.IsInstalled())
                throw new InvalidOperationException("archive-missing-model");
        }
        finally
        {
            TryDelete(tempBz2);
            TryDelete(tempTar);
        }
    }

    private static void DecompressBz2ToTar(string bz2Path, string tarPath)
    {
        using var input = File.OpenRead(bz2Path);
        using var bz2 = new BZip2Stream(input, CompressionMode.Decompress, decompressConcatenated: false);
        using var output = File.Create(tarPath);
        bz2.CopyTo(output);
    }

    private static void ExtractModelFiles(string tarPath)
    {
        using var archive = TarArchive.Open(tarPath);

        foreach (var entry in archive.Entries)
        {
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
            entry.WriteToFile(dest, new ExtractionOptions { Overwrite = true });
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
