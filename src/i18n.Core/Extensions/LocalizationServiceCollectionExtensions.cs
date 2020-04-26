using System;
using i18n.Core;
using i18n.Core.Abstractions;
using i18n.Core.Middleware;
using i18n.Core.PortableObject;
using JetBrains.Annotations;
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
    public static class LocalizationServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the services to enable localization using Portable Object files.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        [UsedImplicitly]
        public static IServiceCollection AddI18NLocalization(this IServiceCollection services)
        {
            return AddI18NLocalization(services, null);
        }

        /// <summary>
        /// Registers the services to enable localization using Portable Object files.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="setupAction">An action to configure the Microsoft.Extensions.Localization.LocalizationOptions.</param>
        public static IServiceCollection AddI18NLocalization(this IServiceCollection services, Action<LocalizationOptions> setupAction)
        {
            services.AddSingleton<IPluralRuleProvider, DefaultPluralRuleProvider>();
            services.AddSingleton<ITranslationProvider, PoFilesTranslationsProvider>();
            services.AddSingleton<ILocalizationFileLocationProvider, ContentRootPoFileLocationProvider>();
            services.AddSingleton<ILocalizationManager, LocalizationManager>();
            services.AddSingleton<IStringLocalizerFactory, PortableObjectStringLocalizerFactory>();
            services.AddSingleton<IHtmlLocalizerFactory, PortableObjectHtmlLocalizerFactory>();
            services.TryAddTransient(typeof(IStringLocalizer<>), typeof(StringLocalizer<>));

            if (setupAction != null)
            {
                services.Configure(setupAction);
            }

            return services;
        }

        public static IApplicationBuilder AddI18NLocalizationMiddlware(this IApplicationBuilder app)
        {
            app.UseMiddleware<I18NMiddleware>();
            return app;
        }
    }
}
