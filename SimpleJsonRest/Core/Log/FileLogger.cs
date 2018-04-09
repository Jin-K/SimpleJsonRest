namespace SimpleJsonRest.Core.Log {
  internal class FileLogger : LogBase {
    private readonly string _filePath;

    internal FileLogger(string filePath) {
      _filePath = filePath;
      if (!Utils.Extensions.CheckCreate( System.IO.Path.GetDirectoryName( filePath ), out string errMessage))
        throw new System.Exception( errMessage );
    }

    protected override void Log(string message, Verbosity verbosity) {
      lock(lockObj) {
        //if (!System.IO.File.Exists( _filePath )) System.IO.File.Create( _filePath ).Dispose();
        using (var writer = new System.IO.StreamWriter( _filePath, true ))
          writer.WriteLine( $"{System.DateTime.Now.ToString( "yyyy/MM/dd hh:mm:ss.fff" )} [{threadId}] {typeof( Verbosity ).GetEnumName( verbosity )}\t{message}" );
      }
    }

    internal void InfoFormat(string message, params object[] args) {
      Info( string.Format( message, args ) );
    }

    internal void DebugFormat(string message, params object[] args) {
      Debug( string.Format( message, args ) );
    }

    internal void ErrorFormat(string message, params object[] args) {
      Error( string.Format( message, args ) );
    }
  }
}