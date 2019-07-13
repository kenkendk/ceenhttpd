using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace Ceen.Mvc
{
    /// <summary>
    /// A controller that does not rely on attributes,
    /// but generates the routes at runtime
    /// </summary>
    public class ManualRoutingController : Controller
    {
        /// <summary>
        /// The list of dynamic routes
        /// </summary>
        public IEnumerable<PartialParsedRoute> Routes => m_routes;

        /// <summary>
        /// The partially parsed routes
        /// </summary>
        private List<PartialParsedRoute> m_routes = new List<PartialParsedRoute>();

        /// <summary>
        /// Adds a dynamic route to the list of routes
        /// </summary>
        /// <param name="route"></param>
        public ManualRoutingController AddRoute(PartialParsedRoute route)
        {
            m_routes.Add(route ?? throw new ArgumentNullException(nameof(route)));
            return this;
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path</param>
        /// <param name="verbs">The verbs allowed for the method</param>
        /// <param name="method">The method to invoke</param>
        public ManualRoutingController AddRoute(string path, string[] verbs, MethodInfo method)
        {
            return AddRoute(new PartialParsedRoute()
            {
                MethodPath = path,
                Verbs = verbs,
                Method = method ?? throw new ArgumentNullException(nameof(method))
            });
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        public ManualRoutingController AddDelegateRoute(string path, Delegate method)
        {
            var items = path?.Split(new char[] { ' ' }, 2);
            if (items == null || items.Length != 2 || items[0].IndexOf("/") >= 0 || !items[1].StartsWith("/"))
                throw new ArgumentException($"The path must be of the form \"VERB /path\"", nameof(path));

            var verbs = items[0].Split(new string[] { "|" }, StringSplitOptions.RemoveEmptyEntries);

            return AddRoute(items[1], verbs, method.GetMethodInfo());
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="verbs">The verbs allowed for the method</param>
        /// <param name="method">The method to invoke</param>
        public ManualRoutingController AddRoute(string path, Func<IResult> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="verbs">The verbs allowed for the method</param>
        /// <param name="method">The method to invoke</param>
        public ManualRoutingController AddRoute(string path, Func<Task<IResult>> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="verbs">The verbs allowed for the method</param>
        /// <param name="method">The method to invoke</param>
        public ManualRoutingController AddRoute(string path, Func<Task> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="verbs">The verbs allowed for the method</param>
        /// <param name="method">The method to invoke</param>
        public ManualRoutingController AddRoute(string path, Action method)
        {
            return AddDelegateRoute(path, method);
        }


        #region Typed function overloads

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        public ManualRoutingController AddRoute<T1>(string path, Func<T1, IResult> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
		/// <typeparam name="T2">The second argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2>(string path, Func<T1, T2, IResult> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
		/// <typeparam name="T2">The second argument type</typeparam>
		/// <typeparam name="T2">The3secondthirdent type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3>(string path, Func<T1, T2, T3, IResult> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4>(string path, Func<T1, T2, T3, T4, IResult> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5>(string path, Func<T1, T2, T3, T4, T5, IResult> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6>(string path, Func<T1, T2, T3, T4, T5, T6, IResult> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7>(string path, Func<T1, T2, T3, T4, T5, T6, T7, IResult> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        /// <typeparam name="T8">The eighth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7, T8>(string path, Func<T1, T2, T3, T4, T5, T6, T7, T8, IResult> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        /// <typeparam name="T8">The eighth argument type</typeparam>
        /// <typeparam name="T9">The nineth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7, T8, T9>(string path, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, IResult> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        /// <typeparam name="T8">The eighth argument type</typeparam>
        /// <typeparam name="T9">The nineth argument type</typeparam>
        /// <typeparam name="T10">The tenth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(string path, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, IResult> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        /// <typeparam name="T8">The eighth argument type</typeparam>
        /// <typeparam name="T9">The nineth argument type</typeparam>
        /// <typeparam name="T10">The tenth argument type</typeparam>
        /// <typeparam name="T11">The eleventh argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(string path, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, IResult> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        /// <typeparam name="T8">The eighth argument type</typeparam>
        /// <typeparam name="T9">The nineth argument type</typeparam>
        /// <typeparam name="T10">The tenth argument type</typeparam>
        /// <typeparam name="T11">The eleventh argument type</typeparam>
        /// <typeparam name="T12">The twelveth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(string path, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, IResult> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        /// <typeparam name="T8">The eighth argument type</typeparam>
        /// <typeparam name="T9">The nineth argument type</typeparam>
        /// <typeparam name="T10">The tenth argument type</typeparam>
        /// <typeparam name="T11">The eleventh argument type</typeparam>
        /// <typeparam name="T12">The twelveth argument type</typeparam>
        /// <typeparam name="T13">The thirteenth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(string path, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, IResult> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        /// <typeparam name="T8">The eighth argument type</typeparam>
        /// <typeparam name="T9">The nineth argument type</typeparam>
        /// <typeparam name="T10">The tenth argument type</typeparam>
        /// <typeparam name="T11">The eleventh argument type</typeparam>
        /// <typeparam name="T12">The twelveth argument type</typeparam>
        /// <typeparam name="T13">The thirteenth argument type</typeparam>
        /// <typeparam name="T14">The fourteenth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(string path, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, IResult> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        /// <typeparam name="T8">The eighth argument type</typeparam>
        /// <typeparam name="T9">The nineth argument type</typeparam>
        /// <typeparam name="T10">The tenth argument type</typeparam>
        /// <typeparam name="T11">The eleventh argument type</typeparam>
        /// <typeparam name="T12">The twelveth argument type</typeparam>
        /// <typeparam name="T13">The thirteenth argument type</typeparam>
        /// <typeparam name="T14">The fourteenth argument type</typeparam>
        /// <typeparam name="T15">The fifteenth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(string path, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, IResult> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        /// <typeparam name="T8">The eighth argument type</typeparam>
        /// <typeparam name="T9">The nineth argument type</typeparam>
        /// <typeparam name="T10">The tenth argument type</typeparam>
        /// <typeparam name="T11">The eleventh argument type</typeparam>
        /// <typeparam name="T12">The twelveth argument type</typeparam>
        /// <typeparam name="T13">The thirteenth argument type</typeparam>
        /// <typeparam name="T14">The fourteenth argument type</typeparam>
        /// <typeparam name="T15">The fifteenth argument type</typeparam>
        /// <typeparam name="T16">The sixteenth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(string path, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, IResult> method)
        {
            return AddDelegateRoute(path, method);
        }


        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        public ManualRoutingController AddRoute<T1>(string path, Func<T1, Task<IResult>> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
		/// <typeparam name="T2">The second argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2>(string path, Func<T1, T2, Task<IResult>> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
		/// <typeparam name="T2">The second argument type</typeparam>
		/// <typeparam name="T2">The3secondthirdent type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3>(string path, Func<T1, T2, T3, Task<IResult>> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4>(string path, Func<T1, T2, T3, T4, Task<IResult>> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5>(string path, Func<T1, T2, T3, T4, T5, Task<IResult>> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6>(string path, Func<T1, T2, T3, T4, T5, T6, Task<IResult>> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7>(string path, Func<T1, T2, T3, T4, T5, T6, T7, Task<IResult>> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        /// <typeparam name="T8">The eighth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7, T8>(string path, Func<T1, T2, T3, T4, T5, T6, T7, T8, Task<IResult>> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        /// <typeparam name="T8">The eighth argument type</typeparam>
        /// <typeparam name="T9">The nineth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7, T8, T9>(string path, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, Task<IResult>> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        /// <typeparam name="T8">The eighth argument type</typeparam>
        /// <typeparam name="T9">The nineth argument type</typeparam>
        /// <typeparam name="T10">The tenth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(string path, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, Task<IResult>> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        /// <typeparam name="T8">The eighth argument type</typeparam>
        /// <typeparam name="T9">The nineth argument type</typeparam>
        /// <typeparam name="T10">The tenth argument type</typeparam>
        /// <typeparam name="T11">The eleventh argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(string path, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, Task<IResult>> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        /// <typeparam name="T8">The eighth argument type</typeparam>
        /// <typeparam name="T9">The nineth argument type</typeparam>
        /// <typeparam name="T10">The tenth argument type</typeparam>
        /// <typeparam name="T11">The eleventh argument type</typeparam>
        /// <typeparam name="T12">The twelveth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(string path, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, Task<IResult>> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        /// <typeparam name="T8">The eighth argument type</typeparam>
        /// <typeparam name="T9">The nineth argument type</typeparam>
        /// <typeparam name="T10">The tenth argument type</typeparam>
        /// <typeparam name="T11">The eleventh argument type</typeparam>
        /// <typeparam name="T12">The twelveth argument type</typeparam>
        /// <typeparam name="T13">The thirteenth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(string path, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, Task<IResult>> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        /// <typeparam name="T8">The eighth argument type</typeparam>
        /// <typeparam name="T9">The nineth argument type</typeparam>
        /// <typeparam name="T10">The tenth argument type</typeparam>
        /// <typeparam name="T11">The eleventh argument type</typeparam>
        /// <typeparam name="T12">The twelveth argument type</typeparam>
        /// <typeparam name="T13">The thirteenth argument type</typeparam>
        /// <typeparam name="T14">The fourteenth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(string path, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, Task<IResult>> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        /// <typeparam name="T8">The eighth argument type</typeparam>
        /// <typeparam name="T9">The nineth argument type</typeparam>
        /// <typeparam name="T10">The tenth argument type</typeparam>
        /// <typeparam name="T11">The eleventh argument type</typeparam>
        /// <typeparam name="T12">The twelveth argument type</typeparam>
        /// <typeparam name="T13">The thirteenth argument type</typeparam>
        /// <typeparam name="T14">The fourteenth argument type</typeparam>
        /// <typeparam name="T15">The fifteenth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(string path, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, Task<IResult>> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        /// <typeparam name="T8">The eighth argument type</typeparam>
        /// <typeparam name="T9">The nineth argument type</typeparam>
        /// <typeparam name="T10">The tenth argument type</typeparam>
        /// <typeparam name="T11">The eleventh argument type</typeparam>
        /// <typeparam name="T12">The twelveth argument type</typeparam>
        /// <typeparam name="T13">The thirteenth argument type</typeparam>
        /// <typeparam name="T14">The fourteenth argument type</typeparam>
        /// <typeparam name="T15">The fifteenth argument type</typeparam>
        /// <typeparam name="T16">The sixteenth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(string path, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, Task<IResult>> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        public ManualRoutingController AddRoute<T1>(string path, Func<T1, Task> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
		/// <typeparam name="T2">The second argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2>(string path, Func<T1, T2, Task> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
		/// <typeparam name="T2">The second argument type</typeparam>
		/// <typeparam name="T2">The3secondthirdent type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3>(string path, Func<T1, T2, T3, Task> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4>(string path, Func<T1, T2, T3, T4, Task> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5>(string path, Func<T1, T2, T3, T4, T5, Task> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6>(string path, Func<T1, T2, T3, T4, T5, T6, Task> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7>(string path, Func<T1, T2, T3, T4, T5, T6, T7, Task> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        /// <typeparam name="T8">The eighth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7, T8>(string path, Func<T1, T2, T3, T4, T5, T6, T7, T8, Task> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        /// <typeparam name="T8">The eighth argument type</typeparam>
        /// <typeparam name="T9">The nineth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7, T8, T9>(string path, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, Task> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        /// <typeparam name="T8">The eighth argument type</typeparam>
        /// <typeparam name="T9">The nineth argument type</typeparam>
        /// <typeparam name="T10">The tenth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(string path, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, Task> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        /// <typeparam name="T8">The eighth argument type</typeparam>
        /// <typeparam name="T9">The nineth argument type</typeparam>
        /// <typeparam name="T10">The tenth argument type</typeparam>
        /// <typeparam name="T11">The eleventh argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(string path, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, Task> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        /// <typeparam name="T8">The eighth argument type</typeparam>
        /// <typeparam name="T9">The nineth argument type</typeparam>
        /// <typeparam name="T10">The tenth argument type</typeparam>
        /// <typeparam name="T11">The eleventh argument type</typeparam>
        /// <typeparam name="T12">The twelveth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(string path, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, Task> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        /// <typeparam name="T8">The eighth argument type</typeparam>
        /// <typeparam name="T9">The nineth argument type</typeparam>
        /// <typeparam name="T10">The tenth argument type</typeparam>
        /// <typeparam name="T11">The eleventh argument type</typeparam>
        /// <typeparam name="T12">The twelveth argument type</typeparam>
        /// <typeparam name="T13">The thirteenth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(string path, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, Task> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        /// <typeparam name="T8">The eighth argument type</typeparam>
        /// <typeparam name="T9">The nineth argument type</typeparam>
        /// <typeparam name="T10">The tenth argument type</typeparam>
        /// <typeparam name="T11">The eleventh argument type</typeparam>
        /// <typeparam name="T12">The twelveth argument type</typeparam>
        /// <typeparam name="T13">The thirteenth argument type</typeparam>
        /// <typeparam name="T14">The fourteenth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(string path, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, Task> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        /// <typeparam name="T8">The eighth argument type</typeparam>
        /// <typeparam name="T9">The nineth argument type</typeparam>
        /// <typeparam name="T10">The tenth argument type</typeparam>
        /// <typeparam name="T11">The eleventh argument type</typeparam>
        /// <typeparam name="T12">The twelveth argument type</typeparam>
        /// <typeparam name="T13">The thirteenth argument type</typeparam>
        /// <typeparam name="T14">The fourteenth argument type</typeparam>
        /// <typeparam name="T15">The fifteenth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(string path, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, Task> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        /// <typeparam name="T8">The eighth argument type</typeparam>
        /// <typeparam name="T9">The nineth argument type</typeparam>
        /// <typeparam name="T10">The tenth argument type</typeparam>
        /// <typeparam name="T11">The eleventh argument type</typeparam>
        /// <typeparam name="T12">The twelveth argument type</typeparam>
        /// <typeparam name="T13">The thirteenth argument type</typeparam>
        /// <typeparam name="T14">The fourteenth argument type</typeparam>
        /// <typeparam name="T15">The fifteenth argument type</typeparam>
        /// <typeparam name="T16">The sixteenth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(string path, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, Task> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        public ManualRoutingController AddRoute<T1>(string path, Action<T1> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
		/// <typeparam name="T2">The second argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2>(string path, Action<T1, T2> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
		/// <typeparam name="T2">The second argument type</typeparam>
		/// <typeparam name="T2">The3secondthirdent type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3>(string path, Action<T1, T2, T3> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4>(string path, Action<T1, T2, T3, T4> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5>(string path, Action<T1, T2, T3, T4, T5> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6>(string path, Action<T1, T2, T3, T4, T5, T6> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7>(string path, Action<T1, T2, T3, T4, T5, T6, T7> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        /// <typeparam name="T8">The eighth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7, T8>(string path, Action<T1, T2, T3, T4, T5, T6, T7, T8> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        /// <typeparam name="T8">The eighth argument type</typeparam>
        /// <typeparam name="T9">The nineth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7, T8, T9>(string path, Action<T1, T2, T3, T4, T5, T6, T7, T8, T9> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        /// <typeparam name="T8">The eighth argument type</typeparam>
        /// <typeparam name="T9">The nineth argument type</typeparam>
        /// <typeparam name="T10">The tenth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(string path, Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        /// <typeparam name="T8">The eighth argument type</typeparam>
        /// <typeparam name="T9">The nineth argument type</typeparam>
        /// <typeparam name="T10">The tenth argument type</typeparam>
        /// <typeparam name="T11">The eleventh argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(string path, Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        /// <typeparam name="T8">The eighth argument type</typeparam>
        /// <typeparam name="T9">The nineth argument type</typeparam>
        /// <typeparam name="T10">The tenth argument type</typeparam>
        /// <typeparam name="T11">The eleventh argument type</typeparam>
        /// <typeparam name="T12">The twelveth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(string path, Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        /// <typeparam name="T8">The eighth argument type</typeparam>
        /// <typeparam name="T9">The nineth argument type</typeparam>
        /// <typeparam name="T10">The tenth argument type</typeparam>
        /// <typeparam name="T11">The eleventh argument type</typeparam>
        /// <typeparam name="T12">The twelveth argument type</typeparam>
        /// <typeparam name="T13">The thirteenth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(string path, Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        /// <typeparam name="T8">The eighth argument type</typeparam>
        /// <typeparam name="T9">The nineth argument type</typeparam>
        /// <typeparam name="T10">The tenth argument type</typeparam>
        /// <typeparam name="T11">The eleventh argument type</typeparam>
        /// <typeparam name="T12">The twelveth argument type</typeparam>
        /// <typeparam name="T13">The thirteenth argument type</typeparam>
        /// <typeparam name="T14">The fourteenth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(string path, Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        /// <typeparam name="T8">The eighth argument type</typeparam>
        /// <typeparam name="T9">The nineth argument type</typeparam>
        /// <typeparam name="T10">The tenth argument type</typeparam>
        /// <typeparam name="T11">The eleventh argument type</typeparam>
        /// <typeparam name="T12">The twelveth argument type</typeparam>
        /// <typeparam name="T13">The thirteenth argument type</typeparam>
        /// <typeparam name="T14">The fourteenth argument type</typeparam>
        /// <typeparam name="T15">The fifteenth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(string path, Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> method)
        {
            return AddDelegateRoute(path, method);
        }

        /// <summary>
        /// Adds a new fully specified route
        /// </summary>
        /// <param name="path">The full path to use for the method prefixed with the verb</param>
        /// <param name="method">The method to invoke</param>
        /// <typeparam name="T1">The first argument type</typeparam>
        /// <typeparam name="T2">The second argument type</typeparam>
        /// <typeparam name="T3">The third argument type</typeparam>
        /// <typeparam name="T4">The fourth argument type</typeparam>
        /// <typeparam name="T5">The fifth argument type</typeparam>
        /// <typeparam name="T6">The sixth argument type</typeparam>
        /// <typeparam name="T7">The seventh argument type</typeparam>
        /// <typeparam name="T8">The eighth argument type</typeparam>
        /// <typeparam name="T9">The nineth argument type</typeparam>
        /// <typeparam name="T10">The tenth argument type</typeparam>
        /// <typeparam name="T11">The eleventh argument type</typeparam>
        /// <typeparam name="T12">The twelveth argument type</typeparam>
        /// <typeparam name="T13">The thirteenth argument type</typeparam>
        /// <typeparam name="T14">The fourteenth argument type</typeparam>
        /// <typeparam name="T15">The fifteenth argument type</typeparam>
        /// <typeparam name="T16">The sixteenth argument type</typeparam>
        public ManualRoutingController AddRoute<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(string path, Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> method)
        {
            return AddDelegateRoute(path, method);
        }
        
        #endregion

    }
}
