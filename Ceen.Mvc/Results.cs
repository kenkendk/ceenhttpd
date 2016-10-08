using System;
using Ceen.Common;
using System.Threading.Tasks;

namespace Ceen.Mvc
{
	public interface IResult
	{
		Task Execute(IHttpContext context);
	}

	internal struct LambdaResult : IResult
	{
		public readonly Func<IHttpContext, Task> m_func;

		public LambdaResult(Func<IHttpContext, Task> func)
		{
			m_func = func;
		}

		public LambdaResult(Action<IHttpContext> func)
		{
			m_func = (ctx) =>
			{
				func(ctx);
				return Task.FromResult(true);
			};
		}

		public Task Execute(IHttpContext context)
		{
			return m_func(context);
		}
	}
}
