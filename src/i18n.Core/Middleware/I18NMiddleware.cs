using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using i18n.Core.Abstractions;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IO;

namespace i18n.Core.Middleware
{
    public interface IPooledStreamManager
    {
        Stream GetStream(string name);
        ValueTask ReturnStreamAsync(Stream stream);
    }

    public sealed class DefaultPooledStreamManager : IPooledStreamManager
    {
        static readonly RecyclableMemoryStreamManager PooledStreamManager = new RecyclableMemoryStreamManager();

        public Stream GetStream([NotNull] string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            return PooledStreamManager.GetStream(name);
        }

        public ValueTask ReturnStreamAsync([NotNull] Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            return stream.DisposeAsync();
        }
    }

    public sealed class I18NMiddlewareOptions
    {
        public ICollection<string> ValidContentTypes { get; }
        public ICollection<string> ExcludeUrls { get; }
        public bool CacheEnabled { get; [UsedImplicitly] set; }

        public I18NMiddlewareOptions()
        {
            ValidContentTypes = new List<string>
            {
                "text/html",
                "text/json",
                "application/json",
                "application/javascript"
            };

            ExcludeUrls = new List<string>
            {
                "/lib/",
                "/styles/",
                "/fonts/",
                "/images/"
            };
        }
    }

    public sealed class I18NMiddleware
    {
        readonly RequestDelegate _next;
        readonly ILocalizationManager _localizationManager;
        readonly ILogger<I18NMiddleware> _logger;
        readonly IPooledStreamManager _pooledStreamManager;
        readonly INuggetReplacer _nuggetReplacer;
        readonly I18NMiddlewareOptions _options;

        public I18NMiddleware(RequestDelegate next, ILocalizationManager localizationManager, IOptions<I18NMiddlewareOptions> middleWareOptions, 
            [CanBeNull] ILogger<I18NMiddleware> logger, IPooledStreamManager pooledStreamManager, INuggetReplacer nuggetReplacer)
        {
            _next = next;
            _localizationManager = localizationManager;
            _logger = logger;
            _pooledStreamManager = pooledStreamManager;
            _nuggetReplacer = nuggetReplacer;
            _options = middleWareOptions.Value;
        }

        // https://dejanstojanovic.net/aspnet/2018/august/minify-aspnet-mvc-core-response-using-custom-middleware-and-pipeline/
        // https://stackoverflow.com/a/60488054
        // https://github.com/turquoiseowl/i18n/issues/293#issuecomment-593399889

        [UsedImplicitly]
        public async Task InvokeAsync(HttpContext context)
        {
            var excludeUrls = _options.ExcludeUrls;
            var modifyResponse = excludeUrls == null || !excludeUrls.Any(bl => context.Request.Path.Value.ToLower().Contains(bl));

            if (!modifyResponse)
            {
                await _next(context);
                return;
            }

            context.Request.EnableBuffering();
            var originBody = ReplaceBody(context.Response);

            try
            {
                await _next(context);
            }
            catch
            {
                await ReturnBodyAsync(context.Response, originBody).ConfigureAwait(false);
                throw;
            }

            var requestCultureFeature = context.Features.Get<IRequestCultureFeature>();
            CultureInfo translationCultureInfo;
            if (requestCultureFeature == null)
            {
                _logger?.LogWarning($"{nameof(IRequestCultureFeature)} is not configured. Thread culture will be used instead.");
                translationCultureInfo = CultureInfo.DefaultThreadCurrentCulture;
            }
            else
            {
                translationCultureInfo = requestCultureFeature.RequestCulture.Culture;
            }

            var contentType = context.Response.ContentType?.ToLower();
            contentType = contentType?.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

            var validContentTypes = _options.ValidContentTypes;
            if (validContentTypes != null && validContentTypes.Contains(contentType))
            {
                string responseBody;
                using (var streamReader = new StreamReader(context.Response.Body))
                {
                    context.Response.Body.Seek(0, SeekOrigin.Begin);
                    responseBody = await streamReader.ReadToEndAsync().ConfigureAwait(false);
                }

                var cultureDictionary = _localizationManager.GetDictionary(translationCultureInfo, !_options.CacheEnabled);

                _logger?.LogDebug(
                    $"Request path: {context.Request.Path}. Culture name: {cultureDictionary.CultureName}. Translations: {cultureDictionary.Translations.Count}.");

                responseBody = _nuggetReplacer.Replace(cultureDictionary, responseBody);

                var requestContent = new StringContent(responseBody, Encoding.UTF8, contentType);
                context.Response.Body = await requestContent.ReadAsStreamAsync().ConfigureAwait(false);
                context.Response.ContentLength = context.Response.Body.Length;
            }

            await ReturnBodyAsync(context.Response, originBody).ConfigureAwait(false);
        }
        
        Stream ReplaceBody(HttpResponse response)
        {
            var originBody = response.Body;
            response.Body = _pooledStreamManager.GetStream(nameof(I18NMiddleware)) ?? 
                            throw new Exception($"{nameof(_pooledStreamManager)} must return a valid stream.");
            response.Body.Seek(0, SeekOrigin.Begin);
            return originBody;
        }

        async ValueTask ReturnBodyAsync(HttpResponse response, Stream originBody)
        {
            response.Body.Seek(0, SeekOrigin.Begin);
            await response.Body.CopyToAsync(originBody).ConfigureAwait(false);
            await _pooledStreamManager.ReturnStreamAsync(response.Body).ConfigureAwait(false);
            response.Body = originBody;
        }
        
    }
}