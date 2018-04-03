using System;
using System.Linq;

namespace SimpleJsonRest.Utils {
  internal delegate void MessageHandler(string errorMessage);

  public class Tracer {
    /// <summary>
    /// Won't trace messages if isn't true
    /// </summary>
    static public bool Loaded {
      get {
        if (!loadedWithoutError && totalAttemps < MAX_ATTEMPTS) SetupLog4Net();
        return loadedWithoutError;
      }
    }

    // TODO: Peut-être que cette logique de max_attemps/totalAttemps est complètement bidonne et inutile ==> C'est même très fortement probable, désolé de me vexer moi-même
    private const ushort MAX_ATTEMPTS = 3;
    static private ushort totalAttemps = 0;

    static internal event MessageHandler OnTracerError;
    static private bool loadedWithoutError = false;
    
    static private readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
    static private log4net.ILog Logger {
      get {
        if (log == null) SetupLog4Net();
        return log;
      }
    }

    /// <summary>
    /// Used to setup and configure the Log4Net tracer.
    /// If logPath isn't specified the function will be looking for a "json4Rest" config section containing the "LogPath" value
    /// </summary>
    /// <param name="logPath"></param>
    static internal void SetupLog4Net(string logPath = null) {
      totalAttemps++;

      if (logPath == null) {
        Utils.HandlerConfig config = null;
        try {
          config = System.Web.Configuration.WebConfigurationManager.GetSection("json4Rest") as Utils.HandlerConfig;
        }
        catch (Exception e) {
          throw new HandlerException($"Error with SimpleJsonRest's config section: {e.Message}", System.Net.HttpStatusCode.NotImplemented);
        }

        if (config == null) throw new NullReferenceException( "config is null" );
        logPath = config.LogPath;
      }

      log4net.Repository.Hierarchy.Hierarchy hierarchy = (log4net.Repository.Hierarchy.Hierarchy) log4net.LogManager.GetRepository();

      log4net.Layout.PatternLayout patternLayout = new log4net.Layout.PatternLayout();
      patternLayout.ConversionPattern = "[IDP-LOG] %d [%t] %-5p %c - %m%n";
      patternLayout.ActivateOptions();

      log4net.Appender.RollingFileAppender roller = new log4net.Appender.RollingFileAppender();
      try {
        roller.File = logPath;
        roller.AppendToFile = true;
        roller.RollingStyle = log4net.Appender.RollingFileAppender.RollingMode.Size;
        roller.DatePattern = "yyyy.MM.dd";
        roller.StaticLogFileName = true;
        roller.MaxSizeRollBackups = 10;
        roller.MaximumFileSize = "1MB";
        roller.Layout = patternLayout;
        roller.ActivateOptions();
        hierarchy.Root.AddAppender(roller);

        log4net.Appender.MemoryAppender memory = new log4net.Appender.MemoryAppender();
        memory.ActivateOptions();
        hierarchy.Root.AddAppender(memory);

        hierarchy.Root.Level = log4net.Core.Level.Debug;
        hierarchy.Configured = true;

        loadedWithoutError = true;
        Log( "log4net configured", MessageVerbosity.Info );
      }
      catch (Exception e) {
        OnTracerError?.Invoke( e.Message );
      }
    }

    static private string GetFormattedMethodName(System.Reflection.MethodInfo method, string end = "") {
      return $"{method.DeclaringType.FullName}.{method.Name}({string.Join(", ", method.GetParameters().Select(param => $"{param.Name}={{{param.Position}}}"))}){end}";
    }

    static private void LogIO(string methodNameFormatted, MethodInvokationDirection direction, params object[] paramz) {
      try {
        Logger.InfoFormat(methodNameFormatted, paramz);
      }
      catch (System.Exception e) {
        Logger.Error(direction == MethodInvokationDirection.Input ? "Error formatting method'name to invoke." : "Error formatting invoked method's name + return object.", e);
      }
    }

    static internal void LogInput(System.Reflection.MethodInfo method, params object[] paramz) {
      string methodNameFormatted = GetFormattedMethodName(method);
      LogIO(methodNameFormatted, MethodInvokationDirection.Input, paramz);
    }

    static internal void LogOutput(System.Reflection.MethodInfo method, object returnObject, params object[] paramz) {
      string methodNameFormatted = GetFormattedMethodName(method, $" returned ({returnObject})");
      LogIO(methodNameFormatted, MethodInvokationDirection.Output, paramz);
    }

    /// <summary>
    /// Log a message with Tracer.
    /// level should be between 1 and 3
    /// </summary>
    /// <param name="message"></param>
    /// <param name="level">
    /// test
    /// </param>
    [Obsolete("Use Log(string message, MessageVerbosity type)")]
    static public void Log(string message, uint level) {
      int logLevel;

      string logLvlString = System.Web.Configuration.WebConfigurationManager.AppSettings["LOG_LEVEL"];
      logLevel = logLvlString == null ? 3 : int.Parse( logLvlString );

      if (level > 0 && logLevel >= level) Logger.Info( message );
    }

    /// <summary>
    /// Log a message and an exception (message + stack trace)
    /// </summary>
    /// <param name="message"></param>
    /// <param name="exception"></param>
    static public void Log(string message, Exception exception) {
      InnerLog( message, MessageVerbosity.Error, System.Reflection.Assembly.GetCallingAssembly() == typeof( Tracer ).Assembly, exception );
    }

    /// <summary>
    /// Log a message with verbosity specification
    /// </summary>
    /// <param name="message"></param>
    /// <param name="type"></param>
    static public void Log(string message, MessageVerbosity type = MessageVerbosity.Info) {
      InnerLog( message, type, System.Reflection.Assembly.GetCallingAssembly() == typeof( Tracer ).Assembly );
    }

    static private void InnerLog(string message, MessageVerbosity type, bool isInnerCall, Exception e = null) {
      if (Loaded) {
        switch(type) {
          case MessageVerbosity.Debug:
            Tracer.Logger.Debug( message );
            break;
          case MessageVerbosity.Info:
            Tracer.Logger.Info( message );
            break;
          case MessageVerbosity.Error:
            if (isInnerCall) OnTracerError?.Invoke( message );
            if (e != null)  Tracer.Logger.Error( message, e );
            else            Tracer.Logger.Error( message );
            break;
        }
      }
      else OnTracerError?.Invoke( "Tracer is not loaded" );
    }
  }

  // TODO Check if this enum is useful ..
  enum MethodInvokationDirection {
    Input = 1,
    Output = 2
  }
}