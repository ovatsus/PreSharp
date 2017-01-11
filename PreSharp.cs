using System.Collections.Generic;
using System.IO;

public static class PreSharp {

    private static TextWriter writer;
    public static TextWriter Writer { get { return writer; } }

    internal static readonly List<string> CompileGeneratedFiles = new List<string>();
    internal static readonly List<string> EmbeddedResourceGeneratedFiles = new List<string>();
    internal static bool HasOutputs { get; private set; }

    private static string currentFile;

    public enum OutputType { Compile, EmbeddedResource, None }

    public static void Flush() {
        if (currentFile != null) {
            string output = writer.ToString();
            if (!File.Exists(currentFile) || output != File.ReadAllText(currentFile)) {
                File.WriteAllText(currentFile, output);
            }
            currentFile = null;
            writer = null;
        }
    }

    public static void SetOutput(string outputFile, OutputType outputType) {
        Flush();
        currentFile = outputFile;
        writer = new StringWriter();
        if (outputType == OutputType.Compile) {
            CompileGeneratedFiles.Add(outputFile);
        } else if (outputType == OutputType.EmbeddedResource) {
            EmbeddedResourceGeneratedFiles.Add(outputFile);
        }
        HasOutputs = true;
    }

    public static void Install() {
        PreSharpEntryPoint.Install();
    }
}