using System;
using System.Collections.Generic;
using System.Linq;

namespace Ceen.PaaS.Services
{
    /// <summary>
    /// Very simple templating system with just string replacing
    /// </summary>
    public static class BasicTemplating
    {
        /// <summary>
        /// Fix for missing case-insensitive replace in .NetStd2.0
        /// This is adapted from https://stackoverflow.com/questions/244531/is-there-an-alternative-to-string-replace-that-is-case-insensitive
        /// </summary>
        /// <param name="str">The string to replace within</param>
        /// <param name="oldValue">The value to be replaced</param>
        /// <param name="newValue">The value to replace it with</param>
        /// <returns>The replaced string</returns>
        private static string ReplaceCaseInsensitive(string str, string oldValue, string newValue)
        {
            if (str == null) throw new ArgumentNullException(nameof(str));
            if (oldValue == null) throw new ArgumentNullException(nameof(oldValue));
            if (oldValue.Length == 0) throw new ArgumentException("String cannot be of zero length.", nameof(oldValue));

            var position = str.IndexOf(oldValue, 0, StringComparison.OrdinalIgnoreCase);
            if (position == -1) return str;

            var sb = new System.Text.StringBuilder(str.Length);
            var lastPosition = 0;

            do
            {
                sb.Append(str, lastPosition, position - lastPosition);
                sb.Append(newValue);

            } while ((position = str.IndexOf(oldValue, lastPosition = position + oldValue.Length, StringComparison.OrdinalIgnoreCase)) != -1);

            sb.Append(str, lastPosition, str.Length - lastPosition);
            return sb.ToString();
        }

        /// <summary>
        /// Replaces the values from the dictionary in the template
        /// </summary>
        /// <param name="template">The template to update</param>
        /// <param name="values">The values to update</param>
        /// <returns>The updated template</returns>
        public static string ReplaceInTemplate(string template, Dictionary<string, string> values)
        {
            if (string.IsNullOrWhiteSpace(template))
                return template;

            foreach (var k in values)
                template = 
                    ReplaceCaseInsensitive(                        
                        ReplaceCaseInsensitive(template, 
                            "{{" + k.Key + "}}", k.Value),
                        "%7B%7B" + k.Key + "%7D%7D", k.Value                        
                    );
                    // Case-insensitive string replace is missing in .net std2.0
                    // template
                    // .Replace("{{" + k.Key + "}}", k.Value, StringComparison.OrdinalIgnoreCase)
                    // .Replace("%7B%7B" + k.Key + "%7D%7D", k.Value, StringComparison.OrdinalIgnoreCase);

            return template;
        }

        /// <summary>
        /// Replaces the values from the dictionary in the template
        /// </summary>
        /// <param name="template">The template to update</param>
        /// <param name="values">The type with the values to update</param>
        /// <typeparam name="T">The type with properties to remove</typeparam>
        /// <returns>The updated template</returns>
        public static string ReplaceInTemplate<T>(string template, T item)
        {
            return ReplaceInTemplate(template, typeof(T).GetProperties().ToDictionary(
                x => x.Name,
                x => x.GetValue(item)
            ));
        }
    }
}