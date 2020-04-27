using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using i18n.Core.Abstractions;
using JetBrains.Annotations;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
        readonly ILogger<LocalizationManager> _logger;
        readonly bool _isDevelopmentEnvironment;

        public bool DisableCache { get; set; }

        /// <summary>
        /// Creates a new instance of <see cref="LocalizationManager"/>.
        /// </summary>
        /// <param name="pluralRuleProviders">A list of <see cref="IPluralRuleProvider"/>s.</param>
        /// <param name="translationProvider">The <see cref="ITranslationProvider"/>.</param>
        /// <param name="cache">The <see cref="IMemoryCache"/>.</param>
        /// <param name="hostEnvironment"></param>
        /// <param name="logger"></param>
        public LocalizationManager(
            IEnumerable<IPluralRuleProvider> pluralRuleProviders,
            ITranslationProvider translationProvider,
            IMemoryCache cache,
            IHostEnvironment hostEnvironment,
            [CanBeNull] ILogger<LocalizationManager> logger)
        {
            _pluralRuleProviders = pluralRuleProviders.OrderBy(o => o.Order).ToArray();
            _translationProvider = translationProvider;
            _cache = cache;
            _logger = logger;
            _isDevelopmentEnvironment = hostEnvironment.IsDevelopment();

            DisableCache = _isDevelopmentEnvironment;
        }

        /// <inheritdocs />
        public CultureDictionary GetDictionary(CultureInfo culture)
        {
            var cacheKeyPrefix = CacheKeyPrefix + culture.Name;

            if (DisableCache)
            {
                _cache.Remove(cacheKeyPrefix);

                if (_isDevelopmentEnvironment)
                {
                    _logger?.LogWarning("Cache is disabled. Translations will be built per request. This is only normal during development.");
                }
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
    }
}
