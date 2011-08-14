using System;

internal abstract class Logger : MarshalByRefObject {

    private bool success = true;

    public bool Success {
        get { return success; }
        protected set { success = value; }
    }

    public abstract void LogMessage(string message);
    public abstract void LogWarning(string file, string errorCode, string message, int line, int column);
    public abstract void LogError(string file, string errorCode, string message, int line, int column);
    public abstract void LogException(string file, Exception exception);
}