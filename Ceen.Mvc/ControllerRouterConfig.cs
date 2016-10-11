using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Ceen.Mvc
{
	/// <summary>
	/// The configuration for a controller router
	/// </summary>
	public class ControllerRouterConfig
	{
		/// <summary>
		/// The default routing template
		/// </summary>
		public const string DEFAULT_ROUTING_TEMPLATE = @"{controller}/{action=index}";

		/// <summary>
		/// Gets or sets the name of the prefix group in the template.
		/// </summary>
		public string PrefixGroupName { get; set; } = "prefix";

		/// <summary>
		/// Gets or sets the name of the controller group in the template.
		/// </summary>
		public string ControllerGroupName { get; set; } = "controller";

		/// <summary>
		/// Gets or sets the name of the action group in the template.
		/// </summary>
		public string ActionGroupName { get; set; } = "action";

		/// <summary>
		/// The template used to locate controllers
		/// </summary>
		public string Template { get; set; } = DEFAULT_ROUTING_TEMPLATE;

		/// <summary>
		/// Gets or sets a value indicating whether controller matches are case-sensitive.
		/// </summary>
		public bool CaseSensitive { get; set; } = false;

		/// <summary>
		/// Gets or sets a value indicating whether controller treats all names as lower-case.
		/// </summary>
		public bool LowerCaseNames { get; set; } = true;

		/// <summary>
		/// Gets a value indicating if the default controller, if any, can be adressed explicitly
		/// </summary>
		public bool HideDefaultController { get; set; } = true;

		/// <summary>
		/// Gets a value indicating if the default action, if any, can be adressed explicitly
		/// </summary>
		public bool HideDefaultAction { get; set; } = true;

		/// <summary>
		/// Gets or sets the suffixes to remove from the default controller names
		/// </summary>
		public string[] ControllerSuffixRemovals { get; set; } = new string[] { "Controller", "Handler" };

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Ceen.Mvc.ControllerRouterConfig"/> class.
		/// </summary>
		/// <param name="defaultController">The default controller to use.</param>
		/// <param name="defaultAction">The default action to use.</param>
		public ControllerRouterConfig(Type defaultController = null, string defaultAction = "index", bool lowerCaseNames = true)
		{
			LowerCaseNames = lowerCaseNames;

			var sb = new StringBuilder();
			if (defaultController == null)
				sb.Append($"{{{ControllerGroupName}}}");
			else
			{
				string name;
				var attr = defaultController.GetCustomAttributes(typeof(NameAttribute), false).Cast<NameAttribute>().FirstOrDefault();
				if (attr != null)
					name = attr.Name;
				else
					name = LowerCaseNames ? defaultController.Name.ToLowerInvariant() : defaultController.Name;
				sb.Append($"{{{ControllerGroupName}={name}}}");
			}

			sb.Append("/");

			if (string.IsNullOrWhiteSpace(defaultAction))
				sb.Append($"{{{ActionGroupName}}}");
			else
				sb.Append($"{{{ActionGroupName}={defaultAction}}}");

			Template = sb.ToString();
		}

		/// <summary>
		/// Creates a copy of this instance
		/// </summary>
		internal ControllerRouterConfig Clone()
		{
			return (ControllerRouterConfig)this.MemberwiseClone();
		}

	}
}
