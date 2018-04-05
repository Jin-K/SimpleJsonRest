namespace SimpleJsonRest.IIS {
  /// <summary>
  /// Structure used to run some instructions as administrator on a domain/workgroup.
  /// Check for Config.Credentials property
  /// </summary>
  public struct ServerAdmin {
    public string Username { get; private set; }
    public string DomainName { get; private set; }
    public string Password { get; private set; }

    /// <summary>
    /// ServerAdmin constructor
    /// </summary>
    /// <param name="username"></param>
    /// <param name="password"></param>
    /// <param name="domainName"></param>
    public ServerAdmin (string username, string password, string domainName) {
      this.Username = username;
      this.DomainName = domainName;
      this.Password = password;
    }
  }
}