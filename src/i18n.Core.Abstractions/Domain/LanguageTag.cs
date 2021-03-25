using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using i18n.Core.Abstractions.Extensions;

namespace i18n.Core.Abstractions.Domain
{
    /// <summary>
    /// Helper class for parsing and manipulating language tags.
    /// </summary>
    /// <remarks>
    /// Supports a subset of BCP 47 language tag spec corresponding to the Windows
    /// support for language names, namely the following subtags:
    ///     language    (mandatory, 2 or 3 alphachars)
    ///     script      (optional, 4 alphachars, or 5 alphachars in the special case of Microsoft Pseudo-Locales)
    ///     region      (optional, 2 alphachars | 3 decdigits)
    ///     private use (optional, -x- followed by 4 or more alphanumeric chars)
    /// Example tags supported:
    ///     "en"                [language]
    ///     "en-US"             [language + region]
    ///     "zh"                [language]
    ///     "zh-HK"             [language + region]
    ///     "zh-123"            [language + region]
    ///     "zh-Hant"           [language + script]
    ///     "zh-Hant-HK"        [language + script + region]
    ///     "zh-Hant-HK-x-AAAA" [language + script + region + private use]
    ///     "qps-ploc"          [language + script] [Microsoft pseudo-locale]
    ///     "qps-plocm"         [language + script] [Microsoft pseudo-locale]
    ///     "qps-ploca"         [language + script] [Microsoft pseudo-locale]
    /// </remarks>
    /// <seealso href="http://www.microsoft.com/resources/msdn/goglobal/default.mspx"/>
    public class LanguageTag : ILanguageTag, IEquatable<LanguageTag>, IComparable<LanguageTag>
    {
        // Decl
        public static readonly string[,] NormalizedLangTags =
        {
            { "zh-CN", "zh-Hans" },
            { "zh-TW", "zh-Hant" },
        };
        public enum MatchGrade
        {
            /// <summary>
            /// Only consider a match where language and script and region and private use parts match.
            /// E.g. fr matches fr
            /// E.g. zh-Hans-HK matches zh-Hans-HK
            /// E.g. fr-FR-x-AAAA matches fr-FR-x-AAAA
            /// </summary>
            ExactMatch = 0,
            /// <summary>
            /// Only consider a match where language and script parts match, one region is set and the other region 
            /// is not set.
            /// E.g. fr matches fr-BE
            /// E.g. zh-Hans-HK matches zh-Hans
            /// </summary>
            DefaultRegion = 1,
            /// <summary>
            /// Only consider a match where language and script parts match. Region part need not match.
            /// E.g. fr-CA matches fr-BE
            /// E.g. zh-Hant-HK matches zh-Hant-TW
            /// </summary>
            ScriptMatch = 2,
            /// <summary>
            /// Only consider a match where language matches. Script and region parts need not match.
            /// E.g. zh-Hans-HK matches zh
            /// </summary>
            LanguageMatch = 3,
            _MaxMatch = LanguageMatch,
        }
        // Data
        public static Regex s_regex_parseLangtag = new Regex(@"^([a-zA-Z]{2,3})(?:-([a-zA-Z]{4,5}))?(?:-([a-zA-Z]{2}|[0-9]{3}))?(?:\-x-([a-zA-Z0-9]{4,}))?$", RegexOptions.CultureInvariant);
        // ([a-zA-Z]{2,3})
        //      Matches language.
        // (?:-([a-zA-Z]{4,5}))?
        //      Matches script.
        //      NB: The inner group is wrapped in an outer non-capturing group that
        //      prefixed the former with the '-' which is thus not captured.
        //      NB: according to BCP47, Script subtage is always 4 chars; however, we have
        //      expanded this to allow 5 chars also so as to allow parsing all the Microsoft 
        //      Pseudo-Locale language tags (qps-ploc, qps-plocm, qps-ploca).
        //      If this causes a problem, consider explicitly matching (ploc|plocm|ploca).
        //      Ref Issue https://github.com/turquoiseowl/i18n/issues/195.
        // (?:-([a-zA-Z]{2}|[0-9]{3}))?
        //      Matches region.
        //      NB: The inner group is wrapped in an outer non-capturing group that
        //      prefixed the former with the '-' which is thus not captured.
        // (?:\+[a-zA-Z0-9]{4,})?
        //      Matches private use subtag
        //      eg en-ABCD-GB-x-AAAA
        public static Regex s_regex_parseUrl = new Regex(
            @"^/([a-zA-Z]{2,3}(?:-[a-zA-Z]{4,5})?(?:-(?:[a-zA-Z]{2}|[0-9]{3}))?(?:\-x-([a-zA-Z0-9]{4,}))?)(?:$|/|\?|#)",
            RegexOptions.CultureInvariant);
        // ^/
        // (                                # begin 1st and only capture group
        // [a-zA-Z]{2,3}                    # 2-letter or 3-letter country code
        // (?:-[a-zA-Z]{4,5})?              # optional script code - not a capture group itself
        // (?:-(?:[a-zA-Z]{2}|[0-9]{3}))?   # optional region code (2-letter or 3-digit) - not a capture group itself
        // (?:\-x-([a-zA-Z0-9]{4,}))?       # optional private use tag (-x- followed by 4+ alphanumericcharacters) - not a capture group itself
        // )                                # end 1st and only capture group
        // (?:$|/|\?|#)                     # match end of string or fwd-slash char or question-mark char or hash char - not a capture group itself
        static readonly ConcurrentDictionary<string, LanguageTag> s_cache = new ConcurrentDictionary<string, LanguageTag>();
        // Facilitates fast and efficient re-use of languag tag instances.
        // Key = langtag string.
        // Write-access to this member to be serialized via s_sync.
        static readonly object s_sync = new object();
        // Facilitates serialization of write-access to s_cache.
        // Props
        /// <summary>
        /// Original full language tag string passed to constructor.
        /// </summary>
        readonly string m_langtag;
        /// <summary>
        /// Original full language tag string passed to constructor, converted to all lowercase.
        /// </summary>
        readonly string m_langtagLC;
        /// <summary>
        /// Reference to any parent language tag, or null if no parent determined.
        /// The parent is the the language tag with one less subtag.
        /// E.g. if all three supported subtags are set (language, script and region), the parent 
        /// will be the language tag composed of the language and script subtags.
        /// </summary>
        readonly LanguageTag m_parent;
        /// <summary>
        /// Mandatory Language subtag, or if CON fails then null.
        /// </summary>
        public string Language { get; }
        /// <summary>
        /// Optional Script subtag.
        /// </summary>
        public string Script { get; }
        /// <summary>
        /// Optional Region subtag.
        /// </summary>
        public string Region { get; }
        /// <summary>
        /// Optional PrivateUse subtag, excluding the "x-" part. 
        /// E.g. for a langtag of "en-GB-x-ACMECorp" this property is "ACMECorp".
        /// </summary>
        public string PrivateUse { get; }
        /// <summary>
        /// Unique string per language which is suitable for using as a key in global
        /// caches such as HttpRuntime.Cache. Inited during construction.
        /// </summary>
        public string GlobalKey { get; }
        /// <summary>
        /// Corresponding CultureInfo instance, or null if the langtag is unsupported on this system.
        /// </summary>
        public CultureInfo CultureInfo { get; }
        /// <summary>
        /// If the system supports a cultureinfo object for the language, this is the native name of 
        /// the language suitable for user display, otherwise it is the language tag string.
        /// </summary>
        public string NativeNameTitleCase { get; }
        /// <summary>
        /// Constructs a new instance based on a language tag string.
        /// If successful, then the Language property is set to a valid language subtag.
        /// </summary>
        /// <param name="langtag">
        /// Supports a subset of BCP 47 language tag spec corresponding to the Windows
        /// support for language names, namely the following subtags:
        ///     language (mandatory, 2 alphachars)
        ///     script   (optional, 4 alphachars)
        ///     region   (optional, 2 alphachars | 3 decdigits)
        ///     privateuse (optional, 4+ alphanumericchars)
        /// Example tags supported:
        ///     "en"               [language]
        ///     "en-US"            [language + region]
        ///     "zh"               [language]
        ///     "zh-HK"            [language + region]
        ///     "zh-123"           [language + region]
        ///     "zh-Hant"          [language + script]
        ///     "zh-Hant-HK"       [language + script + region]
        ///     "en-GB-x-ACMECorp" [language + region + privateuse]
        /// </param>
        /// <seealso href="http://www.microsoft.com/resources/msdn/goglobal/default.mspx"/>
        public LanguageTag(string langtag)
        {
            m_langtag = langtag.Trim();
            // Normalize certain langtags:
            // «LX113» http://www.w3.org/International/articles/language-tags/#script
            for (var i = 0; i < NormalizedLangTags.GetLength(0); ++i)
            {
                if (0 == string.Compare(m_langtag, NormalizedLangTags[i, 0], true))
                {
                    m_langtag = NormalizedLangTags[i, 1];
                    break;
                }
            }
            m_langtagLC = m_langtag.ToLowerInvariant();
            // Parse the langtag.
            var match = s_regex_parseLangtag.Match(m_langtag);
            if (match.Success
                && match.Groups.Count == 5)
            {
                Language = match.Groups[1].Value;
                Script = match.Groups[2].Value;
                Region = match.Groups[3].Value;
                PrivateUse = match.Groups[4].Value;
            }
            // Load any parent:
            //   l-s-r-p -> l-s-r
            //   l-s-r   -> l-s
            //   l-r     -> l
            //   l-s     -> l
            //   l       -> no parent
            if (Region.IsSet() && Script.IsSet() && PrivateUse.IsSet())
            {
                m_parent = GetCachedInstance($"{Language}-{Script}-{Region}");
            }
            else if (Region.IsSet() && Script.IsSet())
            {
                m_parent = GetCachedInstance($"{Language}-{Script}");
            }
            else if (Script.IsSet() || Region.IsSet())
            {
                m_parent = GetCachedInstance(Language);
            }
            else
            {
                m_parent = null;
            }
            //
            GlobalKey = $"po:{m_langtag}".ToLowerInvariant();
            //
            try
            {
                if (PrivateUse.IsSet())
                {   // Strip out the private use subtag to allow CultureInfo to be set, based on the rest of the language tag
                    CultureInfo = new CultureInfo(langtag.Replace("-x-" + PrivateUse, string.Empty));
                }
                else
                {
                    CultureInfo = new CultureInfo(langtag);
                }

            }
            catch (ArgumentException) { }
            // 
            NativeNameTitleCase = CultureInfo != null ? CultureInfo.TextInfo.ToTitleCase(CultureInfo.NativeName) : "m_langtag";
            //Debug.Assert(ToString() == langtag);
        }
        /// <summary>
        /// Instance factory that supports re-use of instances which by definition are read-only.
        /// </summary>
        /// <param name="langtag">
        /// Supports a subset of BCP 47 language tag spec corresponding to the Windows
        /// support for language names, namely the following subtags:
        ///     language (mandatory, 2 alphachars)
        ///     script   (optional, 4 alphachars)
        ///     region   (optional, 2 alphachars | 3 decdigits)
        /// Example tags supported:
        ///     "en"            [language]
        ///     "en-US"         [language + region]
        ///     "zh"            [language]
        ///     "zh-HK"         [language + region]
        ///     "zh-123"        [language + region]
        ///     "zh-Hant"       [language + script]
        ///     "zh-Hant-HK"    [language + script + region]
        ///     "en-GB+ACMECorp" [language + region + privateuse]
        /// </param>
        /// <returns>Either new or pre-exisiting instance, or null if langtag is invalid.</returns>
        /// <seealso href="http://www.microsoft.com/resources/msdn/goglobal/default.mspx"/>
        public static LanguageTag GetCachedInstance(string langtag)
        {
            if (!langtag.IsSet())
            {
                return null;
            }
            LanguageTag result = null;
            // Get any extant instance, no need to lock as just reading.
            if (s_cache.TryGetValue(langtag, out result))
            {
                return result;
            }
            // Instance doesn't exist so we may need to add which will require serialized access, so lock.
            lock (s_sync)
            {
                // Check again for instance incase another thd just created it.
                if (s_cache.TryGetValue(langtag, out result))
                {
                    return result;
                }
                // If cache is over a certain size...clear it.
                // NB: this prevents the cache from filling with invalid langtag values,
                // possibly due to DOS attack.
                if (s_cache.Count > 10000)
                {
                    s_cache.Clear();
                }
                // Create and cache new instance.
                result = new LanguageTag(langtag);
                if (!result.IsValid())
                {
                    return null;
                }
                s_cache[langtag] = result;
                return result;
            }
        }
        // [Object]
        /// <returns>
        /// Language tag string.
        /// Supports a subset of BCP 47 language tag spec corresponding to the Windows
        /// support for language names, namely the following subtags:
        ///     language (mandatory, 2 alphachars)
        ///     script   (optional, 4 alphachars)
        ///     region   (optional, 2 alphachars | 3 decdigits)
        /// Example tags supported:
        ///     "en"            [language]
        ///     "en-US"         [language + region]
        ///     "zh"            [language]
        ///     "zh-HK"         [language + region]
        ///     "zh-123"        [language + region]
        ///     "zh-Hant"       [language + script]
        ///     "zh-Hant-HK"    [language + script + region]
        ///     "en-GB-x-ABCD" [language + region + privateuse]
        /// </returns>
        public override string ToString()
        {
            return m_langtag.IsSet() ? m_langtag : "";
        }
        public override bool Equals(object obj)
        {
            var rhs = obj as LanguageTag;
            return rhs != null && Equals(rhs);
        }
        public override int GetHashCode()
        {
            return m_langtagLC.GetHashCode();
        }
        // [IEquatable<ILanguageTag>]
        public bool Equals(ILanguageTag other)
        {
            return 0 == string.Compare(m_langtag, other.ToString(), true);
        }
        // [IEquatable<LanguageTag>]
        public bool Equals(LanguageTag other)
        {
            return 0 == string.Compare(m_langtagLC, other.m_langtagLC);
        }
        public bool Equals(string other)
        {
            var ltOther = GetCachedInstance(other);
            return 0 == string.Compare(m_langtagLC, ltOther.m_langtagLC);
        }
        // [IComparable<ILanguageTag>]
        public int CompareTo(ILanguageTag other)
        {
            return string.Compare(m_langtag, other.ToString(), true);
        }
        // [IComparable<LanguageTag>]
        public int CompareTo(LanguageTag other)
        {
            return string.Compare(m_langtagLC, other.m_langtagLC);
        }
        public int CompareTo(string other)
        {
            var ltOther = GetCachedInstance(other);
            return string.Compare(m_langtagLC, ltOther.m_langtagLC);
        }
        // [ILanguageTag]
        string ILanguageTag.GetLanguage() { return Language; }
        string ILanguageTag.GetExtlang() { return null; }
        string ILanguageTag.GetScript() { return Script; }
        string ILanguageTag.GetRegion() { return Region; }
        string[] ILanguageTag.GetVariant() { return null; }
        string ILanguageTag.GetExtension() { return null; }
        string ILanguageTag.GetPrivateuse() { return null; }
        ILanguageTag ILanguageTag.GetParent() { return m_parent; }
        int ILanguageTag.GetMaxParents() { return 2; }
        CultureInfo ILanguageTag.GetCultureInfo() { return CultureInfo; }
        string ILanguageTag.GetNativeNameTitleCase() { return NativeNameTitleCase; }
        // Operations
        /// <summary>
        /// Performs 'language matching' between lang described by this (A)
        /// and language decibed by i_rhs (B). Essentially, returns an assessment of
        /// how well a speaker of A will understand B.
        /// The key points are as follows:
        ///   · The Script is almost as relevant as the language itself; that is, if
        ///     you speak a language but do not understand the script, you cannot
        ///     read that language. Thus a mismatch in Script should score low.
        ///   · The Region is less relevant than Script to understanding of language.
        ///     The one exception to this is where the Region has traditionally been
        ///     used to also indicate the Script. E.g.
        ///         zh-CH -> Chinese (Simplified)  i.e. zh-Hans
        ///         zh-TW -> Chinese (Traditional) i.e. zh-Hant
        ///     In these cases we normalize all legacy langtags to their new values
        ///     before matching. E.g. zh-CH is normalized to zh-Hans.
        /// «LX113»
        /// </summary>
        /// <param name="i_rhs"></param>
        /// <returns>
        /// Returns a score on to what extent the two languages match. The value ranges from
        /// 100 (exact match) down to 0 (fundamental language tag mismatch), with values 
        /// in between which may be used to compare quality of a match, larger the value
        /// meaning better quality.
        /// </returns>
        /// <remarks>
        /// Matching values:
        ///                                              RHS
        /// this                    lang    lang+script     lang+region     lang+script+region
        /// ----------------------------------------------------------------------------------
        /// lang                |   A       D               C               D
        /// lang+script         |   D       A               D               B
        /// lang+region         |   C       D               A               D
        /// lang+script+region  |   D       B               D               A
        /// 
        /// NB: For the purposes of the logic above, lang incorporates Language + PrivateUse subtags.
        /// 
        /// A. Exact match (100)
        ///     All three subtags match.
        /// B. Unbalanced Region Mismatch (99) [zh, zh-HK] [zh-Hans, zh-Hans-HK]
        ///     Language and Script match;
        ///     one side has Region set while the other doesn't.
        ///     Here there is the possibility that due to defaults Region matches.
        /// C. Balanced Region Mismatch (98) [zh-IK, zh-HK] [zh-Hans-IK, zh-Hans-HK]
        ///     Language and Script match;
        ///     both sides have Region set but to different values.
        ///     Here there is NO possibility that Region matches.
        /// D. Unbalanced Script Mismatch (97) [zh-HK, zh-Hant-HK]
        ///     Language matches, Region may match;
        ///     one side has Script set while the other doesn't.
        ///     Here there is the possibility that due to defaults Script matches.
        /// E. Balanced Script Mismatch (96)
        ///     Language matches, Region may match;
        ///     both sides have Script set but to different values.
        ///     Here there is NO possibility that Script matches.
        /// F. Language Mismatch (0)
        ///     Language doesn't match.
        /// </remarks>
        /// <seealso href="http://msdn.microsoft.com/en-us/library/windows/apps/jj673578.aspx"/>
        public int Match(LanguageTag i_rhs, MatchGrade matchGrade = MatchGrade.LanguageMatch)
        {
            if (i_rhs == null) { throw new ArgumentNullException(nameof(i_rhs)); }
            // Either langtag being null fails the match.
            if (!Language.IsSet() || !i_rhs.Language.IsSet())
            {
                return 0;
            }
            // Init.
            bool[] L = { 0 == string.Compare(Language, i_rhs.Language, true), Language.IsSet(), i_rhs.Language.IsSet() };
            bool[] S = { 0 == string.Compare(Script, i_rhs.Script, true), Script.IsSet(), i_rhs.Script.IsSet() };
            bool[] R = { 0 == string.Compare(Region, i_rhs.Region, true), Region.IsSet(), i_rhs.Region.IsSet() };
            bool[] P = { 0 == string.Compare(PrivateUse, i_rhs.PrivateUse, true), PrivateUse.IsSet(), i_rhs.PrivateUse.IsSet() };
            // Language incorporates Language + PrivateUse subtags for our logic here.
            L[0] = L[0] && P[0];
            L[1] = L[1] || P[1];
            L[2] = L[2] || P[2];
            // Logic.
            var score = 100;
            // F.
            if (!L[0])
            {
                return 0;
            }
            // A.
            if (S[0] && R[0] && P[0])
            {
                return score;
            }
            --score;
            if (matchGrade != MatchGrade.ExactMatch)
            {
                // B.
                if (S[0] && !R[0] && R[1] != R[2])
                {
                    return score;
                }
                --score;
                if (matchGrade != MatchGrade.DefaultRegion)
                {
                    // C.
                    if (S[0] && !R[0] && R[1] == R[2])
                    {
                        return score;
                    }
                    --score;
                    if (matchGrade != MatchGrade.ScriptMatch)
                    {
                        // D.
                        if (!S[0] && S[1] != S[2])
                        {
                            return score;
                        }
                        --score;
                        // E.
                        if (!S[0] && S[1] == S[2])
                        {
                            return score;
                        }
                    }
                    //--score;
                    //DebugHelpers.WriteLine("LanguageTag.Match -- fallen through: {0}, {1}", ToString(), i_rhs.ToString());
                    //Debug.Assert(false);
                }
            }
            // F.
            return 0;
        }
        /// <summary>
        /// Looks up in the passed collection of supported AppLanguages the language that is best matched
        /// to this langtag. I.e. the written AppLanguage that a user understanding this langtag
        /// will most-likely understand.
        /// </summary>
        /// <returns>Selected CultureInfoEx instance from the AppLanguages collection or null if there was no match.</returns>
        public int Match(LanguageTag[] AppLanguages, MatchGrade matchGrade = MatchGrade.LanguageMatch)
        {
            var matchScore = 0;
            foreach (var langtag in AppLanguages)
            {
                var score = Match(langtag, matchGrade);
                if (score > matchScore)
                {
                    matchScore = score;
                    if (matchScore == 100)
                    { // Can't beat an exact match.
                        break;
                    }
                }
            }
            return matchScore;
        }
        /// <summary>
        /// Helper for detecting a URL prefixed with a langtag part, and if found outputs
        /// both the langtag and the URL with the prefix removed.
        /// </summary>
        /// <remarks>
        /// This method does not check for the validity of the returned langtag other than
        /// it matching the pattern of a langtag as supported by this LanguageTag class.
        /// </remarks>
        /// <param name="url">Either an absolute or relative URL string, as specified by the uriKind parameter.</param>
        /// <param name="uriKind">
        /// Indicates the type of URI in the url parameter. If the URL is known to be relative, this method is more efficient if this 
        /// parameter is set to UriKind.Relative.
        /// </param>
        /// <param name="urlPatched">
        /// On success, set to the URL with the prefix path part removed.
        /// On failure, set to value of url param.
        /// </param>
        /// <returns>On success a langtag string, otherwise null.</returns>
        /// <remarks>
        /// <para>
        /// For URL /zh-Hans/account/signup we return "zh-Hans" and output /account/signup.
        /// </para>
        /// </remarks>
        public static string ExtractLangTagFromUrl(string url, UriKind uriKind, out string urlPatched)
        {
            // If url is possibly absolute
            if (uriKind != UriKind.Relative)
            {
                // If absolute url (include host and optionally scheme)
                Uri uri;
                if (Uri.TryCreate(url, UriKind.Absolute, out uri))
                {
                    var ub = new UriBuilder(url);
                    var langtag = ExtractLangTagFromUrl(ub.Path, UriKind.Relative, out var strPatchedPath);
                    // Match?
                    if (langtag != null)
                    {
                        ub.Path = strPatchedPath;
                        urlPatched = ub.Uri.ToString(); // Go via Uri to avoid port 80 being added.
                    }
                    // No match.
                    else
                    {
                        urlPatched = url;
                    }
                    return langtag;
                }
            }

            // Url is relative. Parse it.
            var match = s_regex_parseUrl.Match(url);
            // If successful
            if (match.Success
                && match.Groups.Count == 3)
            {
                // Extract the langtag value.
                var langtag = match.Groups[1].Value;
                // Patch the url.
                urlPatched = url.Substring(langtag.Length + 1);
                if (!urlPatched.StartsWith("/"))
                {
                    urlPatched = "/" + urlPatched;
                }
                // Success.
                return langtag;
            }
            // No match.
            urlPatched = url;
            return null;
        }
        /// <summary>
        /// Patches in the langtag into the passed url, replacing any extant langtag in the url if necessary.
        /// </summary>
        /// <param name="url">Either an absolute or relative URL string, as specified by the uriKind parameter.</param>
        /// <param name="uriKind">
        /// Indicates the type of URI in the url parameter. If the URL is known to be relative, this method is more efficient if this 
        /// parameter is set to UriKind.Relative.
        /// </param>
        /// <param name="langtag">
        /// Optional langtag to be patched into the URL, or null if any langtag 
        /// to be removed from the URL.
        /// </param>
        /// <returns>UriBuilder containing the modified version of url.</returns>
        /// <remarks>
        /// <para>"http://example.com/account/signup"         , "en" -> "http://example.com/en/account/signup"</para>
        /// <para>"http://example.com/zh-Hans/account/signup" , "en" -> "http://example.com/en/account/signup"</para>
        /// </remarks>
        public static string SetLangTagInUrlPath(string url, UriKind uriKind, string langtag)
        {
            string urlPatched;
            ExtractLangTagFromUrl(url, uriKind, out urlPatched);
            urlPatched = urlPatched.UrlPrependPath(langtag);
            return urlPatched;
        }
        // Trace
        [Conditional("DEBUG")]
        public static void Trace_Match(string i_langtag_lhs, string i_langtag_rhs)
        {
            var lhs = new LanguageTag(i_langtag_lhs);
            var rhs = new LanguageTag(i_langtag_rhs);
            var score = lhs.Match(rhs);
        }
        [Conditional("DEBUG")]
        public static void Trace()
        {
            Trace_Match("zh", "zh");
            Trace_Match("zh", "zh-HK");
            Trace_Match("zh", "zh-Hant");
            Trace_Match("zh", "zh-Hant-HK");
            Trace_Match("zh-HK", "zh");
            Trace_Match("zh-HK", "zh-HK");
            Trace_Match("zh-HK", "zh-Hant");
            Trace_Match("zh-HK", "zh-Hant-HK");
            Trace_Match("zh-Hant", "zh");
            Trace_Match("zh-Hant", "zh-HK");
            Trace_Match("zh-Hant", "zh-Hant");
            Trace_Match("zh-Hant-HK", "zh-Hant-HK");
            Trace_Match("zh-Hant-HK", "zh");
            Trace_Match("zh-Hant-HK", "zh-HK");
            Trace_Match("zh-Hant-HK", "zh-Hant");
            Trace_Match("zh-Hant-HK", "zh-Hant-HK");

            // Private use:
            Trace_Match("zh-Hant-HK-x-ABCD", "zh-Hant-HK-x-ABCD");
            Trace_Match("zh-Hant-HK-x-ABCD", "zh-Hant-HK-x-ZZZZ");
            Trace_Match("zh-Hant-HK-x-ABCD", "zh-Hant-HK");
            Trace_Match("zh-Hant-HK-x-ABCD", "zh-HK");
            Trace_Match("zh-Hant-HK-x-ABCD", "zh");
            //Wrong subtags:
            Trace_Match("zh-Iant-HK-x-ABCD", "zh-Hant-HK-x-ABCD");
            Trace_Match("zh-Hant-GB-x-ABCD", "zh-Hant-HK-x-ABCD");
            //Invalid private use tag:
            Trace_Match("zh-Hant-HK-x-ABCD", "zh-Hant-HK-x-ZZZ");
            Trace_Match("zh-Hant-HK-x-ABCD", "zh-Hant-HK-ABCD");


            Trace_Match("dh", "zh");
            Trace_Match("dh", "zh-HK");
            Trace_Match("dh", "zh-Hant");
            Trace_Match("dh", "zh-Hant-HK");
            Trace_Match("dh-HK", "zh");
            Trace_Match("dh-HK", "zh-HK");
            Trace_Match("dh-HK", "zh-Hant");
            Trace_Match("dh-HK", "zh-Hant-HK");
            Trace_Match("dh-Hant", "zh");
            Trace_Match("dh-Hant", "zh-HK");
            Trace_Match("dh-Hant", "zh-Hant");
            Trace_Match("dh-Hant-HK", "zh-Hant-HK");
            Trace_Match("dh-Hant-HK", "zh");
            Trace_Match("dh-Hant-HK", "zh-HK");
            Trace_Match("dh-Hant-HK", "zh-Hant");
            Trace_Match("dh-Hant-HK", "zh-Hant-HK");

            Trace_Match("zh", "zh");
            Trace_Match("zh", "zh-HK");
            Trace_Match("zh", "zh-Hant");
            Trace_Match("zh", "zh-Hant-HK");
            Trace_Match("zh-IK", "zh");
            Trace_Match("zh-IK", "zh-HK");
            Trace_Match("zh-IK", "zh-Hant");
            Trace_Match("zh-IK", "zh-Hant-HK");
            Trace_Match("zh-Hant", "zh");
            Trace_Match("zh-Hant", "zh-HK");
            Trace_Match("zh-Hant", "zh-Hant");
            Trace_Match("zh-Hant-IK", "zh-Hant-HK");
            Trace_Match("zh-Hant-IK", "zh");
            Trace_Match("zh-Hant-IK", "zh-HK");
            Trace_Match("zh-Hant-IK", "zh-Hant");
            Trace_Match("zh-Hant-IK", "zh-Hant-HK");

            Trace_Match("zh", "zh");
            Trace_Match("zh", "zh-HK");
            Trace_Match("zh", "zh-Hant");
            Trace_Match("zh", "zh-Hant-HK");
            Trace_Match("zh-HK", "zh");
            Trace_Match("zh-HK", "zh-HK");
            Trace_Match("zh-HK", "zh-Hant");
            Trace_Match("zh-HK", "zh-Hant-HK");
            Trace_Match("zh-Iant", "zh");
            Trace_Match("zh-Iant", "zh-HK");
            Trace_Match("zh-Iant", "zh-Hant");
            Trace_Match("zh-Iant-HK", "zh-Hant-HK");
            Trace_Match("zh-Iant-HK", "zh");
            Trace_Match("zh-Iant-HK", "zh-HK");
            Trace_Match("zh-Iant-HK", "zh-Hant");
            Trace_Match("zh-Iant-HK", "zh-Hant-HK");

            Trace_Match("zh", "zh");
            Trace_Match("zh", "zh-HK");
            Trace_Match("zh", "zh-Hant");
            Trace_Match("zh", "zh-Hant-HK");
            Trace_Match("zh-IK", "zh");
            Trace_Match("zh-IK", "zh-HK");
            Trace_Match("zh-IK", "zh-Hant");
            Trace_Match("zh-IK", "zh-Hant-HK");
            Trace_Match("zh-Iant", "zh");
            Trace_Match("zh-Iant", "zh-HK");
            Trace_Match("zh-Iant", "zh-Hant");
            Trace_Match("zh-Iant-HK", "zh-Hant-HK");
            Trace_Match("zh-Iant-HK", "zh");
            Trace_Match("zh-Iant-HK", "zh-HK");
            Trace_Match("zh-Iant-HK", "zh-Hant");
            Trace_Match("zh-Iant-HK", "zh-Hant-HK");
        }
    }


    public static partial class LanguageTagExtensions
    {
        public static bool IsValid(
            this ILanguageTag lt)
        {
            return lt != null && lt.GetLanguage() != null;
        }
    }

}
