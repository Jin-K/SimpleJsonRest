namespace SimpleJsonRest.Utils {
  public class ConfigIIS : IConfig {

    #region Constants
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

    internal const string DEFAULT_LOG_PATH = "Logs\\log.txt";
    #endregion

    #region Private members
    HandlerConfig innerConfig;
    int portToUse;
    string serverToUse, locationToUse;
    #endregion

    #region Constructors
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
    #endregion

    #region Accessible properties
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

    /// <summary>
    /// Location for IIS web folder (where /web.config and /bin/SimpleJsonRest.dll will be placed)
    /// </summary>
    public string Location {
      get { return locationToUse; }
      set { locationToUse = value; }
    }
    #endregion

    #region Methods
    void Construct(string server, int port) {
      if (port == DEFAULT_PORT_SSL) throw new System.Exception("Go fuck yourself with your SSL");
      innerConfig = new HandlerConfig();

      serverToUse = server;
      portToUse = port;

      // get location of caller
      System.Reflection.MethodBase callingMethod = new System.Diagnostics.StackTrace().GetFrame(2).GetMethod();
      innerConfig.AssemblyPath = callingMethod.DeclaringType.Assembly.CodeBase;
    }

    internal bool UpdateWebConfigFile() {
      throw new System.NotImplementedException("Sorry");
    }

    /// <summary>
    /// Create website folder and register website in IIS for this config
    /// </summary>
    /// <returns></returns>
    public bool RegisterInIIS() {
      return RegisterInIIS(this);
    }
    
    /// <summary>
    /// Create website folder and register website in IIS with specified values (check SimpleJsonRest.Utils.ConfigIIS's accessible properties)
    /// Default values for "server" and "port" are respectively "localhost" and 80
    /// </summary>
    /// <returns></returns>
    static public bool RegisterInIIS(string service, string endpoint, string location, string logPath = ConfigIIS.DEFAULT_LOG_PATH) {
      return RegisterInIIS(ConfigIIS.DEFAULT_SERVER, ConfigIIS.DEFAULT_PORT, service, endpoint, location, logPath);
    }
    
    /// <summary>
    /// Create website folder and register website in IIS with specified values (check SimpleJsonRest.Utils.ConfigIIS's accessible properties)
    /// </summary>
    /// <returns></returns>
    static public bool RegisterInIIS(string server, int port, string service, string endpoint, string location, string logPath = ConfigIIS.DEFAULT_LOG_PATH) {
      var config = new ConfigIIS(server, port) {
        Service = service,
        EndPoint = endpoint,
        Location = location,
        LogPath = logPath
      };
      return RegisterInIIS(config);
    }

    /// <summary>
    /// Create website folder and register website in IIS with specified config
    /// </summary>
    /// <returns></returns>
    static public bool RegisterInIIS(ConfigIIS config) {
      if (config.LogPath == null) config.LogPath = DEFAULT_LOG_PATH;
      Utils.Tracer.SetupLog4Net( config.LogPath ); // test

      var ret = false;
      var destFolder = config.Location;
      
      if (!CheckCreate( destFolder, out string error)) return false;
      if (!CreateWebConfigFile($"{destFolder}\\web.config") ) return false;

      
      destFolder += "\\bin";
      
      if (!CheckCreate( destFolder, out error )) return false;
      var uriBuilder = new System.UriBuilder(System.Reflection.Assembly.GetExecutingAssembly().CodeBase);
      var uriPath = System.Uri.UnescapeDataString(uriBuilder.Path);
      var dllPath = $"{System.IO.Path.GetDirectoryName(uriPath)}\\SimpleJsonRest.dll";
      var destFile = $"{destFolder}\\SimpleJsonRest.dll";
      try {
        System.IO.File.Copy(dllPath, destFile);
      }
      catch (System.UnauthorizedAccessException) {
        Tracer.Log( $"No access to write \"{destFile}\"", MessageVerbosity.Error );
        return false;
      }

      return ret && config.UpdateWebConfigFile();
    }

    static bool CheckCreate(string directoryPath, out string errorMessage) {
      errorMessage = "";

      System.IO.DirectoryInfo directoryToCheck = System.IO.Directory.GetParent( directoryPath );
      if (!directoryToCheck.Exists) {
        errorMessage = "Parent folder doesn't exist.";
        return false;
      }

      if (!System.IO.Directory.Exists(directoryPath))
        try {
          System.IO.Directory.CreateDirectory(directoryPath);
        }
        catch (System.UnauthorizedAccessException e) {
          errorMessage = e.Message;
          Tracer.Log( $@"Exception in ConfigIIS.CheckCreate for this folder: ""{directoryPath}""", e );
          return false;
        }

      return true;
    }

    static bool CreateWebConfigFile( string destinationFile ) {
      // TODO: create xml structure and write stream to file, or use a standard web.config stored somewhere
      return true;
    }
    #endregion

  }
}