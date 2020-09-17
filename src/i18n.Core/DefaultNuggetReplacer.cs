using System;
using System.Text;
using System.Text.RegularExpressions;
using i18n.Core.Abstractions;
using i18n.Core.Pot.Helpers;
using JetBrains.Annotations;

namespace i18n.Core
{
    public interface INuggetReplacer
    {
        string Replace([NotNull] CultureDictionary cultureDictionary, string text);
    }

    public class DefaultNuggetReplacer : INuggetReplacer
    {
        static readonly Regex NuggetRegex;

        // https://github.com/turquoiseowl/i18n/blob/ce7bdc9d8a8b92022c42417edeff4fb9ce8d3170/src/i18n.Domain/Helpers/NuggetParser.cs#L149

        static DefaultNuggetReplacer()
        {
            var nuggetTokens = new NuggetTokens("[[[", "]]]", "|||", "///");

            const NuggetParser.Context context = NuggetParser.Context.ResponseProcessing;
            const RegexOptions regexOptions = RegexOptions.CultureInvariant
                                              | RegexOptions.Singleline
                                              | RegexOptions.Compiled;

            // Prep the regexes. We escape each token char to ensure it is not misinterpreted.
            // · Breakdown e.g. "\[\[\[(.+?)(?:\|\|\|(.+?))*(?:\/\/\/(.+?))?\]\]\]"
            NuggetRegex = new Regex(
                string.Format(@"{0}(.+?)(?:{1}(.{4}?))*(?:{2}(.+?))?{3}",
                    EscapeString(nuggetTokens.BeginToken),
                    EscapeString(nuggetTokens.DelimiterToken),
                    EscapeString(nuggetTokens.CommentToken),
                    EscapeString(nuggetTokens.EndToken),
                    // ReSharper disable once UnreachableCode
                    context == NuggetParser.Context.SourceProcessing ? "+" : "*"), regexOptions);
        }

        public string Replace(CultureDictionary cultureDictionary, string text)
        {
            if (cultureDictionary == null) throw new ArgumentNullException(nameof(cultureDictionary));

            string ReplaceNuggets(Match match)
            {
                var textExclNugget = match.Groups[1].Value;
                var searchText = textExclNugget;

                var textExclNuggetPosition = textExclNugget.IndexOf("///", StringComparison.Ordinal);
                if (textExclNuggetPosition >= 0)
                {
                    searchText = textExclNugget.Substring(0, textExclNuggetPosition);
                }

                var translationText = cultureDictionary[searchText];
                return translationText ?? searchText;
            }

            return NuggetRegex.Replace(text, ReplaceNuggets);
        }

        static string EscapeString(string str, char escapeChar = '\\')
        {
            var str1 = new StringBuilder(str.Length * 2);
            foreach (var c in str)
            {
                str1.Append(escapeChar);
                str1.Append(c);
            }
            return str1.ToString();
        }

        readonly struct NuggetTokens
        {
            public string BeginToken { get; }
            public string EndToken { get; }
            public string DelimiterToken { get; }
            public string CommentToken { get; }

            public NuggetTokens(
                string beginToken,
                string endToken,
                string delimiterToken,
                string commentToken)
            {
                if (!beginToken.IsSet()) { throw new ArgumentNullException(nameof(beginToken)); }
                if (!endToken.IsSet()) { throw new ArgumentNullException(nameof(endToken)); }
                if (!delimiterToken.IsSet()) { throw new ArgumentNullException(nameof(delimiterToken)); }
                if (!commentToken.IsSet()) { throw new ArgumentNullException(nameof(commentToken)); }

                BeginToken = beginToken;
                EndToken = endToken;
                DelimiterToken = delimiterToken;
                CommentToken = commentToken;
            }
        }
    }

}
