using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace i18n.Core.Abstractions.Domain.Helpers
{
    /// <summary>
    /// Describes a valid set of string tokens that define the format of a Nugget.
    /// </summary>
    /// <remarks>
    /// The standard numgget format is as follows:
    ///   [[[Enter between %0 and %1 characters|||{1}|||{2}/// The %0 identifies refers to min number and the %1 refers to the max number. ]]]
    /// where:
    ///   BeginToken = "[[["
    ///   EndToken = "]]]"
    ///   DelimiterToken = "|||"
    ///   CommentToken = "///"
    /// </remarks>
    public class NuggetTokens
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
            if (!StringExtensions.IsSet(beginToken)) { throw new ArgumentNullException(nameof(beginToken)); }
            if (!StringExtensions.IsSet(endToken)) { throw new ArgumentNullException(nameof(endToken)); }
            if (!StringExtensions.IsSet(delimiterToken)) { throw new ArgumentNullException(nameof(delimiterToken)); }
            if (!StringExtensions.IsSet(commentToken)) { throw new ArgumentNullException(nameof(commentToken)); }

            BeginToken = beginToken;
            EndToken = endToken;
            DelimiterToken = delimiterToken;
            CommentToken = commentToken;
        }
    }

    /// <summary>
    /// Describes the components of a nugget.
    /// </summary>
    /// <remarks>
    /// Formatted nuggets:
    ///
    /// The msgid for a formatted nugget:
    ///
    ///    Enter between %0 and %1 characters|||100|||6
    ///
    /// while the original string in the code for this may have been:
    ///
    ///    [[[Enter between %0 and %1 characters|||{1}|||{2}]]]
    ///
    /// The canonical msgid part is that between the opening [[[ and the first ||| or ///:
    ///
    ///    Enter between %0 and %1 characters
    /// </remarks>
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public class Nugget
    {
        public string MsgId { get; set; }
        public string[] FormatItems { get; set; }
        public string Comment { get; set; }

        // Helpers

        public bool IsFormatted => FormatItems != null && FormatItems.Length != 0;

        public override string ToString()
        {
            return MsgId;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }
            if (GetType() != obj.GetType())
            {
                return false;
            }
            var other = (Nugget)obj;
            // Compare non-array members.
            if (MsgId != other.MsgId // NB: the operator==() on string objects handles null value on either side just fine.
                || Comment != other.Comment)
            {
                return false;
            }
            // Compare arrays.
            if (FormatItems == null != (other.FormatItems == null)
                || FormatItems != null && !FormatItems.SequenceEqual(other.FormatItems))
            {
                return false;
            }
            return true;
        }

        public override int GetHashCode()
        {
            return 0
                .CombineHashCode(MsgId)
                .CombineHashCode(FormatItems)
                .CombineHashCode(Comment);
        }
    };

    /// <summary>
    /// Helper class for locating and processing nuggets in a string.
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public class NuggetParser
    {
        /// <summary>
        /// Nuggets may be parsed during the different stages as enumerated here.
        /// </summary>
        public enum Context { SourceProcessing, ResponseProcessing };

        /// <summary>
        /// Specifies whether the nugget is being parsed as part of source processing
        /// or response processing.
        /// </summary>
        readonly Context _mContext;

        /// <summary>
        /// Initialized during CON to a regex suitable for breaking down a nugget into its component parts,
        /// as defined by the NuggetTokens definition passed to the CON.
        /// </summary>
        readonly Regex _mRegexNuggetBreakdown;

        // Con

        public NuggetParser(
            NuggetTokens nuggetTokens,
            Context context)
        {
            _mContext = context;
            // Prep the regexes. We escape each token char to ensure it is not misinterpreted.
            // · Breakdown e.g. "\[\[\[(.+?)(?:\|\|\|(.+?))*(?:\/\/\/(.+?))?\]\]\]"
            _mRegexNuggetBreakdown = new Regex(
                string.Format(@"{0}(.+?)(?:{1}(.{4}?))*(?:{2}(.+?))?{3}",
                    EscapeString(nuggetTokens.BeginToken),
                    EscapeString(nuggetTokens.DelimiterToken),
                    EscapeString(nuggetTokens.CommentToken),
                    EscapeString(nuggetTokens.EndToken),
                    _mContext == Context.SourceProcessing ? "+" : "*"),
                RegexOptions.CultureInvariant
                    | RegexOptions.Singleline);
            // RegexOptions.Singleline in fact enable multi-line nuggets.
        }

        // Operations

        /// <summary>
        /// Parses a string entity for nuggets, forwarding the nugget to a caller-provided
        /// delegate, with support for replacement of nugget strings in the entity.
        /// </summary>
        /// <param name="entity">
        /// String containing nuggets to be parsed. E.g. source code file, HTTP response entity.
        /// </param>
        /// <param name="processNugget">
        /// Delegate callback to be called for each nugget encountered in entity:
        ///     delegate(string nuggetString, int pos, Nugget nugget1, string entity1).
        /// Returns a string with which to replace the nugget string in the source entity.
        /// If no change, then may return null.
        /// </param>
        /// <returns>
        /// Entity string reflecting any nugget strings replacements.
        /// </returns>
        public string ParseString(
            string entity,
            Func<string, int, Nugget, string, string> processNugget)
        {
            // Note that this method has two-levels of delegates:
            //   Outer delegate is the delegate which is called by regex as it matches each nugget
            //   Inner delegate is the client callback delegate (ProcessNugget) which we call from the outer delegate.
            //
            // Lookup any/all nuggets in the entity and call the client delegate (ProcessNugget) for each.
            return _mRegexNuggetBreakdown.Replace(entity, delegate (Match match)
            {
                var nugget = NuggetFromRegexMatch(match);
                //
                var modifiedNuggetString = processNugget(
                    match.Groups[0].Value, // entire nugget string
                    match.Groups[0].Index, // zero-based pos of the first char of entire nugget string
                    nugget,                // broken-down nugget
                    entity);               // source entity string
                                           // Returns either modified nugget string, or original nugget string (i.e. for no replacement).
                return modifiedNuggetString ?? match.Groups[0].Value;
            });
        }

        /// <summary>
        /// Parses a nugget string to breakdown the nugget into individual components.
        /// </summary>
        /// <param name="nugget">Subject nugget string.</param>
        /// <returns>If successful, returns Nugget instance; otherwise returns null indicating a badly formatted nugget string.</returns>
        public Nugget BreakdownNugget(string nugget)
        {
            var match = _mRegexNuggetBreakdown.Match(nugget);
            return NuggetFromRegexMatch(match);
        }

        // Helpers

        /// <summary>
        /// Modifies a string such that each character is prefixed by another character
        /// (defaults to backslash).
        /// </summary>
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

        /// <summary>
        /// Returns a nugget instance loaded from a regex match, or null if error.
        /// </summary>
        Nugget NuggetFromRegexMatch(Match match)
        {
            if (!match.Success
                || match.Groups.Count != 4)
            {
                return null;
            }

            var n = new Nugget
            {
                MsgId = match.Groups[1].Value
            };

            // Extract msgid from 2nd capture group.
            // Extract format items from 3rd capture group.
            var formatItems = match.Groups[2].Captures;
            if (formatItems.Count != 0)
            {
                n.FormatItems = new string[formatItems.Count];
                var i = 0;
                foreach (Capture capture in formatItems)
                {
                    if (_mContext == Context.SourceProcessing
                        && !StringExtensions.IsSet(capture.Value))
                    {
                        return null;
                    } // bad format
                    n.FormatItems[i++] = capture.Value;
                }
            }
            // Extract comment from 4th capture group.
            if (StringExtensions.IsSet(match.Groups[3].Value))
            {
                n.Comment = match.Groups[3].Value;
            }
            // Success.
            return n;
        }
    }
}
