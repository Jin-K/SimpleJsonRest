namespace SimpleJsonRest {
  internal class HandlerException : System.Net.WebException {
    string _message;
    public new string Message {
      get {
        return _message ?? base.Message;
      }
      private set {
        _message = value;
      }
    }

    internal System.Net.HttpStatusCode StatusCode { get; private set; }

    internal HandlerException(string message, System.Net.HttpStatusCode statusCode = System.Net.HttpStatusCode.InternalServerError) : base(message) {
      Message = message;
      StatusCode = statusCode;
    }
  }

  public class FaultException : System.SystemException {
    string reason;
    public override string Message => reason;

    FaultCode code;
    public FaultCode Code {
      get { return code ?? new FaultCode("No code specification found."); }
      private set { code = value; }
    }

    public FaultException(string reason) : base(reason) {
      this.reason = reason;
    }

    public FaultException(string reason, FaultCode code) : this(reason) {
      Code = code;
    }
  }

  public class FaultCode {
    string name;
    public string Name {
      get { return name; }
      private set { name = value; }
    }

    public FaultCode(string name) {
      Name = name;
    }
  }
}