using i18n.Core.Abstractions.Domain;
using i18n.Core.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace i18n.Core.Extensions
{
    public static class MiddlewareExtensions
    {
        public static IApplicationBuilder UseI18NRequestLocalization(this IApplicationBuilder app)
        {
            var i18NLocalizationOptions = app.ApplicationServices.GetRequiredService<IOptions<I18NLocalizationOptions>>();
            var localizationOptions = app.ApplicationServices.GetRequiredService<IOptions<LocalizationOptions>>();
            var requestLocationOptions = app.ApplicationServices.GetRequiredService<IOptions<RequestLocalizationOptions>>();
            localizationOptions.Value.ResourcesPath = i18NLocalizationOptions.Value.LocaleDirectory;

            app.UseRequestLocalization(requestLocationOptions.Value);
            app.UseMiddleware<I18NMiddleware>();

            return app;
        }
    }
}
