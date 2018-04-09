using SimpleJsonRest.Core.Log;
using System.Linq;

namespace SimpleJsonRest.Utils {
  internal delegate void MessageHandler(string errorMessage);

  /// <summary>
  /// Class used to log some debug, info and/or error messages.
  /// Is used by library, but can be accessed from outside also.
  /// See following methods: Tracer.Log(string message, Exception exception) or Tracer.Log(string message, MessageVerbosity type)
  /// </summary>
  public class Tracer {
    /// <summary>
    /// Won't trace messages if isn't true
    /// </summary>
    public static bool Loaded {
      get {
        if (!loadedWithoutError && totalAttemps < MAX_ATTEMPTS) SetupLogger();
        return loadedWithoutError;
      }
    }

    internal static event MessageHandler OnTracerError;

    // TODO: Peut-être que cette logique de max_attemps/totalAttemps est complètement bidonne et inutile ==> C'est même très fortement probable, désolé de me vexer moi-même
    private const ushort MAX_ATTEMPTS = 3;
    private static ushort totalAttemps;
    private static bool loadedWithoutError;

    private static FileLogger log;
    private static FileLogger Logger => log ?? SetupLogger();

    internal static FileLogger SetupLogger(string logPath = null) {
      // TODO: Actuellement, ça ne log nulle part lorsqu'IIS démarre le handler ==> Ne faut-il pas aussi créer un fichier log dans le dossier de destination ? (Config IIS)
      totalAttemps++;

      if (logPath == null) {
        Utils.HandlerConfig config = null;
        try {
          config = System.Web.Configuration.WebConfigurationManager.GetSection( "json4Rest" ) as Utils.HandlerConfig;
        }
        catch (System.Exception e) {
          throw new HandlerException( $"Error with SimpleJsonRest's config section: {e.Message}", System.Net.HttpStatusCode.NotImplemented );
        }

        if (config == null) throw new System.NullReferenceException( "config is null" );

        // get IIS web site folder combined with specified path in web.config
        logPath = System.IO.Path.Combine( System.Web.HttpRuntime.AppDomainAppPath, config.LogPath );
      }

      loadedWithoutError = true;
      return log = new FileLogger( logPath );
    }

    private static string GetFormattedMethodName(System.Reflection.MethodInfo method, string end = "") {
      return $"{method.DeclaringType.FullName}.{method.Name}({string.Join(", ", method.GetParameters().Select(param => $"{param.Name}={{{param.Position}}}"))}){end}";
    }

    private static void LogIO(string methodNameFormatted, MethodInvokationDirection direction, params object[] paramz) {
      try {
        Logger.InfoFormat( methodNameFormatted, paramz );
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
    /// Log a message and an exception (message + stack trace)
    /// </summary>
    /// <param name="message"></param>
    /// <param name="exception"></param>
    public static void Log(string message, System.Exception exception) {
      InnerLog( message, MessageVerbosity.Error, System.Reflection.Assembly.GetCallingAssembly() == typeof( Tracer ).Assembly, exception );
    }

    /// <summary>
    /// Log a message with verbosity specification
    /// </summary>
    /// <param name="message"></param>
    /// <param name="type"></param>
    public static void Log(string message, MessageVerbosity type = MessageVerbosity.Info) {
      InnerLog( message, type, System.Reflection.Assembly.GetCallingAssembly() == typeof( Tracer ).Assembly );
    }

    internal static void Log(System.Exception exception) {
      /// Get calling method fullname
      var prevMethod = new System.Diagnostics.StackFrame( 1 ).GetMethod();
      var methodName = $"{prevMethod.DeclaringType}.{prevMethod.Name}";
      /// Reuse static public method
      Log( methodName, exception );
    }

    private static void InnerLog(string message, MessageVerbosity type, bool isInnerCall, System.Exception e = null) {
      if (Loaded) {
        switch(type) {
          case MessageVerbosity.Debug:
            Logger.Debug( message );

            break;
          case MessageVerbosity.Info:
            Logger.Info( message );
            break;
          case MessageVerbosity.Error:
            if (isInnerCall) OnTracerError?.Invoke( message );
            if (e != null)  Logger.Error( message, e );
            else            Logger.Error( message );
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