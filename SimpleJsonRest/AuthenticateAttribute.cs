using System;
using System.Web;

namespace SimpleJsonRest {
    public class AuthenticateAttribute : Attribute {
        internal bool IsConnected {
            get {
                return HttpContext.Current.Session["Logged"] != null && (bool)HttpContext.Current.Session["Logged"];
            }
        }
    }
}