using Ceen;
using Ceen.Mvc;
using Ceen.Database;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace Ceen.PaaS.AdminHandlers
{
    [RequireHandler(typeof(Ceen.PaaS.Services.AdminRequiredHandler))]
    public class TextHandler : API.ControllerBase, IAdminAPIv1
    {
        public enum TextID
        {
            LandingPage,
            
            PrivacyPolicy,
            TermsOfService,
            PersonalDataPolicy,
            
            SignupConfirmation,
            Activation,
            PasswordReset,
            ChangeEmail
        }

        private readonly static Dictionary<TextID, string> _bodyConstants = new Dictionary<TextID, string>{
            { TextID.LandingPage, TextConstants.LandingPageContents },
            
            { TextID.PrivacyPolicy, TextConstants.PrivacyPolicy },
            { TextID.TermsOfService, TextConstants.TermsOfService },
            { TextID.PersonalDataPolicy, TextConstants.PersonDataPolicy },

            { TextID.SignupConfirmation, TextConstants.SignupConfirmationEmailBody },
            { TextID.Activation, TextConstants.ActivationEmailBody },
            { TextID.PasswordReset, TextConstants.ResetPasswordEmailBody },
            { TextID.ChangeEmail, TextConstants.ChangeEmailRequestBody },
        };

        private readonly static Dictionary<TextID, string> _subjectConstants = new Dictionary<TextID, string>{
            { TextID.SignupConfirmation, TextConstants.SignupConfirmationEmailSubject },
            { TextID.Activation, TextConstants.ActivationEmailSubject },
            { TextID.PasswordReset, TextConstants.ResetPasswordEmailSubject },
            { TextID.ChangeEmail, TextConstants.ChangeEmailRequestSubject },
        };

        public class DataTransport
        {
            public string Subject;
            public string Body;
            public TextID ID;
            public DateTime LastChanged;
        }

        [HttpGet]
        [Route("{id}")]
        [Ceen.Mvc.Name("index")]
        public async Task<IResult> FetchItem(TextID id)
        {
            _bodyConstants.TryGetValue(id, out var bodykey);
            if (string.IsNullOrWhiteSpace(bodykey))
                return NotFound;

            _subjectConstants.TryGetValue(id, out var subjkey);

            return Json(await DB.RunInTransactionAsync(db => {
                var body = Services.TextHelper.GetTextEntryFromDb(db, bodykey, "en");

                var res = new DataTransport() {
                    ID = id,
                    Body = body?.Text,
                    LastChanged = body == null ? new DateTime(0) :  body.Updated
                };
                if (!string.IsNullOrWhiteSpace(subjkey))
                    res.Subject = Services.TextHelper.GetTextFromDb(db, subjkey, "en");

                return res;
            }));
        }

        [HttpPost]
        [Route("{id}")]
        [Ceen.Mvc.Name("index")]
        public async Task<IResult> UpdateItem(TextID id, DataTransport data)
        {
            if (data == null || data.Body == null)
                return BadRequest;

            _bodyConstants.TryGetValue(id, out var bodykey);
            if (string.IsNullOrWhiteSpace(bodykey))
                return NotFound;

            _subjectConstants.TryGetValue(id, out var subjkey);
            var res = await DB.RunInTransactionAsync(db =>
            {
                var e = db.SelectItemById<Database.TextEntry>(bodykey);
                if (e == null)
                {
                    db.InsertItem(new Database.TextEntry()
                    {
                        ID = bodykey,
                        Text = data.Body,
                        IsDraft = false,
                        Language = "en"
                    });
                }
                else
                {
                    if (e.Updated > data.LastChanged)
                        throw new HttpException(HttpStatusCode.Conflict, "The server data changed");

                    e.Text = data.Body;
                    e.Language = "en";
                    e.IsDraft = false;
                    db.UpdateItem(e);
                }

                // Re-read the values for reporting
                e = db.SelectItemById<Database.TextEntry>(bodykey);

                var dt = new DataTransport() 
                {
                    Body = e.Text,
                    LastChanged = e.Updated,
                    ID = id
                };

                if (!string.IsNullOrWhiteSpace(subjkey) && data.Subject != null)
                {
                    e = db.SelectItemById<Database.TextEntry>(subjkey);
                    if (e == null)
                    {
                        e = db.InsertItem(new Database.TextEntry()
                        {
                            ID = subjkey,
                            Text = data.Subject,
                            IsDraft = false,
                            Language = "en"
                        });
                    }
                    else
                    {
                        e.Text = data.Subject;
                        e.Language = "en";
                        e.IsDraft = false;
                        db.UpdateItem(e);
                    }
                    dt.Subject = e.Text;
                }

                return dt;
            });

            return Json(res);
        }
    }
}