using Newtonsoft.Json;
using SimpleJsonRest.Routing;
using SimpleJsonRest.Utils;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Web;
using System.Web.Configuration;
using System.Web.SessionState;

namespace SimpleJsonRest {
    public class Handler : IHttpHandler, IRequiresSessionState {
        /// <summary>
        /// You will need to configure this handler in the Web.config file of your 
        /// web and register it with IIS before being able to use it. For more information
        /// see the following link: https://go.microsoft.com/?linkid=8101007
        /// </summary>

        // IHttpHandler Members
        #region IHttpHandler Members

        //internal static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        static IAuthentifiedService service;
        IAuthentifiedService Service {
            get {
                if (service == null) {
                    Config config = null;
                    try {
                        config = WebConfigurationManager.GetSection("json4Rest") as Config;
                    }
                    catch (Exception e) {
                        throw new HandlerException($"Error with SimpleJsonRest's config section: {e.Message}");
                    }
                    if (config == null) throw new HandlerException("No Service config value found", System.Net.HttpStatusCode.NotImplemented);
                    service = Activator.CreateInstance(config.ServiceType) as IAuthentifiedService;
                }

                return service;
            }
        }

        static Route[] router;
        Route[] Router {
            get {
                if (router == null) {
                    var service = Service; // Afin de n'appeler le getter qu'une fois
                    var type = service.GetType();
                    router = (
                        from m in type.GetMethods()
                        where m.IsPublic && m.DeclaringType == type
                        select new Route(m, service)
                    ).ToArray();
                }

                return router;
            }
        }

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
                switch (e.GetType().Name) {
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
                    default:
                        context.Response.StatusCode = 500;
                        json_response = new { error = e.Message };
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
        #endregion

        // Private Methods
        #region Private Methods
        string GetUrl(string rawUrl, string applicationPath) {
            if (applicationPath.Length > 1 && applicationPath.Trim() != string.Empty) {
                if (applicationPath.EndsWith("/")) applicationPath = applicationPath.Remove(applicationPath.Length - 1);
                int pos = rawUrl.IndexOf(applicationPath);
                if (pos >= 0) rawUrl = rawUrl.Substring(0, pos) + rawUrl.Substring(pos + applicationPath.Length);
                if (!rawUrl.StartsWith("/")) rawUrl = "/" + rawUrl;
            }
            if (rawUrl.Length > 1 && rawUrl.EndsWith("/")) rawUrl = rawUrl.Remove(rawUrl.Length - 1);
            return rawUrl;
        }
        #endregion
    }

    static class Extensions {
        internal static string JsonSerialize(this object source) {
            string ret;

            switch (source.GetType().Name) {
                case "String":
                    ret = JsonConvert.SerializeObject(new { response = source });
                    break;
                default:
                    ret = JsonConvert.SerializeObject(source);
                    break;
            }

            return ret;
        }

        internal static void Reply(this HttpContext source, object data) {
            source.Response.Write(data.JsonSerialize());
        }

        internal static void Reject(this HttpContext source) {
            source.Response.StatusCode = 401;
            source.Response.StatusDescription = "Unauthorized";
            source.Response.Write(new { error = "You're not connected." }.JsonSerialize());
        }

        internal static Delegate CreateDelegate(this MethodInfo methodInfo, object target) {
            Func<Type[], Type> getType;
            var isAction = methodInfo.ReturnType.Equals((typeof(void)));
            var types = methodInfo.GetParameters().Select(p => p.ParameterType);

            if (isAction)
                getType = Expression.GetActionType;
            else {
                getType = Expression.GetFuncType;
                types = types.Concat(new[] { methodInfo.ReturnType });
            }

            if (methodInfo.IsStatic) return Delegate.CreateDelegate(getType(types.ToArray()), methodInfo);

            return Delegate.CreateDelegate(getType(types.ToArray()), target, methodInfo.Name);
        }
    }
}