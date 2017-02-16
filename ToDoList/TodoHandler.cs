using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ceen;
using Ceen.Mvc;
using Newtonsoft.Json;

namespace ToDoList
{
	[Name("api")]
	public interface IAPI : IControllerPrefix { }

	[Name("v1")]
	public interface IApiV1 : IAPI { }

	// We assign two prefixes to the controller so we can test 
	// it without XSRF protection
	// 
	// It will still require a login handler, so we have 
	// configured one that does not check XSRF tokens
	// in the config file

	[Name("debug")]
	public interface IDebugAPI : IControllerPrefix { }

	[Name("v1")]
	public interface IDebugApiV1 : IDebugAPI { }


	/// <summary>
	/// The /api/v1/todolist entry.
	/// 
	/// Note: this is only for demonstation purposes,
	/// you should never implement it like this in a production
	/// environment, as the user can loose data if they are using
	/// two different browser sessions.
	/// </summary>
	[Name("todolist")]
	[RequireHandler(typeof(Ceen.Security.Login.LoginRequiredHandler))]
	public class TodoHandler : Controller, IApiV1 
		, IDebugApiV1
	{
		/// <summary>
		/// The class representing the items we store
		/// </summary>
		private class ToDoItem
		{
			/// <summary>
			/// The text of the item
			/// </summary>
			[JsonProperty("text")]
			public string Text { get; set; }
			/// <summary>
			/// A value indicating if the item is completed
			/// </summary>
			[JsonProperty("completed")]
			public bool Completed { get; set; }
		}
		/// <summary>
		/// The name of the on-disk file with todo items
		/// </summary>
		private const string DATAFILE = "items.json";
		/// <summary>
		/// The folder where all user data is kept
		/// </summary>
		private static readonly string STORAGE_FOLDER;

		/// <summary>
		/// Static initializer to help set up the storage folder from environment variables
		/// </summary>
		static TodoHandler()
		{
			var env = Environment.GetEnvironmentVariable("DATA_FOLDER");
			if (string.IsNullOrWhiteSpace(env))
				env = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), "data");

			if (!Directory.Exists(env))
				Directory.CreateDirectory(env);

