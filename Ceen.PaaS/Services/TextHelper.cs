using System;
using Ceen.Database;
using System.Data;

namespace Ceen.PaaS.Services
{
    /// <summary>
    /// Helper methods to extract a text entry from the database with a desired locale
    /// </summary>
    public static class TextHelper
    {
        /// <summary>
        /// Helper method to get a text string from the database in a locale
        /// </summary>
        /// <param name="db">The database instance</param>
        /// <param name="id">The ID of the string to grab</param>
        /// <param name="preferedLanguage">The user's prefered language</param>
        /// <returns>The text string or null</returns>
        public static Database.TextEntry GetTextEntryFromDb(IDbConnection db, string id, string preferedLanguage)
        {
            var e = db.SelectSingle<Database.TextEntry>(x => x.ID == id);

            // If the primary ID entry does not exist, we should not have any translations either
            if (e == null)
                return null;

            // See if we can get an exact language match
            if (e.Language != preferedLanguage)
            {
                var n = db.SelectSingle<Database.TextEntry>(x => x.TranslationOf == id && x.Language == preferedLanguage && !x.IsDraft);
                if (n != null)
                    return n;
            }

            // Return the primary entry
            return e;
        }

        /// <summary>
        /// Helper method to get a text string from the database in a locale
        /// </summary>
        /// <param name="db">The database instance</param>
        /// <param name="id">The ID of the string to grab</param>
        /// <param name="preferedLanguage">The user's prefered language</param>
        /// <returns>The text string or null</returns>
        public static string GetTextFromDb(IDbConnection db, string id, string preferedLanguage)
        {
            return GetTextEntryFromDb(db, id, preferedLanguage)?.Text;
        }        
    }
}