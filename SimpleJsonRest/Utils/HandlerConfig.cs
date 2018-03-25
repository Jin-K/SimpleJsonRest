using System;
using System.Configuration;

namespace SimpleJsonRest.Utils {
  internal class HandlerConfig : ConfigurationSection, IConfig {

    #region Config properties
    [ConfigurationProperty("assembly", IsRequired = false)]
    public string AssemblyPath {
      get { return (string)this["assembly"]; }
      set { this["assembly"] = value; }
    }

    [ConfigurationProperty("service", IsRequired = true)]
    public string Service {
      get { return (string)this["service"]; }
      set { this["service"] = value; }
    }

    [ConfigurationProperty("logPath", IsRequired = true)]
    public string LogPath {
      get { return (string)this["logPath"]; }
      set { this["logPath"] = value; }
    }

    [ConfigurationProperty("endpoint", IsRequired = false)]
    public string EndPoint {
      get { return (string)this["endpoint"]; }
      set { this["endpoint"] = value; }
    }
    #endregion

    #region Private members
    /// <summary>
    /// Service type object supposed to be found
    /// </summary>
    Type serviceType;
    #endregion

    #region Properties
    /// <summary>
    /// Found service type
    /// </summary>
    internal Type ServiceType => serviceType ?? SearchServiceType();
    #endregion

    #region Private methods
    Type SearchServiceType() {
      var zenginsAssembly = System.Reflection.Assembly.LoadFrom(AssemblyPath);
      var types = CollecTypes();
      for (var c = 0; c < types.Length; c++) {
        var type = types[c];
        if (type != null && type.FullName == Service) {
          if (typeof(IAuthentifiedService).IsAssignableFrom(type) && !type.IsInterface)
            serviceType = type;
          break;
        }
      }
      if (serviceType == null) throw new HandlerException($"Type {Service} not found");
      return serviceType;
    }

    Type[] CollecTypes() {
      var domainAssemblies = AppDomain.CurrentDomain.GetAssemblies();
      var typesList = new System.Collections.Generic.List<Type>();

      for (var c = 0; c < domainAssemblies.Length; c++) {
        System.Reflection.Assembly assembly = domainAssemblies[c];
        try {
          typesList.AddRange(assembly.GetTypes());
        }
        catch (System.Reflection.ReflectionTypeLoadException e) {
          typesList.AddRange(e.Types);
        }
      }

      return typesList.ToArray();
    }
    #endregion

  }
}