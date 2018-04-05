using System;
using System.Runtime.InteropServices;

namespace SimpleJsonRest.Core {
  class AdminImitator {
    const int LOGON32_PROVIDER_DEFAULT = 0;
    const int LOGON32_LOGON_INTERACTIVE = 2;

    [DllImport( "advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode )]
    public static extern bool LogonUser(string lpszUsername, string lpszDomain, string lpzsPassword, int dwLogonType, int dwLogonProvider, out SafeTokenHandleStruct phToken);

    [DllImport( "kernel32.dll", CharSet = CharSet.Auto )]
    public extern static bool CloseHandle(IntPtr handle);

    internal readonly string Username;
    internal readonly string DomainName;
    internal readonly string Password;

    /// <summary>
    /// Creates a representation of an user allowed to manage IIS on the machine (with required mechanisms).
    /// User should be local administrator
    /// </summary>
    /// <param name="username"></param>
    /// <param name="domainName"></param>
    /// <param name="password"></param>
    internal AdminImitator(string username, string domainName, string password) {
      this.Username = username;
      this.DomainName = domainName;
      this.Password = password;
    }

    /// <summary>
    /// Creates a representation of an user allowed to manage IIS on the machine (with required mechanisms).
    /// User should be local administrator
    /// </summary>
    /// <param name="credentials"></param>
    internal AdminImitator(IIS.ServerAdmin credentials) : this( credentials.Username, credentials.DomainName, credentials.Password ) {}
    
    // TODO If you incorporate this code into a DLL, be sure to demand FullTrust
    /// <summary>
    /// Wrapping method allowing us to act or perform a piece of code as another user (impersonation).
    /// Expecting a callback (of type Task<bool>) as parameter to be started during impersonation.
    /// </summary>
    /// <param name="taskToDo"></param>
    /// <returns></returns>
    [System.Security.Permissions.PermissionSet( System.Security.Permissions.SecurityAction.Demand, Name = "FullTrust")]
    internal bool RunAsAdministrator(System.Threading.Tasks.Task<bool> taskToDo) {
      try {
        /// If logon fails
        if (!LogonUser( Username, DomainName, Password, LOGON32_LOGON_INTERACTIVE, LOGON32_PROVIDER_DEFAULT, out SafeTokenHandleStruct safeTokenHandle )) {
          Utils.Tracer.Log( $"Could not logon as {Username}@{DomainName} ; error code : {Marshal.GetLastWin32Error()}", Utils.MessageVerbosity.Error );
          return false;
        }

        /// we're connected
        using (safeTokenHandle) {
          Utils.Tracer.Log( $"Logged in with Windows NT token \"{safeTokenHandle}\" : {Username}@{DomainName}" );

          /// Use the token handle returned by LogonUser
          using (System.Security.Principal.WindowsImpersonationContext ctx = System.Security.Principal.WindowsIdentity.Impersonate( safeTokenHandle.DangerousGetHandle() )) {
            /// using context will let us act as the new user
            using (taskToDo) {
              taskToDo.Start();
              return taskToDo.Result;
            }
            /// on Dispose/Close, we switch back to previous user
          }
        }
      }
      catch (Exception ex) {
        Utils.Tracer.Log( "Error trying to run as administrator", ex );
        return false;
      }
    }
  }
}