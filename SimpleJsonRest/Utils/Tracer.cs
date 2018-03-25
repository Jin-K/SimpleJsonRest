using System;
using System.Linq;

namespace SimpleJsonRest.Utils {
  public static class Tracer {
    static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
    internal static log4net.ILog Logger => log;

    static Tracer() {
      SetupLog4Net();
      Logger.Info("log4net configured");
    }

    static void SetupLog4Net() {
      Utils.HandlerConfig config = null;
      try {
        config = System.Web.Configuration.WebConfigurationManager.GetSection("json4Rest") as Utils.HandlerConfig;
      }
      catch (System.Exception e) {
        throw new HandlerException($"Error with SimpleJsonRest's config section: {e.Message}", System.Net.HttpStatusCode.NotImplemented);
      }

      log4net.Repository.Hierarchy.Hierarchy hierarchy = (log4net.Repository.Hierarchy.Hierarchy) log4net.LogManager.GetRepository();

      log4net.Layout.PatternLayout patternLayout = new log4net.Layout.PatternLayout();
      patternLayout.ConversionPattern = "[IDP-LOG] %d [%t] %-5p %c - %m%n";
      patternLayout.ActivateOptions();

      log4net.Appender.RollingFileAppender roller = new log4net.Appender.RollingFileAppender();
      roller.File = config.LogPath;
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
    }

    static string GetFormattedMethodName(System.Reflection.MethodInfo method, string end = "") {
      return $"{method.DeclaringType.FullName}.{method.Name}({string.Join(", ", method.GetParameters().Select(param => $"{param.Name}={{{param.Position}}}"))}){end}";
    }

    static void LogIO(string methodNameFormatted, MethodInvokationDirection direction, params object[] paramz) {
      try {
        Logger.InfoFormat(methodNameFormatted, paramz);
      }
      catch (System.Exception e) {
        Logger.Error(direction == MethodInvokationDirection.Input ? "Error formatting method'name to invoke." : "Error formatting invoked method's name + return object.", e);
      }
    }

    internal static void LogInput(System.Reflection.MethodInfo method, params object[] paramz) {
      string methodNameFormatted = GetFormattedMethodName(method);
      LogIO(methodNameFormatted, MethodInvokationDirection.Input, paramz);
    }

    internal static void LogOutput(System.Reflection.MethodInfo method, object returnObject, params object[] paramz) {
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
    public static void Log(string message, int level = 1) {
      int logLevel;

      string logLvlString = System.Web.Configuration.WebConfigurationManager.AppSettings["LOG_LEVEL"];
      logLevel = logLvlString == null ? 3 : int.Parse(logLvlString);

      if (logLevel >= level) Logger.Info(message);
    }
  }

  enum MethodInvokationDirection {
    Input = 1,
    Output = 2
  }
}