namespace SimpleJsonRest.Utils {
  public class ConfigIIS : IConfig {
    /// <summary>
    /// Default port for HTTP
    /// </summary>
    public const short DEFAULT_PORT = 80;

    /// <summary>
    /// Default port for HTTPS
    /// </summary>
    public const short DEFAULT_PORT_SSL = 443;

    /// <summary>
    /// Default server for test
    /// </summary>
    public const string DEFAULT_SERVER = "localhost";

    HandlerConfig innerConfig;

    int portToUse;
    string serverToUse;

    /// <summary>
    /// Create a new config for IIS
    /// </summary>
    /// <param name="server"></param>
    /// <param name="port"></param>
    public ConfigIIS(string server = DEFAULT_SERVER, int port = DEFAULT_PORT) {
      Construct(server, port);
    }

    /// <summary>
    /// Create a new config for IIS
    /// </summary>
    /// <param name="port"></param>
    /// <param name="server"></param>
    public ConfigIIS(int port, string server = DEFAULT_SERVER) {
      Construct(server, port);
    }


    /// <summary>
    /// Full path of the assembly (.dll or .exe) containing the specified service
    /// </summary>
    public string AssemblyPath => innerConfig.AssemblyPath;

    /// <summary>
    /// Port that the IIS site will use.
    /// </summary>
    public int Port => portToUse;

    /// <summary>
    /// Server on which IIS will host
    /// </summary>
    public string Server => serverToUse;

    /// <summary>
    /// FullName of the service's type (ex: YourNamespace.ServiceClassName)
    /// </summary>
    public string Service {
      get { return innerConfig.Service; }
      set { innerConfig.Service = value; }
    }

    /// <summary>
    /// Location for log file
    /// </summary>
    public string LogPath {
      get { return innerConfig.LogPath; }
      set { innerConfig.LogPath = value; }
    }

    /// <summary>
    /// Endpoint to use for the mapping in IIS, (ex: "/my/path" in "server:port/my/path")
    /// </summary>
    public string EndPoint {
      get { return innerConfig.EndPoint; }
      set { innerConfig.EndPoint = value.StartsWith("/") ? value : "/" + value; }
    }

    void Construct(string server, int port) {
      if (port == DEFAULT_PORT_SSL) throw new System.Exception("Go fuck yourself with your SSL");
      innerConfig = new HandlerConfig();

      serverToUse = server;
      portToUse = port;

      // get location of caller
      System.Reflection.MethodBase callingMethod = new System.Diagnostics.StackTrace().GetFrame(2).GetMethod();
      innerConfig.AssemblyPath = callingMethod.DeclaringType.Assembly.CodeBase;
    }

    internal void UpdateWebConfigFile() {
      throw new System.Exception("Not implemented yet!");
      var assembly = System.Reflection.Assembly.LoadFrom(AssemblyPath);
      if (assembly != null) {
        var registrationServices = new System.Runtime.InteropServices.RegistrationServices();
        var result = registrationServices.RegisterAssembly(assembly, System.Runtime.InteropServices.AssemblyRegistrationFlags.SetCodeBase);
      }
    }
  }
}