using System;
using System.Linq;
using SimpleJsonRest.Utils;

namespace SimpleJsonRest.Routing {
  class Route {
    System.Text.RegularExpressions.Regex _Reg;
    Delegate _Callback;

    string Name { get; set; }

    internal string Path { get; private set; }


    internal Route(string urlPart, Delegate delegateMethod) {
      Path = urlPart;
      Name = delegateMethod.Method.DeclaringType.Name + "." + delegateMethod.Method.Name;

      _Reg = new System.Text.RegularExpressions.Regex("^(" + urlPart.Replace("/", "\\/") + "\\/?(\\?[^\\/]*)?)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
      _Callback = delegateMethod;
    }

    internal Route(System.Reflection.MethodInfo method, object target) {
      Path = "/" + method.Name;
      Name = method.DeclaringType.Name + "." + method.Name;
      _Reg = new System.Text.RegularExpressions.Regex("^(" + Path + "\\/?(\\?[^\\/]*)?)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
      _Callback = method.CreateDelegate(target);
    }

    internal bool Check(string url_part) {
      return _Reg.Match(url_part).Success;
    }

    internal object Execute() {
      object[] attribs = _Callback.Method.GetCustomAttributes(typeof(AuthenticateAttribute), false);

      if (attribs.Length >= 1 && attribs.Any(a => a.GetType() == typeof(AuthenticateAttribute)) && !(attribs.First(a => a.GetType() == typeof(AuthenticateAttribute)) as AuthenticateAttribute).IsConnected) {
        // TODO : Reject 401
        return null;
      }

      // Invoke method correspondante, en déserialisant json entrant (s'il y en a) par rapport aux paramètres attendus
      return DeserializeAndInvoke();
    }


    // Private methods
    object DeserializeAndInvoke() {
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
          Tracer.Logger.Error("SimpleJsonRest.Routing.Route.DeserializeAndInvoke", e);
          throw e.InnerException;
        }
      }
      catch (Exception e) {
        // TODO : Érreur dans la déserialisation ?
        Tracer.Logger.Error("SimpleJsonRest.Routing.Route.DeserializeAndInvoke", e);
        throw;
      }
    }

    dynamic[] PrepareParameters(System.Reflection.ParameterInfo[] parameters) {
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
          Tracer.Logger.Error($"Ne sait pas déserializer le json entrant : {{{Environment.NewLine}{jsonString}{Environment.NewLine}}}", e);
          throw new HandlerException("Input stream isn't real json", System.Net.HttpStatusCode.BadRequest);
        }
      }

      if (obj != null) {
        var propertyNames = new System.Collections.Generic.List<string>();
        foreach (var p in obj.Properties()) propertyNames.Add(p.Name);
        for (var i = 0; i < parameters.Length; i++) {
            var param = parameters[i];
            Func<string, bool> findPropNameDelegate = jsonProp => jsonProp.ToLower() == param.Name.ToLower();
            if (!propertyNames.Any(findPropNameDelegate)) throw new HandlerException($@"Json parameter ""{param.Name}"" not found");
          parametersToSerialize[i] = Deserialize(obj[propertyNames.First(findPropNameDelegate)], param);
        }
      }

      if (parameters.Any(p => p.ParameterType == typeof(System.Web.HttpContext))) {
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

    dynamic Deserialize(dynamic jProp, System.Reflection.ParameterInfo param) {
      Type type = param.ParameterType;

      if (jProp.GetType() == typeof(Newtonsoft.Json.Linq.JValue) && !IsNotCoreType(type))
        return jProp.Value;

      var returnObject = Activator.CreateInstance(type);
      foreach (var truk in jProp.Properties()) {
        System.Reflection.PropertyInfo matchingProperty = type.GetProperty(truk.Name);
        if (matchingProperty != null)
          matchingProperty.SetValue(returnObject, Deserialize(truk.Value, matchingProperty), null);
      }

      return returnObject;
    }

    dynamic Deserialize(dynamic jProp, System.Reflection.PropertyInfo prop) {
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

    object FillIn(Type type, Newtonsoft.Json.Linq.JObject jToken) {
      System.Reflection.PropertyInfo matchingProperty;
      var returnObject = Activator.CreateInstance(type);
      foreach (var jProp in jToken.Properties())
        if ((matchingProperty = type.GetProperty(jProp.Name)) != null)
          matchingProperty.SetValue(returnObject, Deserialize(jProp.Value, matchingProperty), null);
      return Convert.ChangeType(returnObject, type);
    }

    bool IsNotCoreType(Type type) {
      return type != typeof(object) && Type.GetTypeCode(type) == TypeCode.Object;
    }
  }
}