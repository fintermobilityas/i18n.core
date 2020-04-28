using System;
using System.Text.RegularExpressions;
using i18n.Core.Abstractions;
using JetBrains.Annotations;

namespace i18n.Core
{
    public interface INuggetReplacer
    {
        string Replace([NotNull] CultureDictionary cultureDictionary, string text);
    }

    public class DefaultNuggetReplacer : INuggetReplacer
    {
        static readonly Regex NuggetFindRegex = new Regex(@"\[\[\[(.*?)\]\]\]", RegexOptions.Compiled);

        public string Replace(CultureDictionary cultureDictionary, string text)
        {
            if (cultureDictionary == null) throw new ArgumentNullException(nameof(cultureDictionary));
            
            string ReplaceNuggets(Match match)
            {
                // ReSharper disable once UnusedVariable
                var textInclNugget = match.Value;
                var textExclNugget = match.Groups[1].Value;
                var searchText = textExclNugget;

                if (textExclNugget.IndexOf("///", StringComparison.Ordinal) >= 0)
                {
                    // Remove comments
                    searchText = textExclNugget.Substring(0, textExclNugget.IndexOf("///", StringComparison.Ordinal));
                }

                var translationText = cultureDictionary[searchText];
                return translationText ?? searchText;
            }

            return NuggetFindRegex.Replace(text, ReplaceNuggets);
        }
    }
}
