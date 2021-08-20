using System;
using System.Runtime.InteropServices;

namespace Ceen.Httpd.Cli
{
    /// <summary>
    /// The operating system we are currently on
    /// </summary>
    public enum Platform
    {
        /// <summary>
        /// The MS Windows operating system
        /// </summary>
        Windows,
        /// <summary>
        /// A Linux/Unix/BSD variant operating system
        /// </summary>
        Linux,
        /// <summary>
        /// The MacOS operating system
        /// </summary>
        MacOS,
        /// <summary>
        /// An unknown operating system
        /// </summary>
        Unknown
    }

    /// <summary>
    /// A helper class to get information about the system
    /// </summary>
    public static class SystemHelper
    {
        /// <summary>
        /// Gets the OS currently executing
        /// </summary>
        public static Platform CurrentOS
        {
            get
            {
                switch (Environment.OSVersion.Platform)
                {
                    case PlatformID.Win32NT:
                    case PlatformID.Win32S:
                    case PlatformID.Win32Windows:
                    case PlatformID.WinCE:
                        return Platform.Windows;
                    case PlatformID.Unix:
                        // Fix some cases of MacOS reported as Unix
                        if (IsDarwin())
                            return Platform.MacOS;
                        return Platform.Linux;
                    case PlatformID.MacOSX:
                        return Platform.MacOS;
                    default:
                        return Platform.Unknown;
                }                
            }
        }

        /// <summary>
        /// Gets a value indicating whether the current operating system is Posix based
        /// </summary>
        /// <value><c>true</c> if is current OS is Posix; otherwise, <c>false</c>.</value>
        public static bool IsCurrentOSPosix => CurrentOS == Platform.Linux || CurrentOS == Platform.MacOS;

        /// <summary>
        /// Calls libc to get the uname of the current system
        /// </summary>
        /// <returns>The OS uname.</returns>
        /// <param name="buf">A pre-allocated buffer for the result.</param>
        [DllImport("libc")]
        private static extern int uname(IntPtr buf);

        /// <summary>
        /// A cache variable to avoid repeated P/Invoke calls
        /// </summary>
        private static bool? _isDarwin;

        /// <summary>
        /// Checks if the system reports &quot;Darwin&quot; as the uname
        /// </summary>
        /// <returns><c>true</c>, if the Darwin OS is detected, <c>false</c> otherwise.</returns>
        private static bool IsDarwin()
        {
            if (_isDarwin.HasValue)
                return _isDarwin.Value;

            var buffer = IntPtr.Zero;
            try
            {
                buffer = Marshal.AllocHGlobal(8 * 1024);
                if (uname(buffer) == 0)
                {
                    var os = Marshal.PtrToStringAnsi(buffer);
                    if (os == "Darwin")
                        return ((bool)(_isDarwin = true));
                }
            }
            catch
            {
            }
            finally
            {
                if (buffer != IntPtr.Zero)
                    Marshal.FreeHGlobal(buffer);
            }
            return ((bool)(_isDarwin = false));            
        }

        /// <summary>
        /// Gets a value indicating whether we are executing with .Net core
        /// </summary>
        public static bool IsNetCore => RuntimeInformation.FrameworkDescription.StartsWith(".NET Core", StringComparison.Ordinal);

        /// <summary>
        /// Gets a value indicating whether we are executing with .Net5 or later
        /// </summary>
        public static bool IsNet5OrGreater => Environment.Version >= new Version(5,0);

        /// <summary>
        /// Flag indicating if a spawned process will need the &quot;dotnet&quot; program to launch
        /// </summary>
        public static bool ProcessStartRequiresDotnetPrefix => IsNetCore || IsNet5OrGreater;

    }
}
