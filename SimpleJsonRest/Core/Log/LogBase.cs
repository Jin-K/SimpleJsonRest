namespace SimpleJsonRest.Core.Log {
  abstract class LogBase {
    protected readonly object lockObj = new object();
    protected int threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;

    protected abstract void Log(string message, Verbosity verbosity);

    internal protected virtual void Info(string message) {
      Log( message, Verbosity.INFO );
    }

    internal protected virtual void Debug(string message) {
      Log( message, Verbosity.DEBUG );
    }

    internal protected virtual void Error(string message) {
      Log( message, Verbosity.ERROR );
    }

    internal protected virtual void Error(string message, System.Exception exception) {
      Error( message + System.Environment.NewLine + exception + System.Environment.NewLine + exception.StackTrace );
    }

    protected enum Verbosity {
      INFO,
      DEBUG,
      ERROR
    }
  }
}