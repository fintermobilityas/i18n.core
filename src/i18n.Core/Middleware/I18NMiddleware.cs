using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using i18n.Core.Abstractions;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Hosting;
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

        public Stream GetStream([JetBrains.Annotations.NotNull] string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            return PooledStreamManager.GetStream(name);
        }

        public ValueTask ReturnStreamAsync([JetBrains.Annotations.NotNull] Stream stream)
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
        public Encoding RequestEncoding { get; [UsedImplicitly] set; }
        public int RequestBufferingThreshold { get; [UsedImplicitly] set; }

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

            RequestEncoding = Encoding.UTF8;
            RequestBufferingThreshold = 84000; // Less than default GC LOH
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
        public async Task InvokeAsync(HttpContext context, IWebHostEnvironment webHostEnvironment)
        {
            var excludeUrls = _options.ExcludeUrls;
            var modifyResponse = excludeUrls == null || !excludeUrls.Any(bl => context.Request.Path.Value != null 
                                                                               && context.Request.Path.Value.ToLowerInvariant().Contains(bl));
            if (!modifyResponse)
            {
                await _next(context);
                return;
            }

            context.Request.EnableBuffering(_options.RequestBufferingThreshold);
            var originalResponseBodyStream = ReplaceHttpResponseBodyStream(context.Response);

            try
            {
                await _next(context);
            }
            catch
            {
                await ReturnHttpResponseBodyStreamAsync(context.Response, originalResponseBodyStream).ConfigureAwait(false);
                throw;
            }

            var contentType = GetRequestContentType(context);
            var validContentTypes = _options.ValidContentTypes;
            var replaceNuggets = validContentTypes != null && validContentTypes.Contains(contentType);
            if (replaceNuggets)
            {
                var requestCultureInfo = GetRequestCultureInfo(context);
                var cultureDictionary = _localizationManager.GetDictionary(requestCultureInfo, !_options.CacheEnabled);

                _logger?.LogDebug(
                    $"Request path: {context.Request.Path}. Culture name: {cultureDictionary.CultureName}. Translations: {cultureDictionary.Translations.Count}.");

                var responseBody = await ReadResponseBodyAsStringAsync(context);

                string responseBodyTranslated;
                if (webHostEnvironment.IsDevelopment())
                {
                    var sw = new Stopwatch();
                    sw.Restart();
                    responseBodyTranslated = _nuggetReplacer.Replace(cultureDictionary, responseBody);
                    sw.Stop();

                    _logger?.LogDebug($"Replaced body in {sw.ElapsedMilliseconds} ms.");
                    const string i18NMiddlewareName = "X-" + nameof(I18NMiddleware) + "-Ms";
                    context.Response.Headers[i18NMiddlewareName] = sw.ElapsedMilliseconds.ToString();
                }
                else
                {
                    responseBodyTranslated = _nuggetReplacer.Replace(cultureDictionary, responseBody);
                }

                context.Response.Body = originalResponseBodyStream;
                await context.Response.WriteAsync(responseBodyTranslated);

                return;
            }

            await ReturnHttpResponseBodyStreamAsync(context.Response, originalResponseBodyStream).ConfigureAwait(false);
        }

        [SuppressMessage("ReSharper", "ConstantConditionalAccessQualifier")]
        static string GetRequestContentType(HttpContext context)
        {
            return context.Response.ContentType?.ToLower()?.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        }

        CultureInfo GetRequestCultureInfo(HttpContext context)
        {
            var requestCultureFeature = context.Features.Get<IRequestCultureFeature>();

            CultureInfo requestCultureInfo;
            if (requestCultureFeature == null)
            {
                _logger?.LogWarning(
                    $"{nameof(IRequestCultureFeature)} is not configured. {nameof(CultureInfo.DefaultThreadCurrentCulture)} will be used as fallback culture.");
                requestCultureInfo = CultureInfo.DefaultThreadCurrentCulture;
            }
            else
            {
                requestCultureInfo = requestCultureFeature.RequestCulture.Culture;
            }

            return requestCultureInfo;
        }

        static async Task<string> ReadResponseBodyAsStringAsync(HttpContext context)
        {
            context.Response.Body.Seek(0, SeekOrigin.Begin);

            string responseBody;
            using (var streamReader = new StreamReader(context.Response.Body, Encoding.UTF8, false, leaveOpen: true))
            {
                responseBody = await streamReader.ReadToEndAsync().ConfigureAwait(false);
            }

            context.Response.Body.Seek(0, SeekOrigin.Begin);

            return responseBody;
        }

        Stream ReplaceHttpResponseBodyStream(HttpResponse httpResponse)
        {
            var originBody = httpResponse.Body;
            httpResponse.Body = _pooledStreamManager.GetStream(nameof(I18NMiddleware)) ?? 
                            throw new Exception($"{nameof(_pooledStreamManager)} must return a valid stream.");
            httpResponse.Body.Seek(0, SeekOrigin.Begin);
            httpResponse.RegisterForDisposeAsync(new DisposablePooledStream(_pooledStreamManager, httpResponse.Body));
            return originBody;
        }

        static async ValueTask ReturnHttpResponseBodyStreamAsync(HttpResponse httpResponse, Stream originalBodyResponseStream)
        {
            httpResponse.Body.Seek(0, SeekOrigin.Begin);
            await httpResponse.Body.CopyToAsync(originalBodyResponseStream).ConfigureAwait(false);
            httpResponse.Body = originalBodyResponseStream;
        }
        
        readonly struct DisposablePooledStream : IAsyncDisposable
        {
            readonly IPooledStreamManager _pooledStreamManager;
            readonly Stream _stream;

            public DisposablePooledStream(IPooledStreamManager pooledStreamManager, Stream stream)
            {
                _pooledStreamManager = pooledStreamManager;
                _stream = stream;
            }

            public ValueTask DisposeAsync()
            {
                return _pooledStreamManager.ReturnStreamAsync(_stream);
            }
        }
    }
}