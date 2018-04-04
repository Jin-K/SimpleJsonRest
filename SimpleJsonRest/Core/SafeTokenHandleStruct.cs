namespace SimpleJsonRest.Core {
  /// <summary>
  /// c# wrapping class of an unmanaged structure type I guess, used as a c++ reference variable
  /// </summary>
  public sealed class SafeTokenHandleStruct : Microsoft.Win32.SafeHandles.SafeHandleZeroOrMinusOneIsInvalid {
    private SafeTokenHandleStruct() : base( true ) { }

    [System.Runtime.InteropServices.DllImport( "kernel32.dll" )]
    [System.Runtime.ConstrainedExecution.ReliabilityContract(
      System.Runtime.ConstrainedExecution.Consistency.WillNotCorruptState,
      System.Runtime.ConstrainedExecution.Cer.Success
    )]
    [System.Security.SuppressUnmanagedCodeSecurity]
    [return: System.Runtime.InteropServices.MarshalAs( System.Runtime.InteropServices.UnmanagedType.Bool )]
    private static extern bool CloseHandle(System.IntPtr handle);

    protected override bool ReleaseHandle() {
      return CloseHandle( handle );
    }
  }
}