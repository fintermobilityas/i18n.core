using System;
using i18n.Core.Abstractions.Domain;
using i18n.Core.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace i18n.Core.Extensions
{
    public static class MiddlewareExtensions
    {
        public static IApplicationBuilder UseI18NMiddleware(this IApplicationBuilder app, IWebHostEnvironment webHostEnvironment, Func<I18NLocalizationOptions> setupAction)
        {
            app.UseMiddleware<I18NMiddleware>(setupAction?.Invoke() ?? new I18NLocalizationOptions(new SettingsProvider(webHostEnvironment.WebRootPath)));
            return app;
        }
    }
}
