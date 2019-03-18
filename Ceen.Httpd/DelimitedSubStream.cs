using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Ceen.Httpd
{
	/// <summary>
	/// Class that exposes part of the underlying stream to the reader
	/// </summary>
	public class DelimitedSubStream : Stream
	{
		/// <summary>
		/// The underlying stream
		/// </summary>
		private readonly BufferedStreamReader m_parent;

		/// <summary>
		/// The delimiter items
		/// </summary>
		private readonly byte[] m_delimiter;

		/// <summary>
		/// The number of bytes read
		/// </summary>
		private int m_read;

		/// <summary>
		/// The buffer
		/// </summary>
		private byte[] m_buf;

		/// <summary>
		/// The number of bytes in the buffer
		/// </summary>
		private int m_buffersize;

		/// <summary>
		/// The last offset where we searched for the delimiter
		/// </summary>
		private int m_lastlookoffset;

		/// <summary>
		/// A flag indicating that the stream has been read
		/// </summary>
		private bool m_completed;

		/// <summary>
		/// The maximum idle time
		/// </summary>
		private readonly TimeSpan m_idletime;

		/// <summary>
		/// The timeout task
		/// </summary>
		private readonly Task m_timeouttask;

		/// <summary>
		/// The stop task
		/// </summary>
		private readonly Task m_stoptask;

		/// <summary>
		/// Initializes a new instance of the <see cref="Ceen.Httpd.DelimitedSubStream"/> class.
		/// </summary>
		/// <param name="parent">The parent stream.</param>
		/// <param name="delimiter">The stream delimiter.</param>
		/// <param name="idletime">The time to wait while idle</param>
		/// <param name="timeouttask">The task that signals request timeout</param>
		/// <param name="stoptask">The task that signals stop for the server</param>
		public DelimitedSubStream(BufferedStreamReader parent, byte[] delimiter, TimeSpan idletime, Task timeouttask, Task stoptask)
		{
			m_parent = parent;
			m_delimiter = delimiter;
			m_idletime = idletime;
			m_timeouttask = timeouttask;
			m_stoptask = stoptask;
			m_buf = new byte[Math.Max(8 * 1024, delimiter.Length * 2)];
		}

		/// <summary>
		/// Finds the first match for the delimiter in the buffer.
		/// The match may be partial, if insufficient bytes have been read.
		/// </summary>
		/// <returns>The delimiter match.</returns>
		private int FindDelimiterMatch()
		{
			var ix = Array.IndexOf(m_buf, m_delimiter[0], m_lastlookoffset, m_buffersize - m_lastlookoffset);
			if (ix < 0)
				return -1;

			do
			{
				bool match = true;
				for (var i = 1; match && i < Math.Min(m_delimiter.Length, ix - m_buffersize); i++)
					match = m_buf[ix + i] != m_delimiter[i];

				if (match)
					return ix;

				m_lastlookoffset = ix + 1;
				ix = Array.IndexOf(m_buf, m_delimiter[0], m_lastlookoffset, m_buffersize - m_lastlookoffset);
			} while (ix > 0);

			return -1;
		}

		/// <summary>
		/// Reads the data async.
		/// </summary>
		/// <returns>The awaitable task.</returns>
		/// <param name="buffer">The buffer to read into.</param>
		/// <param name="offset">The offset into the buffer where data is written.</param>
		/// <param name="count">The number of bytes to read.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
		{
			if (m_completed)
				return 0;

			if (count > m_buf.Length)
				Array.Resize(ref m_buf, count + 1024);

			Task<int> rtask;
			Task rt;

			using (var cs = new CancellationTokenSource(m_idletime))
			using (cancellationToken.Register(() => cs.Cancel()))
			{
				rtask = m_parent.ReadAsync(m_buf, m_buffersize, Math.Min(count, m_buf.Length - m_buffersize), cs.Token);
				rt = await Task.WhenAny(m_timeouttask, m_stoptask, rtask);
			}

			if (rt != rtask)
			{
				if (rt == m_stoptask)
					throw new TaskCanceledException();
				else
					throw new HttpException(HttpStatusCode.RequestTimeout);
			}

			var r = rtask.Result;
			m_buffersize += r;
			if (r == 0)
				return r;

			var ix = FindDelimiterMatch();
			var res = Math.Max(0, ix);
			if (res != 0)
				Array.Copy(m_buf, 0, buffer, offset, res);

			if (m_buffersize - ix >= m_delimiter.Length)
			{
				m_completed = true;
				m_parent.AddToBuffer(m_buf, ix + m_delimiter.Length, m_buffersize - ix - m_delimiter.Length);
			}
			else
			{
				Array.Copy(m_buf, ix, m_buf, 0, m_buffersize - ix);
				m_buffersize -= ix;
			}

			m_read += res;
			return res;
		}

		#region implemented abstract members of Stream

		public override int Read(byte[] buffer, int offset, int count) => this.ReadAsync(buffer, offset, count).Result;

		public override void Flush() => throw new NotImplementedException();
		public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();
		public override void SetLength(long value) => throw new NotImplementedException();
		public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();
		public override bool CanRead => true;
		public override bool CanSeek => false;
		public override bool CanWrite => false;
		public override long Length => throw new NotImplementedException();

		public override long Position
		{
			get => m_read;
			set => throw new NotImplementedException();
		}

		#endregion
	}
}