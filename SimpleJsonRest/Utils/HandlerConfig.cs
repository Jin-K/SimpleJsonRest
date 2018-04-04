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
      /// Preloading target assembly in appDomain
      System.Reflection.Assembly.LoadFrom( AssemblyPath );

      /// Prepare domain assemblies and a list of types
      System.Reflection.Assembly[] domainAssemblies = AppDomain.CurrentDomain.GetAssemblies();
      System.Collections.Generic.List<Type> typesList = new System.Collections.Generic.List<Type>();

      /// Collect all types we find (bypassing unfounded)
      for (var c = 0; c < domainAssemblies.Length; c++) {
        System.Reflection.Assembly assembly = domainAssemblies[c];
        try {
          typesList.AddRange(assembly.GetTypes());
        }
        catch (System.Reflection.ReflectionTypeLoadException e) {
          typesList.AddRange(e.Types);
        }
      }

      /// return
      return typesList.ToArray();
    }
    #endregion

  }
}