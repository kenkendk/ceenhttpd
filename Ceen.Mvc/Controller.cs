using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ceen.Common;

namespace Ceen.Mvc
{
	/// <summary>
	/// Implementation of a controller
	/// </summary>
	public abstract class Controller
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="T:Ceenhttpd.Mvc.Controller"/> class.
		/// </summary>
		public Controller()
		{
		}

		/// <summary>
		/// Returns the result as JSON encoded with UTF-8
		/// </summary>
		/// <param name="data">The data object to serialize.</param>
		/// <param name="disablecaching">Set to <c>true</c> to emit non-cacheable headers.</param>
		protected IResult Json(object data, bool disablecaching = true)
		{
			return new LambdaResult(ctx =>
			{
				if (disablecaching)
					ctx.Response.SetNonCacheable();
				return ctx.Response.WriteAllJsonAsync(JsonConvert.SerializeObject(data));
			});
		}
		/// <summary>
		/// Returns a text string as the result
		/// </summary>
		/// <param name="data">The string to return.</param>
		/// <param name="encoding">The encoding to use, defaults to UTF-8.</param>
		/// <param name="contenttype">The content type to use, defaults to &quot;text/plain&quot;.</param>
		/// <param name="disablecaching">Set to <c>true</c> to emit non-cacheable headers.</param>
		protected IResult Text(string data, System.Text.Encoding encoding = null, string contenttype = "text/plain", bool disablecaching = true)
		{
			encoding = encoding ?? System.Text.Encoding.UTF8;

			return new LambdaResult(ctx =>
			{
				if (disablecaching)
					ctx.Response.SetNonCacheable();
				
				return ctx.Response.WriteAllAsync(data, encoding, string.Format("{0}; charset={1}", contenttype, encoding.BodyName));
			});
		}

		/// <summary>
		/// Returns a html document as the result
		/// </summary>
		/// <param name="data">The html to return.</param>
		/// <param name="encoding">The encoding to use, defaults to UTF-8.</param>
		/// <param name="disablecaching">Set to <c>true</c> to emit non-cacheable headers.</param>
		protected IResult Html(string data, System.Text.Encoding encoding = null, bool disablecaching = true)
		{
			return Text(data, encoding, "text/html", disablecaching);
		}

		/// <summary>
		/// Sends a &quot;400 - Bad request&quot; response with an optional extra message
		/// </summary>
		/// <param name="message">An optional status message.</param>
		protected IResult BadRequest(string message = null)
		{
			return new LambdaResult(ctx =>
			{
				ctx.Response.StatusCode = HttpStatusCode.BadRequest;
				ctx.Response.StatusMessage = message ?? "Bad request";
			});
		}

		/// <summary>
		/// Sends a &quot;302 - redirect&quot; to the client
		/// </summary>
		/// <param name="url">URL.</param>
		protected IResult Redirect(string url)
		{
			return new LambdaResult(ctx =>
			{
				ctx.Response.Redirect(url);
			});
		}
	}
}
