namespace SimpleJsonRest {
  public interface IAuthentifiedService {
    Models.LoginResponse Login(string login, string password);
    bool Logout(string sessionId);
  }
}
