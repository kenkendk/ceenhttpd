using System;
using System.Threading.Tasks;

namespace Ceen.Mvc
{
	/// <summary>
	/// Interface for describing the result of an invocation.
	/// </summary>
	public interface IResult
	{
		Task Execute(IHttpContext context);
	}

	/// <summary>
	/// Result wrapper for providing an IResult from a function
	/// </summary>
	internal struct LambdaResult : IResult
	{
		/// <summary>
		/// The function to invoke
		/// </summary>
		private readonly Func<IHttpContext, Task> m_func;

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Ceen.Mvc.LambdaResult"/> struct.
		/// </summary>
		/// <param name="func">The function to invoke.</param>
		public LambdaResult(Func<Task> func)
		{
			m_func = (x) => func();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Ceen.Mvc.LambdaResult"/> struct.
		/// </summary>
		/// <param name="func">The action to invoke.</param>
		public LambdaResult(Action func)
		{
			m_func = (x) =>
			{
				func();
				return Task.FromResult(true);
			};
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Ceen.Mvc.LambdaResult"/> struct.
		/// </summary>
		/// <param name="func">The function to invoke.</param>
		public LambdaResult(Func<IHttpContext, Task> func)
		{
			m_func = func;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Ceen.Mvc.LambdaResult"/> struct.
		/// </summary>
		/// <param name="func">The action to invoke.</param>
		public LambdaResult(Action<IHttpContext> func)
		{
			m_func = (ctx) =>
			{
				func(ctx);
				return Task.FromResult(true);
			};
		}

		/// <summary>
		/// Execute the method with the specified context.
		/// </summary>
		/// <param name="context">The context to use.</param>
		public Task Execute(IHttpContext context)
		{
			return m_func(context);
		}
	}
}
