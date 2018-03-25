namespace SimpleJsonRest {
  public interface IAuthentifiedService {
    LoginResponse Login(string login, string password);
    bool Logout(string sessionId);
  }
}
