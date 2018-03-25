using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SimpleHandler.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace SimpleHandler.Routing {
  //internal delegate void Controller(HttpContext context, params object[] args);

  class Route {
    Regex _Reg;
    Delegate _Callback;

    string Name { get; set; }

    internal string Path { get; private set; }


    internal Route(string urlPart, Delegate delegateMethod) {
      //string name = method.Name;
      Path = urlPart;
      Name = delegateMethod.Method.DeclaringType.Name + "." + delegateMethod.Method.Name;

      _Reg = new Regex("^(" + urlPart.Replace("/", "\\/") + "\\/?(\\?[^\\/]*)?)$", RegexOptions.IgnoreCase);
      _Callback = delegateMethod;
    }

    internal Route(MethodInfo method, object target) {
      Path = "/" + method.Name;
      Name = method.DeclaringType.Name + "." + method.Name;
      _Reg = new Regex("^(" + Path + "\\/?(\\?[^\\/]*)?)$", RegexOptions.IgnoreCase);
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
        bool logIO = _Callback.Method.GetCustomAttribute(typeof(LogIOAttribute)) != null;
        if (logIO) Tracer.LogInput(_Callback.Method, _params);
        try {
          object ret = _Callback.Method.Invoke(_Callback.Target, _params);
          if (logIO) Tracer.LogOutput(_Callback.Method, ret, _params);
          return ret;
        }
        catch (TargetInvocationException e) {
          // TODO : Traitement érreur venant du service appelé
          Tracer.Logger.Error("SimpleHandler.Routing.Route.DeserializeAndInvoke", e);
          throw e.InnerException;
        }
      }
      catch (Exception e) {
        // TODO : Érreur dans la déserialisation ?
        Tracer.Logger.Error("SimpleHandler.Routing.Route.DeserializeAndInvoke", e);
        throw;
      }
    }

    dynamic[] PrepareParameters(ParameterInfo[] parameters) {
      dynamic[] parametersToSerialize = new dynamic[parameters.Length];
      dynamic obj;

      HttpContext.Current.Request.InputStream.Position = 0;
      using (StreamReader stream = new StreamReader(HttpContext.Current.Request.InputStream)) {
        string jsonString = Encoding.UTF8.GetString(Encoding.Default.GetBytes(stream.ReadToEnd()));
        while (Uri.UnescapeDataString(jsonString) != jsonString) jsonString = Uri.UnescapeDataString(jsonString);
        try {
          obj = JsonConvert.DeserializeObject<dynamic>(jsonString);
        }
        catch (JsonReaderException e) {
          Tracer.Logger.Error($"Ne sait pas déserializer le json entrant : {{{Environment.NewLine}{jsonString}{Environment.NewLine}}}", e);
          throw new HandlerException("Input stream isn't real json", System.Net.HttpStatusCode.BadRequest);
        }
      }

      if (obj != null) {
        var propertyNames = new List<string>();
        foreach (var p in obj.Properties()) propertyNames.Add(p.Name);
        for (var i = 0; i < parameters.Length; i++) {
          var param = parameters[i];
          Func<string, bool> findPropNameDelegate = jsonProp => jsonProp.ToLower() == param.Name.ToLower();
          if (!propertyNames.Any(findPropNameDelegate)) throw new HandlerException( $@"Json parameter ""{param.Name}"" not found" );
          parametersToSerialize[i] = Deserialize(obj[propertyNames.First(findPropNameDelegate)], param);
        }
      }

      if (parameters.Any(p => p.ParameterType == typeof(HttpContext))) {
        int index = -1;
        for (int c = 0; c < parameters.Length; c++) {
          var param = parameters[c];
          if (param.ParameterType.Name == "HttpContext") {
            index = c;
            break;
          }
        }
        parametersToSerialize[index] = HttpContext.Current;
      }

      return parametersToSerialize;
    }

    dynamic Deserialize(dynamic jProp, ParameterInfo param) {
      Type type = param.ParameterType;

      if (jProp.GetType() == typeof(JValue) && !IsNotCoreType(type))
        return jProp.Value;

      var returnObject = Activator.CreateInstance(type);
      foreach (var truk in jProp.Properties()) {
        PropertyInfo matchingProperty = type.GetProperty(truk.Name);
        if (matchingProperty != null)
          matchingProperty.SetValue(returnObject, Deserialize(truk.Value, matchingProperty));
      }

      return returnObject;
    }

    dynamic Deserialize(dynamic jProp, PropertyInfo prop) {
      Type type = prop.PropertyType.Name != "Nullable`1" ? prop.PropertyType : prop.PropertyType.GenericTypeArguments[0];

      if (jProp is JValue && !IsNotCoreType(type))
        switch (type.Name) {
          case "String": return jProp.Value.ToString();
          case "Int32": return Convert.ToInt32(jProp.Value);
          case "Int64": return Convert.ToInt64(jProp.Value);
          default: return jProp.Value;
        }
      else if (jProp is JArray) {
        if (type.Name == "List`1") {
          IList returnObject = Activator.CreateInstance(type) as IList;
          type = type.GenericTypeArguments[0];
          foreach (var inst in jProp) returnObject.Add(FillIn(type, inst));
          return returnObject;
        }
        else {
          type = type.GetElementType();
          Array returnObject = Array.CreateInstance(type, (jProp as JArray).Count);
          var i = 0;
          foreach (var inst in jProp) returnObject.SetValue(FillIn(type, inst), i++);
          return returnObject;
        }
      }
      else if (jProp is JObject)
        return FillIn(type, jProp);

      return null;
    }

    object FillIn(Type type, JObject jToken) {
      PropertyInfo matchingProperty;
      var returnObject = Activator.CreateInstance(type);
      foreach (var jProp in jToken.Properties())
        if ((matchingProperty = type.GetProperty(jProp.Name)) != null)
          matchingProperty.SetValue(returnObject, Deserialize(jProp.Value, matchingProperty));
      return Convert.ChangeType(returnObject, type);
    }

    bool IsNotCoreType(Type type) {
      return type != typeof(object) && Type.GetTypeCode(type) == TypeCode.Object;
    }
  }
}