using System;
using System.Linq;
using System.Web;

namespace SimpleHandler.Utils {


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
    internal static void Reply(this HttpContext source, object data) {
      source.Response.Write(data.JsonSerialize());
    }

    internal static void Reject(this HttpContext source) {
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
    internal static Delegate CreateDelegate(this System.Reflection.MethodInfo methodInfo, object target) {
      Func<Type[], Type> getType;
      var isAction = methodInfo.ReturnType.Equals((typeof(void)));
      var types = methodInfo.GetParameters().Select(p => p.ParameterType);

      if (isAction)
        getType = System.Linq.Expressions.Expression.GetActionType;
      else {
        getType = System.Linq.Expressions.Expression.GetFuncType;
        types = types.Concat(new[] { methodInfo.ReturnType });
      }

      if (methodInfo.IsStatic) return Delegate.CreateDelegate(getType(types.ToArray()), methodInfo);

      return Delegate.CreateDelegate(getType(types.ToArray()), target, methodInfo.Name);
    }
  }
}