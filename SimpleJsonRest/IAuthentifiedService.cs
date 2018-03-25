using System;
namespace SimpleHandler {
    public interface IAuthentifiedService {
        LoginResponse Login(string login, string password);
        bool Logout(string sessionId);
    }
}