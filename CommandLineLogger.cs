using System;

internal sealed class CommandLineLogger : Logger {

    public override void LogMessage(string message) {
        Console.WriteLine(message);
    }

    public override void LogWarning(string file, string errorCode, string message, int line, int column) {
        Console.WriteLine("{0}({1},{2}): {3} {4}: {5}",
                          file,
                          line,
                          column,
                          "warning",
                          errorCode,
                          message);
    }

    public override void LogError(string file, string errorCode, string message, int line, int column) {
        Console.WriteLine("{0}({1},{2}): {3} {4}: {5}",
                          file,
                          line,
                          column,
                          "error",
                          errorCode,
                          message);
        Success = false;
    }

    public override void LogException(string file, Exception exception) {
        Console.WriteLine("{0}: {1}", file, exception);
        Success = false;
    }
}