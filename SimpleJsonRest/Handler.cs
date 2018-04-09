using SimpleJsonRest.Utils;
namespace SimpleJsonRest {
  public class Handler : System.Web.IHttpHandler, System.Web.SessionState.IRequiresSessionState {
    public static HandlerState State { get; internal set; } = HandlerState.StartPointIsIIS;

    private static IAuthentifiedService service;
    private IAuthentifiedService Service => service ?? LoadService();

    private static Route[] router;
    private Route[] Router => router ?? PrepareNLoadServiceRoutes(Service);

    private static string endpoint;
    private string EndPoint => endpoint ?? LoadEndPointFromConfig();

    private static Utils.HandlerConfig config;
    private Utils.HandlerConfig Config => config ?? LoadConfig();

    public bool IsReusable {
      // Return false in case your Managed Handler cannot be reused for another request.
      // Usually this would be false in case you have some state information preserved per request.
      get { return false; }
    }

    public void ProcessRequest(System.Web.HttpContext context) {
      var url_part = GetUrl(context.Request.RawUrl);

      // Pour tests avec page html
      if (url_part.ToLower().EndsWith(".html") || url_part.ToLower().EndsWith(".htm")) {
        var files = System.IO.Directory.GetFiles(context.Server.MapPath("~"));
        for (var c = 0; c < files.Length; c++) {
          var file = files[c].ToLower();
          if (file.Contains(url_part.Substring(1))) {
            context.Response.ContentType = "text/html";
            context.Response.Write(System.IO.File.ReadAllText(file));
            context.Response.End();
            break;
          }
        }
      }

      var ip = context.Request.ServerVariables["REMOTE_ADDR"];
      
      Utils.Tracer.Log( $" > > > > > > Start request || {ip} || path: {url_part} || --- {System.DateTime.Now} ---->" );

      context.Response.Clear();

      object json_response = null;

      try {
        context.Response.ContentType = "application/json; charset=utf-8";

        foreach (var route in Router)
          if (route.Check(url_part)) {
            Utils.Tracer.Log("route prise : " + route.Path);
            json_response = route.Execute();
            return;
          }

        // Si route pas trouvée
        json_response = new { error = "Unknown path" };
        context.Response.StatusCode = (int) System.Net.HttpStatusCode.NotFound;
      }
      catch (System.Exception e) {
        // TODO Complètement revoir la gestion des exceptions ... ==> FaultException, FaultException2 ? Pourquoi j'ai utilisé ces classes pourries ?
        string exceptionType = e.GetType().Name;
        switch (exceptionType) {
          case "FaultException":
            FaultException _e = e as FaultException;
            context.Response.StatusCode = (int) System.Net.HttpStatusCode.BadRequest;
            json_response = new { error = _e.Message };
            break;
          case "FaultException2":
            HandlerException _e2 = e as HandlerException;
            context.Response.StatusCode = (int)_e2.StatusCode;
            json_response = new { error = _e2.Message };
            break;
          case "TargetInvocationException": // exception coming from a reflected assembly method
            context.Response.StatusCode = (int) System.Net.HttpStatusCode.InternalServerError;
            json_response = new {
              error = (e as System.Reflection.TargetInvocationException).InnerException.Message,
              type = exceptionType,
              trace = (e as System.Reflection.TargetInvocationException).InnerException.StackTrace
            };
            break;
          default:
            context.Response.StatusCode = (int) System.Net.HttpStatusCode.InternalServerError;
            json_response = new {
              error = e.Message,
              type = exceptionType,
              trace = e.StackTrace
            };
            break;
        }

        Utils.Tracer.Log("IDPHandler.ProcessRequest " + e);
      }
      finally {
        Utils.Tracer.Log( $" < < < < < < < End request || {ip} || path: {url_part} || --- {System.DateTime.Now} <----" );

        /// If there is a response
        if (json_response != null)  context.Reply( json_response );
        /// If Route.Execute() returned null ==> Reject 401
        else                        context.Response.StatusCode = (int) System.Net.HttpStatusCode.Unauthorized;

        /// Applies cross-domain http-header if needed
        if (
          System.Web.Configuration.WebConfigurationManager.AppSettings["cross_domain"] != null &&
          int.TryParse(System.Web.Configuration.WebConfigurationManager.AppSettings["cross_domain"], out int cross_domain) &&
          cross_domain == 1
        ) context.Response.AppendHeader("Access-Control-Allow-Origin", "*");

        /// End request traitement and flush
        context.Response.End();
      }
    }

    /// <summary>
    /// Parses received url
    /// </summary>
    /// <param name="url"></param>
    /// <param name="appPath"></param>
    /// <returns></returns>
    //private string GetUrl(string url, string appPath) {
    //  if (appPath.Length > 1 && appPath.Trim() != string.Empty) {
    //    if (appPath.EndsWith("/")) appPath = appPath.Remove(appPath.Length - 1);
    //    int pos = url.IndexOf(appPath);
    //    if (pos >= 0) url = url.Substring(0, pos) + url.Substring(pos + appPath.Length);
    //    if (!url.StartsWith("/")) url = "/" + url;
    //  }
    //  if (url.Length > 1 && url.EndsWith("/")) url = url.Remove(url.Length - 1);
    //  return url;
    //}

    private string GetUrl(string rawUrl) {
      if (EndPoint.Length > 0) rawUrl = rawUrl.Replace( EndPoint, "" );
      return rawUrl.Replace( "//", "/" );
    }

    private Route[] PrepareNLoadServiceRoutes(IAuthentifiedService service) {
      var type = service.GetType();
      var methods = type.GetMethods();
      var routes = new System.Collections.Generic.List<Route>();
      
      foreach(var m in methods) {
        if (m.IsPublic && m.DeclaringType == type)
          routes.Add( new Route( m, service ) );
      }

      return router = routes.ToArray();
    }

    /// <summary>
    /// Loads found service and returns it
    /// </summary>
    /// <returns></returns>
    private IAuthentifiedService LoadService() {
      if (Config == null) throw new HandlerException("No Service config value found", System.Net.HttpStatusCode.NotImplemented);
      return service = System.Activator.CreateInstance(Config.ServiceType) as IAuthentifiedService;
    }

    private string LoadEndPointFromConfig() {
      return endpoint = Config.EndPoint ?? "";
    }

    private Utils.HandlerConfig LoadConfig() {
      try {
        config = System.Web.Configuration.WebConfigurationManager.GetSection( "json4Rest" ) as Utils.HandlerConfig;
      }
      catch (System.Exception e) {
        throw new HandlerException( $"Error with SimpleJsonRest's config section: {e.Message}" );
      }
      return config;
    }
  }

  /// <summary>
  /// Defines SimpleJsonRest behavior
  /// </summary>
  public enum HandlerState { // TODO Use it and set Handler.State !!
    StartPointIsIIS,
    Integrated,
    Undefined
  }
}