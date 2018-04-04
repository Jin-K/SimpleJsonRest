using System.Linq;

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

    private const string DEFAULT_APPLICATION_POOL = "DefaultAppPool";

    internal const string DEFAULT_LOG_PATH = "Logs\\log.txt";
    #endregion

    #region Private members
    private HandlerConfig innerConfig;
    private int portToUse;
    private string serverToUse;
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
    /// Endpoint to use for naming the site's entry in IIS, (By default, IIS has the following site entry: "Default Web Site")
    /// </summary>
    public string SiteName {
      get { return innerConfig.EndPoint; }
      set { innerConfig.EndPoint = value; }
    }

    /// <summary>
    /// Location for IIS web folder (where /web.config and /bin/SimpleJsonRest.dll will be placed)
    /// </summary>
    public string Location { get; set; }

    public Models.AdministratorCredentials Credentials { get; set; }
    #endregion

    #region Methods
    private void Construct(string server, int port) {
      if (port == DEFAULT_PORT_SSL) throw new System.NotSupportedException("This port is reserved for SSL, which is still not supported.");
      innerConfig = new HandlerConfig();

      serverToUse = server;
      portToUse = port;

      // get location of caller
      System.Reflection.MethodBase callingMethod = new System.Diagnostics.StackTrace().GetFrame(2).GetMethod();
      innerConfig.AssemblyPath = callingMethod.DeclaringType.Assembly.CodeBase;
    }

    // TODO Do we need this method ? If yes ==> find a way to update the web.config file (couldn't work with calls coming from another way than IIS)
    internal bool UpdateWebConfigFile() {
      throw new System.NotImplementedException( "OUPS" );
      //try {
      //  var config = System.Web.Configuration.WebConfigurationManager.OpenWebConfiguration( "~" );
      //  HandlerConfig section = config.GetSection( "json4Rest" ) as HandlerConfig;
      //  section.AssemblyPath = innerConfig.AssemblyPath;
      //  section.Service = innerConfig.Service;
      //  section.LogPath = innerConfig.LogPath;
      //  config.Save();
      //  return true;
      //}
      //catch {
      //  return false;
      //}
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
    /// Create website folder and register website in IIS with specified values (check SimpleJsonRest.Utils.ConfigIIS's accessible properties)
    /// Default values for "server" and "port" are respectively "localhost" and 80.
    /// Set up a value for Credentials property if you want the library to auto-register a web site entry in IIS, 
    /// otherwise only the target folder for IIS will be created with minimum required files
    /// </summary>
    /// <returns></returns>
    public static bool RegisterInIIS(string service, string endpoint, string location, string logPath = ConfigIIS.DEFAULT_LOG_PATH) {
      return RegisterInIIS(ConfigIIS.DEFAULT_SERVER, ConfigIIS.DEFAULT_PORT, service, endpoint, location, logPath);
    }

    /// <summary>
    /// Create website folder and register website in IIS with specified values (check SimpleJsonRest.Utils.ConfigIIS's accessible properties).
    /// Set up a value for Credentials property if you want the library to auto-register a web site entry in IIS, 
    /// otherwise only the target folder for IIS will be created with minimum required files
    /// </summary>
    /// <returns></returns>
    public static bool RegisterInIIS(string server, int port, string service, string endpoint, string location, string logPath = ConfigIIS.DEFAULT_LOG_PATH) {
      var config = new ConfigIIS(server, port) {
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
    public static bool RegisterInIIS(ConfigIIS config) {
      if (config.LogPath == null) config.LogPath = DEFAULT_LOG_PATH;
      Tracer.SetupLog4Net( config.LogPath );
      
      var destFolder = config.Location;
      if (!CheckCreate( destFolder, out string error)) return false;
      if (!config.CreateWebConfigFile($"{destFolder}\\web.config") ) return false;
      
      destFolder += "\\bin";
      if (!CheckCreate( destFolder, out error )) return false;

      var currentAssembly = System.Reflection.Assembly.GetExecutingAssembly();
      
      /// TODO Find a way to fix the following problem:
      /// some of the assemblies required by SimpleJsonRest (requiredAssemblies4lib) could be overloaded by assemblies already loaded by the calling assembly
      /// In that case  ==> the following script could find the location of the assembly loaded by the calling assembly, 
      ///                   not the one that SimpleJsonRest requires. And that's a very big problem because we will be copying the bad dll (not same version number)
      ///               ==> When IIS or w3wp.exe calls our library --> BOOM !!!! Error: dll not found (because we are probably requiring a different version)
      var requiredAssemblies4lib = currentAssembly.GetReferencedAssemblies();
      var loadedAssembliesInDomain = System.AppDomain.CurrentDomain.GetAssemblies();
      var dllPath = currentAssembly.Location;
      var destFile = $"{destFolder}\\SimpleJsonRest.dll";
      try {
        /// Copy main dll (SimpleJsonRest)
        System.IO.File.Copy(dllPath, destFile, true);

        /// Copy log4net & Newtonsoft.json dll
        var stillNotFound = 2;
        foreach (var ass in requiredAssemblies4lib) {
          var assName = ass.FullName;
          if (assName.Contains( "log4net, Version=2.0.8.0" ) || assName.Contains( "Newtonsoft.Json, Version=6.0.0.0" )) {
            var dllLocation = System.Reflection.Assembly.Load( ass ).Location;
            var dllDestination = $"{destFolder}\\{System.IO.Path.GetFileName( dllLocation )}";
            System.IO.File.Copy( dllLocation, dllDestination, true ); // override if exists
            stillNotFound--;
          }
          if (stillNotFound == 0) break;
        }
      }
      catch (System.UnauthorizedAccessException) {
        Tracer.Log( $"No access to write \"{destFile}\"", MessageVerbosity.Error );
        return false;
      }

      /// No credentials specified ? ==> Guessing that we're not 
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
      SetUpCredentials( new Models.AdministratorCredentials( username , password, domainName) );
    }

    /// <summary>
    /// Setup administrator credentials to auto-register an entry in IIS (Internet Information Services).
    /// </summary>
    /// <param name="credentials"></param>
    public void SetUpCredentials(Models.AdministratorCredentials credentials) {
      this.Credentials = credentials;
    }

    private static bool CheckCreate(string directoryPath, out string errorMessage) {
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

    private bool CreateWebConfigFile( string destinationFile ) {
      try {
        // If file don't exists
        if (!System.IO.File.Exists(destinationFile)) {
          
          // Create the file to write to.
          using(System.IO.StreamWriter writer = System.IO.File.CreateText(destinationFile)) {

            // convert Assembly Path
            var assemblyPath = AssemblyPath;
            assemblyPath = assemblyPath.StartsWith( "file:///" ) ? assemblyPath.Substring( 8 ) : assemblyPath;
            assemblyPath = assemblyPath.Replace( "/", "\\\\" );

            // Append lines
            writer.WriteLine( "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" );
            writer.WriteLine( "<!--" );
            writer.WriteLine( "  For more information on how to configure your ASP.NET application, please visit" );
            writer.WriteLine( "  https://go.microsoft.com/fwlink/?LinkId=169433\n  -->\n\n" );
            writer.WriteLine( "<configuration>\n" );
            writer.WriteLine( "  <configSections>" );
            writer.WriteLine( "    <section name=\"json4Rest\" type=\"SimpleJsonRest.Utils.HandlerConfig\" allowLocation=\"true\" allowDefinition=\"Everywhere\" />" );
            writer.WriteLine( "  </configSections>" );
            writer.WriteLine($"  <json4Rest assembly=\"{assemblyPath}\" service=\"{Service}\" logPath=\"{LogPath}\" />" );
            writer.WriteLine( "  <system.webServer>" );
            writer.WriteLine( "    <handlers>" );
            writer.WriteLine( "      <add name=\"Handler\" path=\"*\" verb=\"*\" type=\"SimpleJsonRest.Handler\" />" );
            writer.WriteLine( "    </handlers>" );
            writer.WriteLine( "  </system.webServer>\n" );
            writer.WriteLine( "</configuration>" );
          }
        }

        return true;
      }
      catch {
        return false;
      }
    }

    private bool CreateIISEntry() {

      var iisDriver = new Core.IISImpersonationDriver( this.Credentials );

      return iisDriver.RunAsAdministrator(
        new System.Threading.Tasks.Task<bool>( () => {
          /// Prepare server manager && Check if website name already exists in IIS
          Microsoft.Web.Administration.ServerManager serverMgr = new Microsoft.Web.Administration.ServerManager();
          if (WebsiteExists( serverMgr, SiteName )) {
            Tracer.Log( $"Website ({SiteName}) already exists.", MessageVerbosity.Error );
            return false;
          }
          
          /// Adding new web site binded to an application pool
          try {
            /// Find (in existing application pools collection) one with 32 bits enabled or get default application pool and active 32 bits if we have the default application pool in IIS
            Microsoft.Web.Administration.ApplicationPool appPoolToUse = null;
            foreach (var pool in serverMgr.ApplicationPools) {
              /// priority for application pool with 32bits enabled
              if (pool.Enable32BitAppOnWin64) {       
                appPoolToUse = pool;
                break;
              }
              /// if no one has 32 bits but that DefaultAppPool exists ==> take and active 32 after iteration
              if (pool.Name == DEFAULT_APPLICATION_POOL)
                appPoolToUse = pool;
            }
            if (appPoolToUse.Name == DEFAULT_APPLICATION_POOL)
              appPoolToUse.Enable32BitAppOnWin64 = true;
            /// Adding web site entry and http/ip bindings 4 it
            Microsoft.Web.Administration.Site mySite = serverMgr.Sites.Add( SiteName, "http", $"*:{Port}:", Location ); // susceptible de throw FormatException
            /// Specifing the application pool to use
            mySite.ApplicationDefaults.ApplicationPoolName = appPoolToUse.Name;
            /// commiting IIS changes
            serverMgr.CommitChanges();        
          }
          catch (System.FormatException e) {
            var msg = e.Message.StartsWith( "The site name cannot contain the following characters:" ) ? "Error with SiteName" : "Unexpected error";
            Tracer.Log( msg, e );
            return false;
          }
          catch (System.Exception e) {
            Tracer.Log( "Unexpected error", e );
            return false;
          }

          return true;
        } )
      );
    }

    private bool WebsiteExists(Microsoft.Web.Administration.ServerManager serverMgr, string strWebsitename) {
      bool flagset = false;
      Microsoft.Web.Administration.SiteCollection sitecollection = serverMgr.Sites;
      foreach (Microsoft.Web.Administration.Site site in sitecollection) {
        if (site.Name == strWebsitename.ToString()) {
          flagset = true;
          break;
        }
        else {
          flagset = false;
        }
      }
      return flagset;
    }
    #endregion

  }
}