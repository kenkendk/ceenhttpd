using System;
using System.Linq;
using System.Threading.Tasks;
using Ceen;
using Newtonsoft.Json;

namespace Ceen.PaaS.API
{
    // TODO: Remove this class entirely?
    // It can be replaced with a single method

    /// <summary>
    /// Helper class for placing a description on an enum field
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public class DescriptionAttribute : Attribute
    {
        /// <summary>
        /// The text to display
        /// </summary>
        public readonly string Text;
        /// <summary>
        /// The locale the string is using
        /// </summary>
        public readonly string Locale;

        /// <summary>
        /// Creates a new description attribute
        /// </summary>
        /// <param name="locale">The locale to set</param>
        /// <param name="text">The text to use</param>
        public DescriptionAttribute(string locale, string text)
        {
            if (locale != null && locale.Length != 2)
                throw new ArgumentException("Currently, only 2-letter ISO names are supported");
            Locale = locale;
            Text = text ?? throw new ArgumentNullException(nameof(text));
        }
    }

    /// <summary>
    /// Attribute for placing a string constant value on an attribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class CodeAttribute : Attribute
    {
        /// <summary>
        /// The code constant to use
        /// </summary>
        public readonly string Code;

        /// <summary>
        /// Constructs a new code attribute
        /// </summary>
        /// <param name="code">The code text to use</param>
        public CodeAttribute(string code)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
        }
    }


    /// <summary>
    /// Helper class for responding with a status code and a display text
    /// </summary>
    /// <typeparam name="T">The enum with status codes</typeparam>
    public abstract class SignalResponseBase<T> : Ceen.Mvc.IResult
        where T : struct, IComparable
    {
        /// <summary>
        /// Static check that the T type is an enum type
        /// </summary>
        private static bool _isEnum = typeof(T).IsEnum ? true : throw new InvalidCastException($"The type {typeof(T)} is not an enum");

        /// <summary>
        /// The code to report
        /// </summary>
        public readonly string Code;
        /// <summary>
        /// The message to report
        /// </summary>
        public readonly string Message;

        /// <summary>
        /// The code assigned
        /// </summary>
        public readonly T CodeValue;

        /// <summary>
        /// Constructs a new signal response with the given code and culture
        /// </summary>
        /// <param name="code">The code to respond with</param>
        /// <param name="language">The culture used to select a status message</param>
        /// <param name="messageoverride">A custom (localized) message to use</name>
        public SignalResponseBase(T code, string language, string messageoverride = null)
        {
            CodeValue = code;

            var field = typeof(T).GetFields()
                .Where(x => x.FieldType.IsEnum)
                .FirstOrDefault(x => object.Equals(x.GetValue(null), code));

            if (field == null)
                throw new ArgumentException($"Failed to find {code} in {typeof(T)}");

            // Look for a [Code] attribute, or use the field name
            Code = field.GetCustomAttributes(typeof(CodeAttribute), true)
                .OfType<CodeAttribute>()
                .Select(x => x.Code)
                .FirstOrDefault()
                ?? field.Name;

            if (!string.IsNullOrWhiteSpace(messageoverride))
                Message = messageoverride;
            else
                // Look for a [Description] attribute with the desired locale, a null locale, or use the field name
                Message = field.GetCustomAttributes(typeof(DescriptionAttribute), true)
                    .OfType<DescriptionAttribute>()
                    .Where(x => (x.Locale == language && !string.IsNullOrWhiteSpace(language)) || x.Locale == null)
                    .OrderByDescending(x => x.Locale)
                    .Select(x => x.Text)
                    .FirstOrDefault()
                    ?? field.Name;
        }

        /// <summary>
        /// Execute the method with the specified context.
        /// </summary>
        /// <param name="context">The context to use.</param>
        public Task Execute(IHttpContext context)
        {
            // We do not use the HTTP status codes
            context.Response.StatusCode = HttpStatusCode.OK;
            context.Response.StatusMessage = HttpStatusMessages.DefaultMessage(HttpStatusCode.OK);
            context.Response.SetNonCacheable();
            return context.Response.WriteAllJsonAsync(JsonConvert.SerializeObject(this));
        }

    }
}