using System;
using System.Net;
using System.Net.Sockets;

namespace Ceen.Httpd.Cli.SubProcess
{
    /// <summary>
    /// Represents a unix domain socket address based on a filename.
    /// Adapted from https://github.com/mono/mono/blob/master/mcs/class/Mono.Posix/Mono.Unix/UnixEndPoint.cs
    /// </summary>
    internal class UnixEndPoint : EndPoint
    {
        /// <summary>
        /// The filename this endpoint is connected to
        /// </summary>
        private readonly string m_path;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Ceen.Httpd.Cli.UnixSpawn.UnixEndPoint"/> class.
        /// </summary>
        /// <param name="path">The path to bind to.</param>
        public UnixEndPoint(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException(nameof(path));

            m_path = path;
        }

        /// <summary>
        /// Internal constructor, used to create endpoint with empty filename
        /// </summary>
        private UnixEndPoint()
        {
            m_path = string.Empty;
        }

        /// <summary>
        /// Gets the filename this instance represents
        /// </summary>
        /// <value>The filename.</value>
        public string Filename { get { return m_path; } }

        /// <summary>
        /// Gets the address family.
        /// </summary>
        /// <value>The address family.</value>
        public override AddressFamily AddressFamily { get { return AddressFamily.Unix; } }

        /// <summary>
        /// Creates an <seealso cref="EndPoint"/> from a <seealso cref="SocketAddress"/>
        /// </summary>
        /// <returns>The created endpoint.</returns>
        /// <param name="socketAddress">The socket address to parse.</param>
        public override EndPoint Create(SocketAddress socketAddress)
        {
            // This happens for some reason on an accepted socket, which does not appear to reference
            // the bound instance anymore
            if (socketAddress.Family == AddressFamily.Unix && socketAddress.Size == 16)
                return new UnixEndPoint();

            var addr = (int)AddressFamily.Unix;
            if (socketAddress [0] != (addr & 0xFF))
                throw new ArgumentException ($"{nameof(socketAddress)} is not a unix socket address.");
            if (socketAddress [1] != ((addr & 0xFF00) >> 8))
                throw new ArgumentException ($"{nameof(socketAddress)} is not a unix socket address.");

            // This can happen if the argument is from RemoteEndPoint,
            // which on linux does not return the file name.
            if (socketAddress.Size == 2)
                return new UnixEndPoint();

            var size = socketAddress.Size - 2;
            var bytes = new byte[size];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = socketAddress[i + 2];
                // There may be junk after the null terminator, so ignore it all.
                if (bytes[i] == 0)
                {
                    size = i;
                    break;
                }
            }

            return new UnixEndPoint(System.Text.Encoding.UTF8.GetString(bytes, 0, size));
        }

        /// <summary>
        /// Converts this <seealso cref="UnixEndPoint"/> to a <seealso cref="SocketAddress"/>.
        /// </summary>
        /// <returns>The serialized address.</returns>
        public override SocketAddress Serialize()
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(m_path);
            var sa = new SocketAddress(AddressFamily, 2 + bytes.Length + 1);
            // sa [0] -> family low byte, sa [1] -> family high byte
            for (int i = 0; i < bytes.Length; i++)
                sa[2 + i] = bytes[i];

            //NULL suffix for non-abstract path
            sa[2 + bytes.Length] = 0;

            return sa;
        }

        /// <summary>
        /// Returns a <see cref="T:System.String"/> that represents the current <see cref="T:Ceen.Httpd.Cli.UnixSpawn.UnixEndPoint"/>.
        /// </summary>
        /// <returns>A <see cref="T:System.String"/> that represents the current <see cref="T:Ceen.Httpd.Cli.UnixSpawn.UnixEndPoint"/>.</returns>
        public override string ToString()
        {
            return (m_path);
        }

        /// <summary>
        /// Serves as a hash function for a <see cref="T:Ceen.Httpd.Cli.UnixSpawn.UnixEndPoint"/> object.
        /// </summary>
        /// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a
        /// hash table.</returns>
        public override int GetHashCode()
        {
            return m_path.GetHashCode();
        }

        /// <summary>
        /// Determines whether the specified <see cref="object"/> is equal to the current <see cref="T:Ceen.Httpd.Cli.UnixSpawn.UnixEndPoint"/>.
        /// </summary>
        /// <param name="o">The <see cref="object"/> to compare with the current <see cref="T:Ceen.Httpd.Cli.UnixSpawn.UnixEndPoint"/>.</param>
        /// <returns><c>true</c> if the specified <see cref="object"/> is equal to the current
        /// <see cref="T:Ceen.Httpd.Cli.UnixSpawn.UnixEndPoint"/>; otherwise, <c>false</c>.</returns>
        public override bool Equals(object o)
        {
            var other = o as UnixEndPoint;
            if (other == null)
                return false;

            return (other.m_path == m_path);
        }
    }
}
