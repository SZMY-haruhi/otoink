using System.IO;

namespace Otoink.App.Asr;

public static class ModelLocator
{
    public const string ModelFolderName = "sherpa-onnx-sense-voice-zh-en-ja-ko-yue-2024-07-17";

    public static string Root =>
        Path.Combine(AppContext.BaseDirectory, "models", ModelFolderName);

    public static IEnumerable<string> Roots()
    {
        yield return Root;
    }

    public static string ModelOnnx => Path.Combine(Root, "model.int8.onnx");

    public static string ModelOnnxFallback => Path.Combine(Root, "model.onnx");

    public static string Tokens => Path.Combine(Root, "tokens.txt");

    public static string ResolveModelPath()
    {
        if (File.Exists(ModelOnnx))
            return ModelOnnx;
        if (File.Exists(ModelOnnxFallback))
            return ModelOnnxFallback;
        return ModelOnnx;
    }

    public static bool IsInstalled() => IsInstalledAt(Root);

    private static bool IsInstalledAt(string root) =>
        File.Exists(Path.Combine(root, "tokens.txt"))
        && File.Exists(Path.Combine(root, "model.int8.onnx"));
}
