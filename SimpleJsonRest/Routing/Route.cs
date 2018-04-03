using System;
using System.Linq;
using SimpleJsonRest.Utils;

namespace SimpleJsonRest.Routing {
  class Route {
    private System.Text.RegularExpressions.Regex _Reg;
    private Delegate _Callback;

    /// <summary>
    /// Name of the Route
    /// </summary>
    private string Name { get; set; }
    
    /// <summary>
    /// URL path able to trigger the route
    /// </summary>
    internal string Path { get; private set; }

    /// <summary>
    /// Register a route: with a callback method and an object target
    /// </summary>
    /// <param name="method"></param>
    /// <param name="target"></param>
    internal Route(System.Reflection.MethodInfo method, object target) {
      Path = "/" + method.Name;
      Name = method.DeclaringType.Name + "." + method.Name;
      _Reg = new System.Text.RegularExpressions.Regex("^(" + Path + "\\/?(\\?[^\\/]*)?)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
      _Callback = method.CreateDelegate(target);
    }

    /// <summary>
    /// Tests if an url part matches to this route's expecting path
    /// </summary>
    /// <param name="url_part"></param>
    /// <returns></returns>
    internal bool Check(string url_part) {
      return _Reg.Match(url_part).Success;
    }

    /// <summary>
    /// Executes the route callback method (Invocation via .NET reflection)
    /// </summary>
    /// <returns></returns>
    internal object Execute() {
      object[] attribs = _Callback.Method.GetCustomAttributes(typeof(AuthenticateAttribute), false);

      for (int i = 0; i < attribs.Length; i++) {
        var attrib = attribs[i] as AuthenticateAttribute;
        if (attrib != null && !attrib.IsConnected) return null; // handled in Handler.ProcessRequest finally block
      }

      // Invoke method correspondante, en déserialisant json entrant (s'il y en a) par rapport aux paramètres attendus
      return DeserializeAndInvoke();
    }


    // Private methods
    private object DeserializeAndInvoke() { // TODO Refactor invoking
      try {
        var _params = PrepareParameters(_Callback.Method.GetParameters());
        bool logIO = _Callback.Method.GetCustomAttribute<LogIOAttribute>() != null;
        if (logIO) Tracer.LogInput(_Callback.Method, _params);
        try {
          object ret = _Callback.Method.Invoke(_Callback.Target, _params);
          if (logIO) Tracer.LogOutput(_Callback.Method, ret, _params);
          return ret;
        }
        catch (System.Reflection.TargetInvocationException e) {
          // TODO : Traitement érreur venant du service appelé
          Tracer.Log("SimpleJsonRest.Routing.Route.DeserializeAndInvoke", e);
          throw e.InnerException;
        }
      }
      catch (Exception e) {
        // TODO : Érreur dans la déserialisation ?
        Tracer.Log("SimpleJsonRest.Routing.Route.DeserializeAndInvoke", e);
        throw;
      }
    }

    private dynamic[] PrepareParameters(System.Reflection.ParameterInfo[] parameters) {
      dynamic[] parametersToSerialize = new dynamic[parameters.Length];
      dynamic obj;

      System.Web.HttpContext.Current.Request.InputStream.Position = 0;
      using (System.IO.StreamReader stream = new System.IO.StreamReader(System.Web.HttpContext.Current.Request.InputStream)) {
        string jsonString = System.Text.Encoding.UTF8.GetString(System.Text.Encoding.Default.GetBytes(stream.ReadToEnd()));
        while (Uri.UnescapeDataString(jsonString) != jsonString) jsonString = Uri.UnescapeDataString(jsonString);
        try {
          obj = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(jsonString);
        }
        catch (Newtonsoft.Json.JsonReaderException e) {
          Tracer.Log($"Ne sait pas déserializer le json entrant : {{{Environment.NewLine}{jsonString}{Environment.NewLine}}}", e);
          throw new HandlerException("Input stream isn't real json", System.Net.HttpStatusCode.BadRequest);
        }
      }

      if (obj != null) {
        var propertyNames = new System.Collections.Generic.List<string>();
        foreach (var p in obj.Properties()) propertyNames.Add(p.Name);
        for (var i = 0; i < parameters.Length; i++) {
            var param = parameters[i];
            Func<string, bool> findPropNameDelegate = jsonProp => jsonProp.ToLower() == param.Name.ToLower();
            if (!propertyNames.Any(findPropNameDelegate)) throw new HandlerException($@"Json parameter ""{param.Name}"" not found"); // TODO Avoid using of LINQ
          parametersToSerialize[i] = Deserialize(obj[propertyNames.First(findPropNameDelegate)], param);
        }
      }

      if (parameters.Any(p => p.ParameterType == typeof(System.Web.HttpContext))) { // TODO Avoid LINQ
        int index = -1;
        for (int c = 0; c < parameters.Length; c++) {
          var param = parameters[c];
          if (param.ParameterType.Name == "HttpContext") {
            index = c;
            break;
          }
        }
        parametersToSerialize[index] = System.Web.HttpContext.Current;
      }

      return parametersToSerialize;
    }

    private dynamic Deserialize(dynamic jProp, System.Reflection.ParameterInfo param) {
      Type type = param.ParameterType;

      if (jProp.GetType() == typeof(Newtonsoft.Json.Linq.JValue) && !IsNotCoreType(type))
        return jProp.Value;

      // TODO Revoir performance instanciation (et utiliser conseils de vidéo de perf vue sur youtube: utilisant instructions CIL)
      var returnObject = Activator.CreateInstance(type);
      foreach (var truk in jProp.Properties()) {
        System.Reflection.PropertyInfo matchingProperty = type.GetProperty(truk.Name);
        if (matchingProperty != null)
          matchingProperty.SetValue(returnObject, Deserialize(truk.Value, matchingProperty), null); // TODO Penser aux différentes versions .NET ... (3eme paramètre)
      }

      return returnObject;
    }

    private dynamic Deserialize(dynamic jProp, System.Reflection.PropertyInfo prop) {
      Type type = prop.PropertyType.Name != "Nullable`1" ? prop.PropertyType : prop.PropertyType.GetGenericArguments()[0];

      if (jProp is Newtonsoft.Json.Linq.JValue && !IsNotCoreType(type))
        switch (type.Name) {
          case "String": return jProp.Value.ToString();
          case "Int32": return Convert.ToInt32(jProp.Value);
          case "Int64": return Convert.ToInt64(jProp.Value);
          default: return jProp.Value;
        }
      else if (jProp is Newtonsoft.Json.Linq.JArray) {
        if (type.Name == "List`1") {
          System.Collections.IList returnObject = Activator.CreateInstance(type) as System.Collections.IList;
          type = type.GetGenericArguments()[0];
          foreach (var inst in jProp) returnObject.Add(FillIn(type, inst));
          return returnObject;
        }
        else {
          type = type.GetElementType();
          Array returnObject = Array.CreateInstance(type, (jProp as Newtonsoft.Json.Linq.JArray).Count);
          var i = 0;
          foreach (var inst in jProp) returnObject.SetValue(FillIn(type, inst), i++);
          return returnObject;
        }
      }
      else if (jProp is Newtonsoft.Json.Linq.JObject)
        return FillIn(type, jProp);

      return null;
    }

    private object FillIn(Type type, Newtonsoft.Json.Linq.JObject jToken) {
      System.Reflection.PropertyInfo matchingProperty;
      var returnObject = Activator.CreateInstance(type); // TODO Revoir méthode instanciation ?
      foreach (var jProp in jToken.Properties())
        if ((matchingProperty = type.GetProperty(jProp.Name)) != null)
          matchingProperty.SetValue(returnObject, Deserialize(jProp.Value, matchingProperty), null);
      return Convert.ChangeType(returnObject, type);
    }

    private bool IsNotCoreType(Type type) {
      return type != typeof(object) && Type.GetTypeCode(type) == TypeCode.Object;
    }
  }
}