using System;
using System.Web;
using System.Web.Configuration;
using System.Web.SessionState;
using System.Linq;
using SimpleHandler.Routing;
using SimpleHandler.Utils;

namespace SimpleHandler {
  public class Handler : IHttpHandler, IRequiresSessionState {

    public static HandlerState State => HandlerState.Undefined;

    static IAuthentifiedService service;
    IAuthentifiedService Service => service ?? LoadService();

    static Route[] router;
    Route[] Router => router ?? PrepareNLoadServiceRoutes(Service);

    public bool IsReusable {
      // Return false in case your Managed Handler cannot be reused for another request.
      // Usually this would be false in case you have some state information preserved per request.
      get { return true; }
    }


    public void ProcessRequest(HttpContext context) {
      var url_part = GetUrl(context.Request.RawUrl, context.Request.ApplicationPath);

      // Pour tests avec page html
      if (url_part.ToLower().EndsWith(".html") || url_part.ToLower().EndsWith(".htm")) {
        var file = System.IO.Directory.GetFiles(context.Server.MapPath("~")).FirstOrDefault(fN => fN.ToLower().Contains(url_part.Substring(1)));
        if (file != null) {
          context.Response.ContentType = "text/html";
          context.Response.Write(System.IO.File.ReadAllText(file));
          context.Response.End();
        }
      }

      var ip = context.Request.ServerVariables["REMOTE_ADDR"];

      Tracer.Logger.Info($" --- || Start request || {ip} || path: {url_part} || --- {DateTime.Now.ToString()}    ---     <--");

      context.Response.Clear();

      object json_response = null;

      try {
        context.Response.ContentType = "application/json; charset=utf-8";

        foreach (var route in Router)
          if (route.Check(url_part)) {
            Tracer.Logger.Info("route prise : " + route.Path);
            json_response = route.Execute();
            return;
          }

        // Si route pas trouvée
        json_response = new { error = "Unknown path" };
        context.Response.StatusCode = 404;
      }
      catch (Exception e) {
        string exceptionType = e.GetType().Name;
        switch (exceptionType) {
          case "FaultException":
            FaultException _e = e as FaultException;
            context.Response.StatusCode = 400;
            json_response = new { error = _e.Message };
            break;
          case "FaultException2":
            HandlerException _e2 = e as HandlerException;
            context.Response.StatusCode = (int)_e2.StatusCode;
            json_response = new { error = _e2.Message };
            break;
          case "TargetInvocationException":
            context.Response.StatusCode = 500;
            json_response = new {
              error = (e as System.Reflection.TargetInvocationException).InnerException.Message,
              type = exceptionType,
              trace = (e as System.Reflection.TargetInvocationException).InnerException.StackTrace
            };
            break;
          default:
            context.Response.StatusCode = 500;
            json_response = new {
              error = e.Message,
              type = exceptionType,
              trace = e.StackTrace
            };
            break;
        }

        Tracer.Logger.Error("IDPHandler.ProcessRequest " + e);
      }
      finally {
        Tracer.Logger.Info($" --- || End request || {ip} || path: {url_part} || --- {DateTime.Now.ToString()}     ---     <--");

        if (json_response != null) context.Reply(json_response);

        int cross_domain;
        if (WebConfigurationManager.AppSettings["cross_domain"] != null && int.TryParse(WebConfigurationManager.AppSettings["cross_domain"], out cross_domain) && Convert.ToBoolean(cross_domain))
          context.Response.AppendHeader("Access-Control-Allow-Origin", "*");

        context.Response.End();
      }
    }


    /// <summary>
    /// You will need to configure this handler in the Web.config file of your 
    /// web and register it with IIS before being able to use it. For more information
    /// see the following link: https://go.microsoft.com/?linkid=8101007
    /// </summary>
    static public bool RegisterWithinIIS(ConfigIIS config = null) {
      config.UpdateWebConfigFile();

      return true;
    }

    static public bool RegisterWithinIIS2(ConfigIIS config = null) {
      if (config == null) return false;
      config.UpdateWebConfigFile();
      return true;
    }

    string GetUrl(string url, string appPath) {
      if (appPath.Length > 1 && appPath.Trim() != string.Empty) {
        if (appPath.EndsWith("/")) appPath = appPath.Remove(appPath.Length - 1);
        int pos = url.IndexOf(appPath);
        if (pos >= 0) url = url.Substring(0, pos) + url.Substring(pos + appPath.Length);
        if (!url.StartsWith("/")) url = "/" + url;
      }
      if (url.Length > 1 && url.EndsWith("/")) url = url.Remove(url.Length - 1);
      return url;
    }

    Route[] PrepareNLoadServiceRoutes(IAuthentifiedService service) {
      var type = service.GetType();
      router = (
          from m in type.GetMethods()
          where m.IsPublic && m.DeclaringType == type
          select new Route(m, service)
      ).ToArray();
      return router;
    }

    /// <summary>
    /// Loads found service and returns it
    /// </summary>
    /// <returns></returns>
    IAuthentifiedService LoadService() {
      Utils.HandlerConfig config = null;
      try {
        config = WebConfigurationManager.GetSection("json4Rest") as Utils.HandlerConfig;
      }
      catch (Exception e) {
        throw new HandlerException($"Error with SimpleHandler's config section: {e.Message}");
      }
      if (config == null) throw new HandlerException("No Service config value found", System.Net.HttpStatusCode.NotImplemented);
      return Handler.service = Activator.CreateInstance(config.ServiceType) as IAuthentifiedService;
    }
  }

  /// <summary>
  /// Defines SimpleHandler behavior
  /// </summary>
  public enum HandlerState {
    StartPointIsIIS,
    Integrated,
    Undefined
  }
}