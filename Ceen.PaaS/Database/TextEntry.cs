using System;
using Ceen.Database;

namespace Ceen.PaaS.Database
{
    /// <summary>
    /// A text piece
    /// </summary>
    public class TextEntry
    {
        /// <summary>
        /// The text entry ID
        /// </summary>
        [PrimaryKey]
        public string ID;

        /// <summary>
        /// The text in this item
        /// </summary>
        public string Text;

        /// <summary>
        /// The time this text entry was created
        /// </summary>
        [CreatedTimestamp]
        public DateTime Created;

        /// <summary>
        /// The time this text entry was last updated
        /// </summary>
        [ChangedTimestamp]
        public DateTime Updated;

        /// <summary>
        /// A value indicating if this entry is an auto-saved draft
        /// </summary>
        public bool IsDraft;

        /// <summary>
        /// A value indicating what entry this is a draft for
        /// </summary>
        public string DraftTarget;

        /// <summary>
        /// If this is a multi-language text entry, we can differentiate here
        /// </summary>
        public string Language;

        /// <summary>
        /// If this document is a translation of another document, this will point to it
        /// </summary>
        public string TranslationOf;

    }
}