			STORAGE_FOLDER = env;
		}

		/// <summary>
		/// Gets a userID from a context and validates that the ID can be used as a directory name
		/// </summary>
		/// <returns>The user identifier.</returns>
		/// <param name="context">The context to get the ID from.</param>
		private string GetUserID(IHttpContext context)
		{
			var user = context.Request.UserID;
			if (string.IsNullOrWhiteSpace(user))
				throw new Exception("Something is wrong, the API was called without a valid user");
			if (user.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
				throw new Exception($"The userid \"{user}\" has invalid characters");

			return user;
		}

		/// <summary>
		/// Gets the path to the storage file for the current user
		/// </summary>
		/// <returns>The user data path.</returns>
		/// <param name="context">The context to get the ID from.</param>
		private string GetUserDataPath(IHttpContext context)
		{
			var folder = Path.Combine(STORAGE_FOLDER, GetUserID(context));
			if (!Directory.Exists(folder))
				Directory.CreateDirectory(folder);

			return Path.Combine(folder, DATAFILE);
		}

		/// <summary>
		/// Helper method to write the combined set of items to disk
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="context">The http context.</param>
		/// <param name="items">The items to write.</param>
		private async Task WriteDataToFile(IHttpContext context, ToDoItem[] items)
		{
			if (items == null)
				throw new Exception("Data cannot be null, it must be an empty array");

			using (var fs = new FileStream(GetUserDataPath(context), FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
			{
				fs.SetLength(0);
				using (var sw = new StreamWriter(fs, System.Text.Encoding.UTF8))
					await sw.WriteAsync(JsonConvert.SerializeObject(items));
			}
		}

		/// <summary>
		/// Reads stored data from the file.
		/// </summary>
		/// <returns>The data from file.</returns>
		/// <param name="context">The http context.</param>
		private async Task<ToDoItem[]> ReadDataFromFile(IHttpContext context)
		{
			var path = GetUserDataPath(context);
			if (!File.Exists(path))
				return new ToDoItem[0];
			else
			{
				// We could just dump the file contents to the output,
				// but that could help an attacker with dumping any file
				// if there is a configuration problem somewhere

				using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
				using (var sr = new StreamReader(fs, System.Text.Encoding.UTF8, true))
					return JsonConvert.DeserializeObject<ToDoItem[]>(await sr.ReadToEndAsync());
			}
		}


		[HttpGet]
		[Name("index")]
		/// <summary>
		/// Handles a GET request, using the &quot;index&quot; name,
		/// because we want two different functions to handle
		/// PUT, POST and GET, and they cannot all be called &quot;Index&quot;.
		/// </summary>
		/// <param name="context">The http context.</param>
		public async Task<IResult> Get(IHttpContext context)
		{
			return Json(await ReadDataFromFile(context));
		}

		[HttpPut]
		[Name("index")]
		/// <summary>
		/// Handles a PUT request, using the &quot;index&quot; name,
		/// because we want two different functions to handle
		/// PUT, POST and GET, and they cannot all be called &quot;Index&quot;.
		/// </summary>
		/// <param name="context">The http context.</param>
		public async Task<IResult> Put(IHttpContext context)
		{
			// We could just dump the input stream to a file, but that would allow storage
			// of arbitrary content. Instead we parse it to make sure it is valid, and then 
			// write the parsed contents to disk

			using (var sr = new StreamReader(context.Request.Body, System.Text.Encoding.UTF8, false))
			{
				var str = await sr.ReadToEndAsync();
				var data = JsonConvert.DeserializeObject<ToDoItem[]>(str);

				await WriteDataToFile(context, data);
			}

			return OK;				
		}

		[HttpPost]
		[Name("index")]
		/// <summary>
		/// Handles a PUT request, using the &quot;index&quot; name,
		/// because we want two different functions to handle
		/// PUT, POST and GET, and they cannot all be called &quot;Index&quot;.
		/// </summary>
		/// <param name="context">The http context.</param>
		public async Task<IResult> Post(IHttpContext context)
		{
			using (var sr = new StreamReader(context.Request.Body, System.Text.Encoding.UTF8, false))
			{
				var str = await sr.ReadToEndAsync();
				var data = JsonConvert.DeserializeObject<ToDoItem>(str);

				var current = await ReadDataFromFile(context);
				Array.Resize(ref current, current.Length + 1);
				current[current.Length - 1] = data;

				await WriteDataToFile(context, current);
			}

			return OK;
		}

		[HttpGet]
		[Route("{index}")]
		[Name("index")]
		/// <summary>
		/// Gets a todo item based on its index
		/// </summary>
		/// <returns>The HTTP results.</returns>
		/// <param name="context">The http context.</param>
		/// <param name="index">The index of the item, we limit it to only allow this to be specified via the path.</param>
		public async Task<IResult> GetDetail(IHttpContext context, [Parameter(ParameterSource.Url)] int index)
		{
			if (index < 0)
				return Status(HttpStatusCode.BadRequest, "Invalid todo ID");
			var data = await ReadDataFromFile(context);
			if (index >= data.Length)
				return Status(HttpStatusCode.BadRequest, "Invalid todo ID");

			return Json(data[index]);
		}

		[HttpPut]
		[Route("{index}")]
		[Name("index")]
		/// <summary>
		/// Updates a todo item based on its index
		/// </summary>
		/// <returns>The HTTP results.</returns>
		/// <param name="context">The http context.</param>
		/// <param name="index">The index of the item, we limit it to only allow this to be specified via the path.</param>
		public async Task<IResult> PutDetail(IHttpContext context, int index)
		{
			if (index < 0)
				return Status(HttpStatusCode.BadRequest, "Invalid todo ID");
			var data = await ReadDataFromFile(context);
			if (index >= data.Length)
				return Status(HttpStatusCode.BadRequest, "Invalid todo ID");

			using (var sr = new StreamReader(context.Request.Body, System.Text.Encoding.UTF8, false))
			{
				var str = await sr.ReadToEndAsync();
				var item = JsonConvert.DeserializeObject<ToDoItem>(str);

				data[index] = item;
			}

			await WriteDataToFile(context, data);

			return OK;
		}

		[HttpDelete]
		[Route("{index}")]
		[Name("index")]
		/// <summary>
		/// Deletes a todo item based on its index
		/// </summary>
		/// <returns>The HTTP results.</returns>
		/// <param name="context">The http context.</param>
		/// <param name="index">The index of the item, we limit it to only allow this to be specified via the path.</param>
		public async Task<IResult> DeleteDetail(IHttpContext context, int index)
		{
			if (index < 0)
				return Status(HttpStatusCode.BadRequest, "Invalid todo ID");
			var data = await ReadDataFromFile(context);
			if (index >= data.Length)
				return Status(HttpStatusCode.BadRequest, "Invalid todo ID");

			var lst = data.ToList();
			lst.RemoveAt(index);
			await WriteDataToFile(context, data);

			return OK;
		}
	}
}
