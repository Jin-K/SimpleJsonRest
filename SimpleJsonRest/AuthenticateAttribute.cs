
namespace SimpleJsonRest {
  public class AuthenticateAttribute : System.Attribute {
    internal bool IsConnected => System.Web.HttpContext.Current.Session["Logged"] != null && (bool) System.Web.HttpContext.Current.Session["Logged"];
  }
}