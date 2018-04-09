using System;
using SimpleJsonRest.Utils;

namespace SimpleJsonRest {
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
      Name = method.DeclaringType != null ? method.DeclaringType.Name + "." + method.Name : method.Name;
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
      object[] attribs = _Callback.Method.GetCustomAttributes(typeof(RequireAuthenticateAttribute), false);

      for (int i = 0; i < attribs.Length; i++)
        if (attribs[i] is RequireAuthenticateAttribute attrib && !attrib.IsConnected)
          return null; // handled in Handler.ProcessRequest finally block

      // Invoke method correspondante, en déserialisant json entrant (s'il y en a) par rapport aux paramètres attendus
      return DeserializeAndInvoke();
    }


    #region Private Methods
    private object DeserializeAndInvoke() { // TODO Refactoring
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
          Tracer.Log(e);
          throw e.InnerException ?? e;
        }
      }
      catch (Exception e) {
        // TODO : Érreur dans la déserialisation ?
        Tracer.Log(e);
        throw;
      }
    }

    private dynamic[] PrepareParameters(System.Reflection.ParameterInfo[] parameters) {
      dynamic[] parametersToSerialize = new dynamic[parameters.Length];
      dynamic obj;

      // Read incoming stream (expecting json string) and convert it to dynamic object
      System.Web.HttpContext.Current.Request.InputStream.Position = 0;
      using (var stream = new System.IO.StreamReader(System.Web.HttpContext.Current.Request.InputStream)) {
        // Read & unescape
        string unescaped, jsonString = System.Text.Encoding.UTF8.GetString(System.Text.Encoding.Default.GetBytes(stream.ReadToEnd()));
        while (( unescaped = Uri.UnescapeDataString(jsonString)) != jsonString) jsonString = unescaped;

        // Letting Json.NET trying to deserialize it
        try {
          obj = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(jsonString);
        }
        catch (Newtonsoft.Json.JsonReaderException e) {
          Tracer.Log($"Ne sait pas déserializer le json entrant : {{{Environment.NewLine}{jsonString}{Environment.NewLine}}}", e);
          throw new HandlerException("Input stream isn't real json", System.Net.HttpStatusCode.BadRequest);
        }
      }

      // TODO Use a kind of hashset to check parameters, avoiding multiple loops like now ...
      if (obj != null) {
        // Collect all properties of dynamic object
        var propertyNames = new System.Collections.Generic.List<string>();
        foreach (var p in obj.Properties()) propertyNames.Add( p.Name );

        // Iterate over parameters received as argument (required input parameters of method to call)
        for (var i = 0; i < parameters.Length; i++) {
          var param = parameters[i];

          // Check for each required param if a collected (above) json parameter exists matching name
          var match = "";
          for (var j = 0; j < propertyNames.Count; j++)
            if (param.Name.Equals( propertyNames[j], StringComparison.InvariantCultureIgnoreCase )) {
              match = propertyNames[j];
              break;
            }
          if (match == string.Empty) throw new HandlerException( $@"Json parameter ""{param.Name}"" not found" );

          // Dynamically deserialize json parameter and assigned to corresponding method input parameter
          parametersToSerialize[i] = DeserializeParameter( obj[match], param );
        }
      }
      
      // If one of the method parameters is of type HttpContext ... (special case)
      for(var c = 0; c < parameters.Length; c++) {
        var param = parameters[c];
        if (param.ParameterType == typeof(System.Web.HttpContext)) {
          parametersToSerialize[c] = System.Web.HttpContext.Current;
          break;
        }
      }

      return parametersToSerialize;
    }

    private dynamic DeserializeParameter(dynamic jProp, System.Reflection.ParameterInfo param) {
      var type = param.ParameterType;

      if (jProp.GetType() == typeof(Newtonsoft.Json.Linq.JValue) && !IsNotCoreType(type))
        return jProp.Value;

      // TODO Revoir performance instanciation (et utiliser conseils de vidéo de perf vue sur youtube: utilisant instructions CIL)
      var returnObject = Activator.CreateInstance(type);
      foreach (var truk in jProp.Properties()) {
        System.Reflection.PropertyInfo matchingProperty = type.GetProperty(truk.Name);
        if (matchingProperty != null)
          matchingProperty.SetValue(returnObject, DeserializeProperty( truk.Value, matchingProperty), null); // TODO Penser aux différentes versions .NET ... (3eme paramètre)
      }

      return returnObject;
    }

    private dynamic DeserializeProperty(dynamic jProp, System.Reflection.PropertyInfo prop) {
      var type = prop.PropertyType.Name != "Nullable`1" ? prop.PropertyType : prop.PropertyType.GetGenericArguments()[0];

      if (jProp is Newtonsoft.Json.Linq.JValue && !IsNotCoreType(type))
        switch (type.Name) {
          case "String": return jProp.Value.ToString();
          case "Int32": return Convert.ToInt32(jProp.Value);
          case "Int64": return Convert.ToInt64(jProp.Value);
          default: return jProp.Value;
        }
      if (jProp is Newtonsoft.Json.Linq.JArray) {
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
      if (jProp is Newtonsoft.Json.Linq.JObject)
        return FillIn(type, jProp);

      return null;
    }

    private object FillIn(Type type, Newtonsoft.Json.Linq.JObject jToken) {
      System.Reflection.PropertyInfo matchingProperty;
      var returnObject = Activator.CreateInstance(type); // TODO Revoir méthode instanciation ? Clairement !
      foreach (var jProp in jToken.Properties())
        if ((matchingProperty = type.GetProperty(jProp.Name)) != null)
          matchingProperty.SetValue(returnObject, DeserializeProperty( jProp.Value, matchingProperty), null);
      return Convert.ChangeType(returnObject, type);
    }

    private bool IsNotCoreType(Type type) {
      return type != typeof(object) && Type.GetTypeCode(type) == TypeCode.Object;
    }
    #endregion
  }
}
