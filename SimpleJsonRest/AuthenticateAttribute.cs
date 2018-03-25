using System;
using System.Web;

namespace SimpleHandler {
    public class AuthenticateAttribute : Attribute {
        internal bool IsConnected {
            get {
                return HttpContext.Current.Session["Logged"] != null && (bool)HttpContext.Current.Session["Logged"];
            }
        }
    }
}