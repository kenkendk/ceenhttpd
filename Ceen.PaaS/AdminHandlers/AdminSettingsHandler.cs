using System.Threading.Tasks;
using System.Linq;
using Ceen;
using Ceen.Mvc;
using Ceen.Database;
using Ceen.PaaS.Database;
using Newtonsoft.Json;
using System;
using System.Data;
using System.Collections.Generic;
using Ceen.PaaS.AdminHandlers;

namespace Ceen.PaaS.AdminHandlers
{
    [RequireHandler(typeof(Ceen.PaaS.Services.AdminRequiredHandler))]
    public class AdminSettingsHandler : API.ControllerBase, IAdminAPIv1
    {
        /// <summary>
        /// Helper attribute to mark a field as being a text element
        /// </summary>
        public sealed class TextSourceAttribute : System.Attribute
        {
            /// <summary>
            /// The text element
            /// </summary>
            public readonly string TextConstant;

            /// <summary>
            /// Constructs a new TextSourceAttribute
            /// </summary>
            /// <param name="textconstant">The constant to wire with</param>
            public TextSourceAttribute(string textconstant)
            {
                if (string.IsNullOrWhiteSpace(textconstant))
                    throw new ArgumentException("Blank text source not allowed", nameof(textconstant));
                TextConstant = textconstant;
            }
        }

        /// <summary>
        /// The data exchanged from the admin settings module
        /// </summary>
        public class DataExchange
        {
            /// <summary>
            /// Constructur for allowing deserialization from JSON
            /// </summary>
            public DataExchange() {}

            /// <summary>
            /// Helper method to create an instance
            /// </summary>
            /// <param name="db"></param>
            public DataExchange(IDbConnection db)
            {
                this.Images =
                    db.Select<Database.ImageMap>(x => x.CollectionID == "front-page")
                    .Select(x => new API.ImagesHandler.ResultEntry(x))
                    .OrderBy(x => x.Created)
                    .ToArray();

                foreach (var f in GetFields())
                    f.Item1.SetValue(this, Services.TextHelper.GetTextFromDb(db, f.Item2, "en"));
            }

            /// <summary>
            /// Returns all text-bound fields
            /// </summary>
            private IEnumerable<Tuple<System.Reflection.FieldInfo, string>> GetFields()
            {
                return this
                    .GetType()
                    .GetFields()
                    .Select(x => new Tuple<System.Reflection.FieldInfo, string>(
                        x,
                        x.GetCustomAttributes(false)
                            .OfType<TextSourceAttribute>()
                            .FirstOrDefault()?.TextConstant
                    ))
                    .Where(x => !string.IsNullOrWhiteSpace(x.Item2));
            }

            [JsonProperty("images")]
            public API.ImagesHandler.ResultEntry[] Images;

            [TextSource(TextConstants.LandingPageContents)]
            [JsonProperty("startPageText")]
            public string StartPageText;

            [TextSource(TextConstants.SignupConfirmationEmailSubject)]            
            [JsonProperty("emailSignupSubject")]
            public string EmailSignupSubject;
            
            [TextSource(TextConstants.SignupConfirmationEmailBody)]
            [JsonProperty("emailSignupBody")]
            public string EmailSignupBody;

            [TextSource(TextConstants.ResetPasswordEmailSubject)]
            [JsonProperty("emailResetSubject")]
            public string EmailResetSubject;
            
            [TextSource(TextConstants.ResetPasswordEmailBody)]
            [JsonProperty("emailResetBody")]
            public string EmailResetBody;
            
            [TextSource(TextConstants.ActivationEmailSubject)]
            [JsonProperty("emailActivatedSubject")]
            public string EmailActivatedSubject;
            
            [TextSource(TextConstants.ActivationEmailBody)]
            [JsonProperty("emailActivatedBody")]
            public string EmailActivatedBody;

            [TextSource(TextConstants.ChangeEmailRequestSubject)]
            [JsonProperty("emailChangeSubject")]
            public string EmailChangeSubject;

            [TextSource(TextConstants.ChangeEmailRequestBody)]
            [JsonProperty("emailChangeBody")]
            public string EmailChangeBody;

            [TextSource(TextConstants.PrivacyPolicy)]
            [JsonProperty("privacyPolicy")]
            public string PrivacyPolicy;

            [TextSource(TextConstants.TermsOfService)]
            [JsonProperty("termsOfService")]
            public string TermsOfService;

            /// <summary>
            /// Patches the database with the text items sent
            /// </summary>
            public void Patch(IDbConnection db)
            {
                foreach (var f in GetFields().Where(x => !string.IsNullOrWhiteSpace(x.Item1.GetValue(this) as string)))
                {
                    var str = f.Item1.GetValue(this) as string;
                    var e = db.SelectSingle<TextEntry>(x => x.ID == f.Item2);
                    if (e == null)
                    {
                        db.InsertItem(new TextEntry() {
                            ID = f.Item2,
                            Text = str,
                            IsDraft = false,
                            Language = "en"
                        });
                    }
                    else
                    {
                        e.Text = str;
                        e.Language = "en";
                        e.IsDraft = false;
                        db.UpdateItem(e);
                    }

                    // Mark the contents as stale
                    if (f.Item2 == TextConstants.LandingPageContents)
                        Task.Run(() => Cache.InvalidateMainIndexHtmlAsync());
                    else if (f.Item2 == TextConstants.PrivacyPolicy)
                        Task.Run(() => Cache.PrivacyPolicy.InvalidateAsync());
                    else if (f.Item2 == TextConstants.TermsOfService)
                        Task.Run(() => Cache.TermsOfService.InvalidateAsync());
                }
            }
        }

        [HttpGet]
        [Ceen.Mvc.Name("index")]
        public async Task<IResult> Get()
        {
            if (!await Services.AdminHelper.IsAdminAsync(Context.Request.UserID))
                return Forbidden;

            return Json(await DB.RunInTransactionAsync(db => new DataExchange(db)));
        }

        [HttpPatch]
        [Ceen.Mvc.Name("index")]
        public async Task<IResult> Patch(DataExchange data)
        {
            if (!await Services.AdminHelper.IsAdminAsync(Context.Request.UserID))
                return Forbidden;
            if (data == null)
                return BadRequest;

            await DB.RunInTransactionAsync(db => data.Patch(db));

            return OK;
        }

    }
}