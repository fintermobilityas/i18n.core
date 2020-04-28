using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using i18n.Core.Abstractions;
using Microsoft.Extensions.Caching.Memory;

namespace i18n.Core
{
    /// <summary>
    /// Represents a manager that manage the localization resources.
    /// </summary>
    public class LocalizationManager : ILocalizationManager
    {
        const string CacheKeyPrefix = "CultureDictionary-";

        static readonly PluralizationRuleDelegate DefaultPluralRule = n => n != 1 ? 1 : 0;

        readonly IList<IPluralRuleProvider> _pluralRuleProviders;
        readonly ITranslationProvider _translationProvider;
        readonly IMemoryCache _cache;
        readonly INuggetReplacer _nuggetReplacer;

        /// <summary>
        /// Creates a new instance of <see cref="LocalizationManager"/>.
        /// </summary>
        /// <param name="pluralRuleProviders">A list of <see cref="IPluralRuleProvider"/>s.</param>
        /// <param name="translationProvider">The <see cref="ITranslationProvider"/>.</param>
        /// <param name="cache">The <see cref="IMemoryCache"/>.</param>
        /// <param name="nuggetReplacer"></param>
        public LocalizationManager(
            IEnumerable<IPluralRuleProvider> pluralRuleProviders,
            ITranslationProvider translationProvider,
            IMemoryCache cache,
            INuggetReplacer nuggetReplacer)
        {
            _pluralRuleProviders = pluralRuleProviders.OrderBy(o => o.Order).ToArray();
            _translationProvider = translationProvider;
            _cache = cache;
            _nuggetReplacer = nuggetReplacer;
        }

        /// <inheritdocs />
        public CultureDictionary GetDictionary(CultureInfo culture, bool disableCache = false)
        {
            var cacheKeyPrefix = CacheKeyPrefix + culture.Name;

            if (disableCache)
            {
                _cache.Remove(cacheKeyPrefix);
            }

            var cachedDictionary = _cache.GetOrCreate(cacheKeyPrefix, k => new Lazy<CultureDictionary>(() =>
            {
                var rule = DefaultPluralRule;

                foreach (var provider in _pluralRuleProviders)
                {
                    if (provider.TryGetRule(culture, out rule))
                    {
                        break;
                    }
                }

                var dictionary = new CultureDictionary(culture.Name, rule ?? DefaultPluralRule);
                _translationProvider.LoadTranslations(culture, dictionary);

                return dictionary;
            }, LazyThreadSafetyMode.ExecutionAndPublication));

            return cachedDictionary.Value;
        }

        /// <inheritdocs />
        public string Translate(CultureInfo culture, string text)
        {
            var cultureDictionary = GetDictionary(culture);
            return _nuggetReplacer.Replace(cultureDictionary, text);
        }
    }
}
