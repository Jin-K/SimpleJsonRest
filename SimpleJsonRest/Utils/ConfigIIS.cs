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
    /// Endpoint to use for the mapping in IIS, (ex: "/my/path" in "server:port/my/path")
    /// </summary>
    public string EndPoint {
      get { return innerConfig.EndPoint; }
      set { innerConfig.EndPoint = value.StartsWith("/") ? value : "/" + value; }
    }

    /// <summary>
    /// Location for IIS web folder (where /web.config and /bin/SimpleJsonRest.dll will be placed)
    /// </summary>
    public string Location { get; set; }
    #endregion

    #region Methods
    private void Construct(string server, int port) {
      if (port == DEFAULT_PORT_SSL) throw new System.Exception("Go fuck yourself with your SSL");
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
    public static bool RegisterInIIS(string service, string endpoint, string location, string logPath = ConfigIIS.DEFAULT_LOG_PATH) {
      return RegisterInIIS(ConfigIIS.DEFAULT_SERVER, ConfigIIS.DEFAULT_PORT, service, endpoint, location, logPath);
    }

    /// <summary>
    /// Create website folder and register website in IIS with specified values (check SimpleJsonRest.Utils.ConfigIIS's accessible properties)
    /// </summary>
    /// <returns></returns>
    public static bool RegisterInIIS(string server, int port, string service, string endpoint, string location, string logPath = ConfigIIS.DEFAULT_LOG_PATH) {
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
    public static bool RegisterInIIS(ConfigIIS config) {
      if (config.LogPath == null) config.LogPath = DEFAULT_LOG_PATH;
      Utils.Tracer.SetupLog4Net( config.LogPath ); // test
      
      var destFolder = config.Location;
      
      if (!CheckCreate( destFolder, out string error)) return false;
      if (!config.CreateWebConfigFile($"{destFolder}\\web.config") ) return false;
      
      destFolder += "\\bin";
      
      if (!CheckCreate( destFolder, out error )) return false;
      var uriBuilder = new System.UriBuilder(System.Reflection.Assembly.GetExecutingAssembly().CodeBase);
      var uriPath = System.Uri.UnescapeDataString(uriBuilder.Path);
      var dllPath = $"{System.IO.Path.GetDirectoryName(uriPath)}\\SimpleJsonRest.dll";
      var destFile = $"{destFolder}\\SimpleJsonRest.dll";
      try {
        System.IO.File.Copy(dllPath, destFile, true);
      }
      catch (System.UnauthorizedAccessException) {
        Tracer.Log( $"No access to write \"{destFile}\"", MessageVerbosity.Error );
        return false;
      }

      return config.CreateIISEntry3(); // TODO Solve problem to add entry in IIS, we should probably use WindowsIdentity.Impersonate (ref: https://msdn.microsoft.com/en-us/library/chf6fbt4(v=vs.110).aspx)
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
      Microsoft.Web.Administration.ServerManager iisManager = null;
      try {
        iisManager = new Microsoft.Web.Administration.ServerManager();
        iisManager.Sites.Add( EndPoint, "http", "*:80", Location );
        iisManager.CommitChanges();
      }
      catch {
        return false;
      }
      finally {
        if (iisManager != null) iisManager.Dispose();
      }

      return true;
    }

    private bool CreateIISEntry2() {
      try {
        using(Microsoft.Web.Administration.ServerManager m = new Microsoft.Web.Administration.ServerManager()) {
          Microsoft.Web.Administration.ApplicationPool pool = m.ApplicationPools.Add( "MyPool" );
          Microsoft.Web.Administration.Site site = m.Sites.CreateElement( "site" );
          site.Name = "MySite";
          site.Id = "MySite".GetHashCode();

          Microsoft.Web.Administration.Application app = site.Applications.Add( EndPoint, Location );
          app.ApplicationPoolName = "MyPool";

          m.CommitChanges();
        }
      }
      catch {
        return false;
      }

      return true;
    }

    private bool CreateIISEntry3() {
      try {
        //System.IntPtr accessToken = System.IntPtr.Zero;
        //System.Security.Principal.WindowsIdentity identity = new System.Security.Principal.WindowsIdentity( accessToken );
        //System.Security.Principal.WindowsImpersonationContext context = identity.Impersonate();
        
        Microsoft.Web.Administration.ServerManager serverMgr = new Microsoft.Web.Administration.ServerManager();
        string strWebsitename = EndPoint.Replace( "/", "" );
        string strApplicationPool = "DefaultAppPool";
        string strhostname = "localhost"; //abc.com
        string stripaddress = "";// ip address
        string bindinginfo = stripaddress + ":80:" + strhostname;

        //check if website name already exists in IIS
        bool bWebsite = IsWebsiteExists( serverMgr,  strWebsitename );
        if (!bWebsite) {
          Microsoft.Web.Administration.Site mySite = serverMgr.Sites.Add( EndPoint, "http", bindinginfo, Location );
          mySite.ApplicationDefaults.ApplicationPoolName = strApplicationPool;
          mySite.TraceFailedRequestsLogging.Enabled = true;
          mySite.TraceFailedRequestsLogging.Directory = "C:\\inetpub\\customfolder\\site";
          serverMgr.CommitChanges();
        }
        else return false;
      }
      catch (System.UnauthorizedAccessException) { return false; }
      catch { return false; }

      return true;
    }

    private bool IsWebsiteExists(Microsoft.Web.Administration.ServerManager serverMgr, string strWebsitename) {
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

    private bool CreateIISEntry4() {
      string metabasePath = "IIS://localhost/W3SVC";
      System.DirectoryServices.DirectoryEntry w3svc = new System.DirectoryServices.DirectoryEntry( metabasePath, "angel.munoz@amma.be", "password" );

      string serverBinding = ":80:localhost";
      string homeDirectory = Location;

      object[] newSite = new object[] { "", new object[] { serverBinding }, homeDirectory };
      object webSiteId = (object) w3svc.Invoke( "CreateNewSite", newSite );

      // Returns the Website ID from the Metabase
      var id = (int) webSiteId;

      Tracer.Log( "WebSiteId = " + id, MessageVerbosity.Info );

      return true;
    }
    #endregion

  }
}