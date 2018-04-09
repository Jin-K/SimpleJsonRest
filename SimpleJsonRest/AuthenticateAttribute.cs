namespace SimpleJsonRest {
  /// <summary>
  /// Use this attribute to
  /// </summary>
  public class RequireAuthenticateAttribute : System.Attribute {
    internal bool IsConnected => System.Web.HttpContext.Current.Session["Logged"] != null && (bool) System.Web.HttpContext.Current.Session["Logged"];
  }
}