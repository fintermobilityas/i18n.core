using System.Collections.Generic;

namespace i18n.Core.PostBuild.Entities
{
    /// <summary>
    /// Template items are only used to keep track of the strings needing translation in any given project and for then updating the translations and translationItems with this data.
    /// You should never need to work with TemplateItem unless you work with finding nuggets and updating the template file.
    /// </summary>
    public sealed class TemplateItem
    {
        public string MsgKey { get; set; }
        public string MsgId { get; set; }
        public IEnumerable<ReferenceContext> References { get; set; }
        public IEnumerable<string> Comments { get; set; }
        public string FileName { get; set; }

        public override string ToString()
        {
            return MsgKey;
        }

        public static string KeyFromMsgidAndComment(
            string msgid,
            string comment,
            bool messageContextFromComment)
        {
            if (messageContextFromComment && !string.IsNullOrEmpty(comment))
            {
                return $"{msgid}:£#£#£:{comment}";
            }
            return msgid;
        }

    }
}
