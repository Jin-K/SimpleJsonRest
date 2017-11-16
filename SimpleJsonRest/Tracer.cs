using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using System;
using System.Linq;
using System.Reflection;
using System.Web.Configuration;

namespace SimpleJsonRest.Utils {
    public static class Tracer {
        static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        internal static ILog Logger {
            get { return log; }
        }

        static Tracer() {
            SetupLog4Net();
            Logger.Info("log4net configured");
        }

        static void SetupLog4Net() {
            Config config = null;
            try {
                config = WebConfigurationManager.GetSection("json4Rest") as Config;
            }
            catch (Exception e) {
                throw new HandlerException($"Error with SimpleJsonRest's config section: {e.Message}", System.Net.HttpStatusCode.NotImplemented);
            }

            Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();

            PatternLayout patternLayout = new PatternLayout();
            patternLayout.ConversionPattern = "[IDP-LOG] %d [%t] %-5p %c - %m%n";
            patternLayout.ActivateOptions();

            RollingFileAppender roller = new RollingFileAppender();
            roller.File = config.LogPath;
            roller.AppendToFile = true;
            roller.RollingStyle = RollingFileAppender.RollingMode.Size;
            roller.DatePattern = "yyyy.MM.dd";
            roller.StaticLogFileName = true;
            roller.MaxSizeRollBackups = 10;
            roller.MaximumFileSize = "1MB";
            roller.Layout = patternLayout;
            roller.ActivateOptions();
            hierarchy.Root.AddAppender(roller);

            MemoryAppender memory = new MemoryAppender();
            memory.ActivateOptions();
            hierarchy.Root.AddAppender(memory);

            hierarchy.Root.Level = Level.Debug;
            hierarchy.Configured = true;
        }

        static string GetFormattedMethodName(MethodInfo method, string end = "") {
            return $"{method.DeclaringType.FullName}.{method.Name}({string.Join(", ", method.GetParameters().Select(param => $"{param.Name}={{{param.Position}}}"))}){end}";
        }

        static void LogIO(string methodNameFormatted, MethodInvokationDirection direction, params object[] paramz) {
            try {
                Logger.InfoFormat(methodNameFormatted, paramz);
            }
            catch (Exception e) {
                Logger.Error(direction == MethodInvokationDirection.Input ? "Error formatting method'name to invoke." : "Error formatting invoked method's name + return object.", e);
            }
        }

        internal static void LogInput(MethodInfo method, params object[] paramz) {
            string methodNameFormatted = GetFormattedMethodName(method);
            LogIO(methodNameFormatted, MethodInvokationDirection.Input, paramz);
        }

        internal static void LogOutput(MethodInfo method, object returnObject, params object[] paramz) {
            string methodNameFormatted = GetFormattedMethodName(method, $" returned ({returnObject})");
            LogIO(methodNameFormatted, MethodInvokationDirection.Output, paramz);
        }

        public static void Log(string message, int level = 1) {
            int logLevel = int.Parse(WebConfigurationManager.AppSettings["LOG_LEVEL"]);
            if (logLevel < level) return;

            Logger.Info(message);
        }
    }

    enum MethodInvokationDirection {
        Input = 1,
        Output = 2
    }
}