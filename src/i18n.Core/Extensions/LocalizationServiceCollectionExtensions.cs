using System;
using System.Diagnostics.CodeAnalysis;
using i18n.Core;
using i18n.Core.Abstractions;
using i18n.Core.Abstractions.Domain;
using i18n.Core.PortableObject;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Provides extension methods for <see cref="IServiceCollection"/>.
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedMethodReturnValue.Global")]
    public static class LocalizationServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the services to enable localization using Portable Object files.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="i18NLocaleDirectory"></param>
        /// <param name="requestLocalizationSetup">An action to configure the Microsoft.Extensions.Localization.LocalizationOptions.</param>
        [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
        public static IServiceCollection AddI18NLocalization([JetBrains.Annotations.NotNull] this IServiceCollection services, [JetBrains.Annotations.NotNull] string i18NLocaleDirectory, Action<RequestLocalizationOptions> requestLocalizationSetup = null)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (i18NLocaleDirectory == null) throw new ArgumentNullException(nameof(i18NLocaleDirectory));

            services.AddSingleton<IPluralRuleProvider, DefaultPluralRuleProvider>();
            services.AddSingleton<ITranslationProvider, PortableObjectFilesTranslationsProvider>();
            services.AddSingleton<ILocalizationFileLocationProvider, ContentRootPoFileLocationProvider>();
            services.AddSingleton<ILocalizationManager, LocalizationManager>();
            services.AddSingleton<IStringLocalizerFactory, PortableObjectStringLocalizerFactory>();
            services.AddSingleton<IHtmlLocalizerFactory, PortableObjectHtmlLocalizerFactory>();
            services.AddSingleton<ISettingsProvider>(x => new SettingsProvider(i18NLocaleDirectory));
           
            services.TryAddTransient(typeof(IStringLocalizer<>), typeof(StringLocalizer<>));

            if (requestLocalizationSetup != null)
            {
                services.Configure(requestLocalizationSetup);
            }

            services.AddSingleton(x => 
                Options.Options.Create(new I18NLocalizationOptions(x.GetRequiredService<ISettingsProvider>())));

            return services;
        }
    }
}
