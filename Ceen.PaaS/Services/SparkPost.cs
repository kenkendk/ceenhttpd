using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Ceen;
using Newtonsoft.Json;

namespace Ceen.PaaS.Services
{
    public class SparkPost : IModule
    {
        /// <summary>The transmission API endpoint</summary>
        public string EndpointUrl { get; set; }
        
        /// <summary>The API key to use for sending</summary>
        public string APIKey { get; set; }

        /// <summary>
        /// Creates the SparkPost instance
        /// </summary>
        public SparkPost()
        {
            LoaderContext.RegisterSingletonInstance(this);
            var secrets = LoaderContext.EnsureSingletonInstance<SecretsHandler>();
            EndpointUrl = SecretsHandler.GetSecret("SPARKPOST_URL") ?? "https://api.sparkpost.com/api/v1/transmissions";
            APIKey = SecretsHandler.GetSecret("SPARKPOST_KEY");
        }

        /// <summary>
        /// Sends the transmission message as provided
        /// </summary>
        /// <param name="transmission">The transmission to send</param>
        /// <returns>An awaitable task</returns>
        public static async Task SendEmailAsync(Transmission transmission)
        {
            var inst = LoaderContext.EnsureSingletonInstance<SparkPost>();
            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage(HttpMethod.Post, inst.EndpointUrl))
            {
                request.Headers.Add("Authorization", inst.APIKey);
                var json = JsonConvert.SerializeObject(transmission);
                using (var stringContent = new StringContent(json, Encoding.UTF8, "application/json"))
                {
                    request.Content = stringContent;
                    
                    using (var response = await client
                        .SendAsync(request, HttpCompletionOption.ResponseContentRead)
                        .ConfigureAwait(false))
                    {
                        var cnt = await response.Content.ReadAsStringAsync();
                        try
                        {
                            var resp = JsonConvert.DeserializeObject<Response>(cnt);
                            if (resp.Results == null || resp.Results.Accepted != 1)
                                throw new Exception("Bad response from SparkPost");
                            response.EnsureSuccessStatusCode();
                        }
                        catch (Exception sendex)
                        {
                            await Log.ErrorAsync($"Got error from SparkPost: {cnt}", sendex);
                            throw;
                        }
                    }
                }
            }
        }

        public class Content
        {
            [JsonProperty("from")]
            public Address From;
            [JsonProperty("subject")]
            public string Subject;
            [JsonProperty("text")]
            public string Text;
            [JsonProperty("html")]
            public string Html;
        }

        public class Options
        {
            /// <summary>
            /// Set to <c>true</c> if this is an activation or password reset email
            /// </summary>
            [JsonProperty("transactional")]
            public bool? Transactional;
        }

        public class Transmission
        {
            [JsonProperty("options")]
            public Options Options;
            [JsonProperty("content")]
            public Content Content;
            [JsonProperty("recipients")]
            public Recipient[] Recipients;
            [JsonProperty("attachments")]
            public Attachment[] Attachments;

            public static Transmission Create(string from_name, string from_email, string to_name, string to_email, string subject, string text, string html, bool transactional = false)
            {
                return new Transmission()
                {
                    Options = new Options() { Transactional = transactional },
                    Content = new Content()
                    {
                        Subject = subject,
                        Text = text,
                        Html = html,
                        From = new Address()
                        {
                            Name = from_name,
                            Email = from_email
                        }
                    },
                    Recipients = new Recipient[] {
                    new Recipient() {
                        Address = new Address() {
                            Name = to_name,
                            Email = to_email
                        }
                    }
                }
                };
            }
        }

        public class Address
        {
            [JsonProperty("name")]
            public string Name;
            [JsonProperty("email")]
            public string Email;
        }

        public class Recipient
        {
            [JsonProperty("address")]
            public Address Address;
        }

        public class Attachment
        {
            /// <summary>
            /// Attachment name
            /// </summary>
            [JsonProperty("name")]
            public string Name;
            /// <summary>
            /// The content-type header
            /// </summary>
            [JsonProperty("type")]
            public string Type;
            /// <summary>
            /// The base64 encoded payload
            /// </summary>
            [JsonProperty("data")]
            public string Data;

        }

        public class Response
        {
            [JsonProperty("results")]
            public ResponseResult Results;

            [JsonProperty("errors")]
            public ResponseError[] Errors;

        }

        public class ResponseResult
        {
            [JsonProperty("total_rejected_recipients")]
            public int Rejected;
            [JsonProperty("total_accepted_recipients")]
            public int Accepted;
            [JsonProperty("id")]
            public string Id;

            [JsonProperty("rcpt_to_errors")]
            public ResponseError[] RcptToErrors;
        }

        public class ResponseError
        {
            [JsonProperty("description")]
            public string Description;
            [JsonProperty("code")]
            public string Code;
            [JsonProperty("message")]
            public string Message;
        }        

    }
}