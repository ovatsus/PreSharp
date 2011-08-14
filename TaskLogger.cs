using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

internal sealed class TaskLogger : Logger {

    private AppDomainIsolatedTask _task;

    public TaskLogger(AppDomainIsolatedTask task) {
        _task = task;
    }

    public override void LogMessage(string message) {
        _task.Log.LogMessage(MessageImportance.High, message);
    }

    public override void LogWarning(string file, string errorCode, string message, int line, int column) {
        _task.Log.LogWarning(null, errorCode, null, file, line, column, line, column, message);
    }

    public override void LogError(string file, string errorCode, string message, int line, int column) {
        _task.Log.LogError(null, errorCode, null, file, line, column, line, column, message);
        Success = false;
    }

    public override void LogException(string file, Exception exception) {
        _task.Log.LogErrorFromException(exception, true, true, file);
        Success = false;
    }
}