using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ceen;

namespace Ceen.Httpd.Handler
{
    /// <summary>
    /// A basic proxy handler that simply forwards all data from another http server
    /// </summary>
    internal class SimpleProxyHandler : IHttpModule
    {
        /// <summary>
        /// The url we proxy requests for, should not end with a forward slash
        /// </summary>
        public string HostUrl { get; set; }

        /// <summary>
        /// A prefix to remove from the source url.
        /// If this is set, only requests matching the prefix
        /// are proxied
        /// </summary>
        public string LeftTrimSourceUrl { get; set; }

        /// <summary>
        /// Helper method to rewrite the url
        /// </summary>
        /// <param name="context">The context with the request to rewrite</param>
        /// <returns>The url for the proxy instance, or null if the request should not be proxied</returns>
        protected string RewriteUrl(IHttpContext context)
        {
            var path = context.Request.Path;
            if (!string.IsNullOrWhiteSpace(LeftTrimSourceUrl))
            {
                if (!path.StartsWith(LeftTrimSourceUrl))
                    return null;

                path = path.Substring(LeftTrimSourceUrl.Length);
            }

            return HostUrl + path + context.Request.RawQueryString;
        }

        /// <summary>
        /// Handles a proxy request
        /// </summary>
        /// <param name="context">The request content</param>
        /// <param name="cancellationToken">The token indicating to stop handling.</param>
        /// <returns><c>true</c> if the request was handled; <c>false</c> otherwise</returns>
        public async Task<bool> HandleAsync(IHttpContext context, CancellationToken cancellationToken)
        {
            var targeturl = RewriteUrl(context);
            if (targeturl == null)
                return false;

#pragma warning disable SYSLIB0014 // vendored Ceen proxy handler still uses WebRequest; HttpClient migration tracked separately
            var wr = System.Net.WebRequest.CreateHttp(targeturl);
#pragma warning restore SYSLIB0014
            wr.Headers.Clear();
            foreach (var item in context.Request.Headers)
                wr.Headers.Add(item.Key, item.Value);

            wr.Method = context.Request.Method;
            if (context.Request.ContentLength > 0)
                using (var rs = await wr.GetRequestStreamAsync())
                    // Same bug class as MTConnectPostResponseHandler.ReadRequestBytes
                    // (dime F-IMP-001): a request-body drain must honour the outer
                    // cancellation token so a client abort short-circuits the copy
                    // rather than reading the full body into the proxied upstream.
                    // Uses the (Stream, int bufferSize, CancellationToken) overload
                    // (universal since .NET 4.5); the 2-arg (Stream, CancellationToken)
                    // shape is netstandard2.1 / net5+ only and cannot ship on the
                    // library's net4x / netstandard2.0 targets. The 81920 buffer
                    // matches the .NET runtime default for the tokenless
                    // CopyToAsync overload so throughput is unchanged.
                    await context.Request.Body.CopyToAsync(rs, 81920, cancellationToken);

            using (var res = await GetResponseWithoutExceptionAsync(wr))
            {
                foreach (var key in res.Headers.AllKeys)
                    context.Response.Headers[key] = res.Headers[key];
                context.Response.StatusCode = (Ceen.HttpStatusCode)(int)res.StatusCode;
                context.Response.StatusMessage = res.StatusDescription;

                // The .Net http client has already removed the chunked markers
                // so the stream contains only data
                if (string.Equals(context.Response.Headers["Transfer-Encoding"], "chunked", StringComparison.OrdinalIgnoreCase))
                    context.Response.Headers.Remove("Transfer-Encoding");

                await context.Response.FlushHeadersAsync();
                using (var r = context.Response.GetResponseStream())
                using (var rr = res.GetResponseStream())
                    // Same bug class as the request-body drain 21 lines above
                    // (dime F-IMP-005) and the sibling
                    // MTConnectPostResponseHandler.ReadRequestBytes (dime F-IMP-001):
                    // a slow upstream response must not block a client abort.
                    // Uses the universal 3-arg (Stream, int bufferSize,
                    // CancellationToken) overload so net461-net48 /
                    // netstandard2.0 / net6.0 also honour the token — the
                    // pre-existing #if NET5_0_OR_GREATER guard left those six
                    // TFMs on a tokenless CopyToAsync, letting a slow-response
                    // upstream fully drain after cancel. The 81920 buffer
                    // matches the runtime default for the tokenless overload
                    // so throughput on the happy path is unchanged.
                    await rr.CopyToAsync(r, 81920, context.Request.TimeoutCancellationToken);
            }

            return true;
        }

        /// <summary>
        /// Helper method to work around annoying exceptions thrown by System.Net.WebRequest
        /// when the server returns a non-success status code
        /// </summary>
        /// <param name="req">The request to get the response from</param>
        /// <returns>The web response</returns>
        private static async Task<System.Net.HttpWebResponse> GetResponseWithoutExceptionAsync(System.Net.HttpWebRequest req)
        {
            try
            {
                return (System.Net.HttpWebResponse)await req.GetResponseAsync();
            }
            catch (System.Net.WebException we)
            {
                var resp = we.Response as System.Net.HttpWebResponse;
                if (resp == null)
                    throw;
                return resp;
            }
        }
    }
}