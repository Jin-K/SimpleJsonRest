namespace SimpleJsonRest.Models {
  /// <summary>
  /// Structure used to run some instructions as administrator on a domain/workgroup.
  /// Check for ConfigIIS.Credentials property
  /// </summary>
  public class AdministratorCredentials {
    public string Username { get; private set; }
    public string DomainName { get; private set; }
    public string Password { get; private set; }

    /// <summary>
    /// AdministratorCredentials constructor
    /// </summary>
    /// <param name="username"></param>
    /// <param name="password"></param>
    /// <param name="domainName"></param>
    public AdministratorCredentials (string username, string password, string domainName) {
      this.Username = username;
      this.DomainName = domainName;
      this.Password = password;
    }
  }
}