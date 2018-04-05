using System.Linq;

namespace SimpleJsonRest.Utils {

  static class Extensions {

    /// <summary>
    /// Returns object as a json string
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    internal static string JsonSerialize(this object source) {
      string ret;

      switch (source.GetType().Name) {
        case "String":
          ret = Newtonsoft.Json.JsonConvert.SerializeObject(new { response = source });
          break;
        default:
          ret = Newtonsoft.Json.JsonConvert.SerializeObject(source);
          break;
      }

      return ret;
    }

    /// <summary>
    /// Responds (HTTP) with a json representation of given data object
    /// </summary>
    /// <param name="source"></param>
    /// <param name="data"></param>
    internal static void Reply(this System.Web.HttpContext source, object data) {
      source.Response.Write(data.JsonSerialize());
    }

    internal static void Reject(this System.Web.HttpContext source) {
      source.Response.StatusCode = 401;
      source.Response.StatusDescription = "Unauthorized";
      source.Response.Write(new { error = "You're not connected." }.JsonSerialize());
    }

    /// <summary>
    /// Creates an appropriate delegate from a method with an object target
    /// </summary>
    /// <param name="methodInfo"></param>
    /// <param name="target"></param>
    /// <returns></returns>
    internal static System.Delegate CreateDelegate(this System.Reflection.MethodInfo methodInfo, object target) {
      System.Func<System.Type[], System.Type> getType;
      var isAction = methodInfo.ReturnType.Equals((typeof(void)));
      var types = methodInfo.GetParameters().Select(p => p.ParameterType);

      if (isAction)
        getType = System.Linq.Expressions.Expression.GetActionType;
      else {
        getType = System.Linq.Expressions.Expression.GetFuncType;
        types = types.Concat(new[] { methodInfo.ReturnType });
      }

      if (methodInfo.IsStatic) return System.Delegate.CreateDelegate(getType(types.ToArray()), methodInfo);

      return System.Delegate.CreateDelegate(getType(types.ToArray()), target, methodInfo.Name);
    }

    /// <summary>
    /// Interessant extension method to check if we can create a folder before trying to create it.
    /// But may cause another type of exception (PrivilegeNotHeldException) before creating the folder.
    /// And I think that it takes more time to check access/privileges than just trying to create the folder and catch an Exception if we can't
    /// </summary>
    /// <param name="directory"></param>
    /// <param name="errorMessage"></param>
    /// <returns></returns>
    [System.Obsolete]
    internal static bool CheckWriteAccessAndCreate(this System.IO.DirectoryInfo directory, out string errorMessage) {
      System.IO.DirectoryInfo directoryToCheck = directory.Parent;
      if (!directoryToCheck.Exists) {
        errorMessage = "Parent folder doesn't exist.";
        return false;
      }

      errorMessage = "";

      string whoIsTrying = System.Security.Principal.WindowsIdentity.GetCurrent().Name;

      // Gets all access security types for directory
      System.Security.AccessControl.DirectorySecurity security;
      try {
        security = directoryToCheck.GetAccessControl( System.Security.AccessControl.AccessControlSections.All );
      }
      catch (System.Security.AccessControl.PrivilegeNotHeldException e) {
        errorMessage = e.Message;
        Tracer.Log( $"\"SeSecurityPrivilege\" error: {whoIsTrying} doesn't have privileges to check folder's security ({directoryToCheck})", e );
        return false;
      }

      // Collects all access rules for that security types
      System.Security.AccessControl.AuthorizationRuleCollection rules = security.GetAccessRules( true, true, typeof( System.Security.Principal.NTAccount ) );

      // Iterate each access rule
      for (var i = 0; i < rules.Count; i++) {
        var rule = rules[i];

        // Do we match the identity ?
        if (rule.IdentityReference.Value.Equals( whoIsTrying, System.StringComparison.CurrentCultureIgnoreCase)) {
          var fsAccessRule = rule as System.Security.AccessControl.FileSystemAccessRule; // cast to check 4 access rights
          var hasAccessOrNotBordelDeMerde = ( fsAccessRule.FileSystemRights & System.Security.AccessControl.FileSystemRights.WriteData ) > 0 
                                            && fsAccessRule.AccessControlType != System.Security.AccessControl.AccessControlType.Deny;

          if (!hasAccessOrNotBordelDeMerde) {
            errorMessage = $"\"{whoIsTrying}\" does not have write access to {directoryToCheck}";
            return false;
          }

          try {
            directory.Create();
          }
          catch (System.Exception e) {
            errorMessage = e.Message;
            Tracer.Log( $@"Exception in Config.CheckCreate for this folder: ""{directory.ToString()}""", e );
            return false;
          }
        }
      }

      return true;
    }

    // Depuis que j'ai passé le truk à .NET framework 4 au lieu de .NET Framework 4.5
    // Plusieurs fonctions n'étaient plus définies
    // TODO: Voir s'il y a moyen de compiler (à l'avenir) pour plusieurs version du framework avec les statements #if NET20, NET30, etc
    public static T GetCustomAttribute<T>(this System.Reflection.MethodInfo method) where T : System.Attribute {
      object[] attributes = method.GetCustomAttributes(false);
      return attributes.OfType<T>().FirstOrDefault();
    }
  }
}