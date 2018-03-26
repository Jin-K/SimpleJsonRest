namespace SimpleJsonRest.Core {
  public delegate void LibraryErrorHandler(string errorMessage, System.Type emitterType);

  static public class EventsDispatcher {
    static EventsDispatcher() {
      // TODO MAYBE: find a way to unsubscribe to all event handlers
      Utils.Tracer.OnTracerError += message => OnLibraryError( message, typeof(Utils.Tracer) );
    }

    /// <summary>
    /// Send Library's first-level exception message on inner exception
    /// </summary>
    static public event LibraryErrorHandler OnLibraryError;
  }
}