using System;

namespace SimpleJsonRest.Models {
  /// <summary>
  /// Structure containing a session ID and datetime of session start
  /// </summary>
  public struct LoginResponse {
    public string SessionId { get; set; }
    public System.DateTime Start { get; set; }

    /// <summary>
    /// Construct a LoginResponse with given sessionId and datetime of instanciation
    /// </summary>
    /// <param name="sessionId"></param>
    public LoginResponse(string sessionId) {
      SessionId = sessionId;
      Start = DateTime.Now;
    }
  }
}