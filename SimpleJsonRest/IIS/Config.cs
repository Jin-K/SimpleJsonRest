﻿namespace SimpleJsonRest.IIS {
  public class Config : Core.IConfig {

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
    private const string DEFAULT_APPLICATION_POOL = "DefaultAppPool";
    #endregion

    #region Private members
    private Utils.HandlerConfig _innerConfig;
    private int _portToUse;
    private string _serverToUse;
    #endregion

    #region Constructors
    /// <summary>
    /// Create a new config for IIS
    /// </summary>
    /// <param name="server"></param>
    /// <param name="port"></param>
    public Config(string server = DEFAULT_SERVER, int port = DEFAULT_PORT) {
      Construct(server, port);
    }

    /// <summary>
    /// Create a new config for IIS
    /// </summary>
    /// <param name="port"></param>
    /// <param name="server"></param>
    public Config(int port, string server = DEFAULT_SERVER) {
      Construct(server, port);
    }
    #endregion

    #region Accessible properties
    /// <summary>
    /// Full path of the assembly (.dll or .exe) containing the specified service
    /// </summary>
    public string AssemblyPath => _innerConfig.AssemblyPath;

    /// <summary>
    /// Port that the IIS site will use.
    /// </summary>
    public int Port => _portToUse;

    /// <summary>
    /// Server on which IIS will host
    /// </summary>
    public string Server => _serverToUse;

    /// <summary>
    /// FullName of the service's type (ex: YourNamespace.ServiceClassName)
    /// </summary>
    public string Service {
      get => _innerConfig.Service;
      set => _innerConfig.Service = value;
    }

    /// <summary>
    /// Location for log file
    /// </summary>
    public string LogPath {
      get => _innerConfig.LogPath;
      set => _innerConfig.LogPath = value;
    }

    /// <summary>
    /// Endpoint to use for naming the site's entry in IIS, (By default, IIS has the following site entry: "Default Web Site")
    /// </summary>
    public string SiteName {
      get => _innerConfig.EndPoint;
      set => _innerConfig.EndPoint = value;
    }

    /// <summary>
    /// Location for IIS web folder (where /web.config and /bin/SimpleJsonRest.dll will be placed)
    /// </summary>
    public string Location { get; set; }

    public ServerAdmin? Credentials { get; set; }
    #endregion

    #region Methods
    private void Construct(string server, int port) {
      if (port == DEFAULT_PORT_SSL) throw new System.NotSupportedException("This port is reserved for SSL, which is still not supported.");
      _innerConfig = new Utils.HandlerConfig();

      _serverToUse = server;
      _portToUse = port;

      // get location of caller
      var callingMethod = new System.Diagnostics.StackTrace().GetFrame(2).GetMethod();
      _innerConfig.AssemblyPath = callingMethod.DeclaringType?.Assembly.Location;
    }

    /// <summary>
    /// Create website folder and register website in IIS for this config.
    /// Set up a value for Credentials property if you want the library to auto-register a web site entry in IIS, 
    /// otherwise only the target folder for IIS will be created with minimum required files
    /// </summary>
    /// <returns></returns>
    public bool RegisterInIIS() {
      return RegisterInIIS(this);
    }

    /// <summary>
    /// Create website folder and register website in IIS with specified values (check SimpleJsonRest.Utils.Config's accessible properties)
    /// Default values for "server" and "port" are respectively "localhost" and 80.
    /// Set up a value for Credentials property if you want the library to auto-register a web site entry in IIS, 
    /// otherwise only the target folder for IIS will be created with minimum required files
    /// </summary>
    /// <returns></returns>
    public static bool RegisterInIIS(string service, string endpoint, string location, string logPath = DEFAULT_LOG_PATH) {
      return RegisterInIIS(DEFAULT_SERVER, DEFAULT_PORT, service, endpoint, location, logPath);
    }

    /// <summary>
    /// Create website folder and register website in IIS with specified values (check SimpleJsonRest.Utils.Config's accessible properties).
    /// Set up a value for Credentials property if you want the library to auto-register a web site entry in IIS, 
    /// otherwise only the target folder for IIS will be created with minimum required files
    /// </summary>
    /// <returns></returns>
    public static bool RegisterInIIS(string server, int port, string service, string endpoint, string location, string logPath = DEFAULT_LOG_PATH) {
      var config = new Config(server, port) {
        Service = service,
        SiteName = endpoint,
        Location = location,
        LogPath = logPath
      };
      return RegisterInIIS(config);
    }

    /// <summary>
    /// Creates website folder and register website in IIS with specified config.
    /// Set up a value for Credentials property if you want the library to auto-register a web site entry in IIS, 
    /// otherwise only the target folder for IIS will be created with minimum required files
    /// </summary>
    /// <returns></returns>
    public static bool RegisterInIIS(Config config) {
      if (config.LogPath == null) config.LogPath = DEFAULT_LOG_PATH;
      //Utils.Tracer.SetupLog4Net( config.LogPath );
      Utils.Tracer.SetupLogger( System.IO.Path.Combine(config.Location, config.LogPath) );

      var destFolder = config.Location;
      if (!Utils.Extensions.CheckCreate( destFolder, out string _)) return false;
      if (!config.CreateWebConfigFile($"{destFolder}\\web.config") ) return false;
      
      destFolder += "\\bin";
      if (!Utils.Extensions.CheckCreate( destFolder, out _ )) return false;

      var currentAssembly = System.Reflection.Assembly.GetExecutingAssembly();
      
      // TODO Find a way to fix the following problem:
      // some of the assemblies required by SimpleJsonRest (requiredAssemblies4lib) could be overloaded by assemblies already loaded by the calling assembly
      // In that case  ==> the following script could find the location of the assembly loaded by the calling assembly, 
      //                   not the one that SimpleJsonRest requires. And that's a very big problem because we will be copying the bad dll (not same version number)
      //               ==> When IIS or w3wp.exe calls our library --> BOOM !!!! Error: dll not found (because we are probably requiring a different version)
      var requiredAssemblies4Lib = currentAssembly.GetReferencedAssemblies();
      var dllPath = currentAssembly.Location;
      var destFile = $"{destFolder}\\SimpleJsonRest.dll";
      try {
        // Copy main dll (SimpleJsonRest)
        System.IO.File.Copy(dllPath, destFile, true);

        // Copy log4net & Newtonsoft.json dll
        foreach (var ass in requiredAssemblies4Lib) {
          var assName = ass.FullName;
          if (assName.Contains( "Newtonsoft.Json, Version=6.0.0.0" )) {
            var dllLocation = System.Reflection.Assembly.Load( ass ).Location;
            var dllDestination = $"{destFolder}\\{System.IO.Path.GetFileName( dllLocation )}";
            if (!System.IO.File.Exists( dllDestination ))
              System.IO.File.Copy( dllLocation, dllDestination, true );
            break;
          }
        }
      }
      catch (System.UnauthorizedAccessException) {
        Utils.Tracer.Log( $"No access to write \"{destFile}\"", Utils.MessageVerbosity.Error );
        return false;
      }

      // No credentials specified ? ==> Guessing that we're not
      if (config.Credentials == null) return true;

      return config.CreateIISEntry();
    }

    /// <summary>
    /// Setup administrator credentials to auto-register an entry in IIS (Internet Information Services).
    /// </summary>
    /// <param name="username"></param>
    /// <param name="password"></param>
    /// <param name="domainName"></param>
    public void SetUpCredentials(string username, string password, string domainName) {
      SetUpCredentials( new ServerAdmin( username , password, domainName) );
    }

    /// <summary>
    /// Setup administrator credentials to auto-register an entry in IIS (Internet Information Services).
    /// </summary>
    /// <param name="credentials"></param>
    public void SetUpCredentials(ServerAdmin credentials) {
      Credentials = credentials;
    }

    private bool CreateWebConfigFile( string destinationFile ) {
      try {
        // If file don't exists
        if (System.IO.File.Exists(destinationFile)) return true;
        
        // Create the file to write to.
        using(System.IO.StreamWriter writer = System.IO.File.CreateText(destinationFile)) {
            
          // Append lines
          writer.WriteLine( "<?xml version=\"1.0\" encoding=\"utf-8\"?>" );
          writer.WriteLine( "<!--" );
          writer.WriteLine( "  For more information on how to configure your ASP.NET application, please visit" );
          writer.WriteLine( "  https://go.microsoft.com/fwlink/?LinkId=169433\n  -->" );
          writer.WriteLine( "<configuration>" );
          writer.WriteLine( "  <configSections>" );
          writer.WriteLine( "    <section name=\"json4Rest\" type=\"SimpleJsonRest.Utils.HandlerConfig\" allowLocation=\"true\" allowDefinition=\"Everywhere\" />" );
          writer.WriteLine( "  </configSections>" );
          writer.WriteLine($"  <json4Rest assembly=\"{AssemblyPath.Replace("\\", "\\\\")}\" service=\"{Service}\" logPath=\"{LogPath}\" endpoint=\"{SiteName}\" />" );
          writer.WriteLine( "  <system.webServer>" );
          writer.WriteLine( "    <handlers>" );
          writer.WriteLine( "      <add name=\"Handler\" path=\"*\" verb=\"*\" type=\"SimpleJsonRest.Handler\" />" );
          writer.WriteLine( "    </handlers>" );
          writer.WriteLine( "  </system.webServer>" );
          writer.WriteLine( "</configuration>" );
        }

        return true;
      }
      catch {
        return false;
      }
    }

    private bool CreateIISEntry() {
      // We need credentials to manipulate IIS
      if (!Credentials.HasValue) return false;

      var iisDriver = new Core.AdminImitator( Credentials.Value );

      return iisDriver.RunAsAdministrator(
        new System.Threading.Tasks.Task<bool>( () => {
          // Prepare server manager && Check if website name already exists in IIS
          Microsoft.Web.Administration.ServerManager serverMgr = new Microsoft.Web.Administration.ServerManager();
          if (WebSiteExists( serverMgr, SiteName )) {
            Utils.Tracer.Log( $"Website ({SiteName}) already exists.", Utils.MessageVerbosity.Error );
            return false;
          }
          
          // Adding new web site binded to an application pool
          try {
            // Find (in existing application pools collection) one with 32 bits enabled or get default application pool and active 32 bits if we have the default application pool in IIS
            Microsoft.Web.Administration.ApplicationPool appPoolToUse = null;
            foreach (var pool in serverMgr.ApplicationPools) {
              // priority for application pool with 32bits enabled
              if (pool.Enable32BitAppOnWin64) {       
                appPoolToUse = pool;
                break;
              }
              // if no one has 32 bits but that DefaultAppPool exists ==> take and active 32 after iteration
              if (pool.Name == DEFAULT_APPLICATION_POOL)
                appPoolToUse = pool;
            }
            if (appPoolToUse?.Name == DEFAULT_APPLICATION_POOL)
              appPoolToUse.Enable32BitAppOnWin64 = true;
            // Adding web site entry and http/ip bindings 4 it
            Microsoft.Web.Administration.Site mySite = serverMgr.Sites.Add( SiteName, "http", $"*:{Port}:", Location ); // susceptible de throw FormatException
            // Specifing the application pool to use
            mySite.ApplicationDefaults.ApplicationPoolName = appPoolToUse?.Name ?? DEFAULT_APPLICATION_POOL;
            // commiting IIS changes
            serverMgr.CommitChanges();        
          }
          catch (System.FormatException e) {
            var msg = e.Message.StartsWith( "The site name cannot contain the following characters:" ) ? "Error with SiteName" : "Unexpected error";
            Utils.Tracer.Log( msg, e );
            return false;
          }
          catch (System.Exception e) {
            Utils.Tracer.Log( "Unexpected error", e );
            return false;
          }

          return true;
        } )
      );
    }

    private bool WebSiteExists(Microsoft.Web.Administration.ServerManager serverMgr, string strWebsitename) {
      var sitesCollection = serverMgr.Sites;
      for (var i = 0; i < sitesCollection.Count; i++)
        if (sitesCollection[i].Name == strWebsitename)
          return true;
      return false;
    }
    #endregion

  }
}