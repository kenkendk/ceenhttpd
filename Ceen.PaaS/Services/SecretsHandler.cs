using System.IO;
using System.Collections.Generic;
namespace Ceen.PaaS
{
    /// <summary>
    /// Helper class for storing service tokens in a way that they are not leaked
    /// when sharing the source code
    /// </summary>
    public class SecretsHandler : IModule
    {
        /// <summary>
        /// The name of the secrets file
        /// </summary>
        public string Filename { get; set; } = "secrets.txt";

        /// <summary>
        /// The lock guarding the table
        /// </summary>
        private readonly object m_lock = new object();

        /// <summary>
        /// The list of secrets
        /// </summary>
        private Dictionary<string, string> m_secrets;

        /// <summary>
        /// Creates a new SecretsHandler
        /// </summary>
        public SecretsHandler()
        {
            LoaderContext.RegisterSingletonInstance(this);
        }

        /// <summary>
        /// Gets the named secret, or returns null.
        /// Note that this method will clear the value, so it can only be obtained ONCE
        /// </summary>
        /// <param name="name">The name of the secret to get</param>
        /// <returns>The value or null</returns>
        public static string GetSecret(string name)
        {
            var self = LoaderContext.EnsureSingletonInstance<SecretsHandler>();
            lock(self.m_lock)
            {
                if (self.m_secrets == null)
                {
                    var secrets = new Dictionary<string, string>();
                    if (File.Exists(self.Filename))
                    {
                        foreach(var n in File.ReadAllLines(self.Filename))
                        {
                            if (string.IsNullOrWhiteSpace(n))
                                continue;

                            var parts = n.Split(new char[] { '=' }, 2);
                            if (parts.Length != 2)
                                continue;

                            var key = parts[0].Trim();
                            var value = parts[1].Trim();
                            if (key.StartsWith("#"))
                                continue;

                            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                                continue;

                            secrets[key] = value;
                        }
                    }

                    self.m_secrets = secrets;
                }
                
                self.m_secrets.TryGetValue(name, out var v);
                self.m_secrets.Remove(name);

                return string.IsNullOrWhiteSpace(v)
                    ? null
                    : v;
            }
        }

    }
}