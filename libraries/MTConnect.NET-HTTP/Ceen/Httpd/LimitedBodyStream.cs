using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Ceen.Httpd
{
    internal class LimitedBodyStream : Stream
    {
        /// <summary>
        /// The underlying stream
        /// </summary>
        private readonly BufferedStreamReader m_parent;

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
        /// The number of bytes to read
        /// </summary>
        private long m_bytesleft;

        /// <summary>
        /// The number of bytes read
        /// </summary>
        private long m_bytesread;

        /// <summary>
        /// Value indicating if the requests are just passed through
        /// </summary>
        private readonly bool m_passthrough;

        /// <summary>
        /// Initializes a new instance of the <see cref="Ceen.Httpd.LimitedBodyStream"/> class.
        /// </summary>
        /// <param name="parent">The parent stream.</param>
        /// <param name="totalbytes">The number of bytes to limit to.</param>
        /// <param name="idletime">The maximum idle time.</param>
        /// <param name="timeouttask">The timeout wait task.</param>
        /// <param name="stoptask">The stop signal task.</param>
        public LimitedBodyStream(BufferedStreamReader parent, long totalbytes, TimeSpan idletime, Task timeouttask, Task stoptask)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));

            m_parent = parent;
            m_bytesleft = totalbytes;
            m_idletime = idletime;
            m_timeouttask = timeouttask;
            m_stoptask = stoptask;
            m_passthrough = totalbytes < 0;
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
            Task<int> rtask;
            Task rt;

            if (m_passthrough)
            {
                using (var cs = new CancellationTokenSource(m_idletime))
                using (cancellationToken.Register(() => cs.Cancel()))
                {
                    rtask = m_parent.ReadAsync(buffer, offset, count, cs.Token);
                    rt = await Task.WhenAny(m_timeouttask, m_stoptask, rtask);
                }

                if (rt != rtask)
                {
                    if (rt == m_stoptask)
                        throw new TaskCanceledException();
                    else
                        throw new HttpException(HttpStatusCode.RequestTimeout);
                }

                return rtask.Result;
            }

            if (m_bytesleft <= 0)
                return 0;

            using (var cs = new CancellationTokenSource(m_idletime))
            using (cancellationToken.Register(() => cs.Cancel()))
            {
                rtask = m_parent.ReadAsync(buffer, offset, (int)Math.Min(count, m_bytesleft), cs.Token);
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
            if (r == 0)
                return r;

            m_bytesleft -= r;
            m_bytesread += r;
            return r;
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
        public override long Length => m_bytesleft + m_bytesread;

        public override long Position
        {
            get => m_bytesread;
            set => throw new NotImplementedException();
        }

        /// <summary>
        /// Helper method to consume the body of the request
        /// </summary>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns><c>true</c> if the bytes could be discarded, <c>false</c> otherwise</returns>
        public async Task<bool> DiscardAllAsync(System.Threading.CancellationToken cancellationToken)
        {
            if (m_passthrough)
                return false;

            var buf = new byte[1024 * 8];
            while (m_bytesleft > 0)
            {
                // CA2022 short-read handling — TFM-uniform. Every supported TFM
                // lands on the same shape: loop ReadAsync until the transport
                // signals EOF (return 0) or the whole body is drained. ReadAsync
                // may return fewer bytes than requested on multi-segment TCP
                // arrivals; the loop keeps calling until m_bytesleft hits zero
                // (drain complete → return true) or a 0-byte read indicates
                // premature EOF (return false so the caller can propagate the
                // drain failure to the outer HTTP handler).
                //
                // Do NOT re-introduce a ReadExactlyAsync-into-fixed-buffer shape
                // on any TFM: when the remaining body is smaller than buf.Length
                // (the common case on the final iteration and on any body
                // smaller than 8 KB), ReadExactlyAsync throws
                // EndOfStreamException and propagates up through the outer
                // HttpServer catch, killing keep-alive and 500-ing the client.
                var read = await ReadAsync(buf, 0, buf.Length, cancellationToken);
                if (read == 0)
                    return false;
            }

            return true;
        }
        #endregion
    }
}

