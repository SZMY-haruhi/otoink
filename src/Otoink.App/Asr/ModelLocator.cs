using System.IO;

namespace Otoink.App.Asr;

public static class ModelLocator
{
    public const string ModelFolderName = "sherpa-onnx-sense-voice-zh-en-ja-ko-yue-2024-07-17";

    public static string Root =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "otoink",
            "models",
            ModelFolderName);

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

    public static bool IsInstalled() =>
        File.Exists(Tokens) && (File.Exists(ModelOnnx) || File.Exists(ModelOnnxFallback));
}
