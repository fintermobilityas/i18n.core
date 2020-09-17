using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using i18n.Core.Abstractions;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
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
            var cancellationToken = context.RequestAborted;
            var requestEncoding = _options.RequestEncoding ?? Encoding.UTF8;
            var excludeUrls = _options.ExcludeUrls;
            var modifyResponse = excludeUrls == null || !excludeUrls.Any(bl => context.Request.Path.Value != null
                                                                               && context.Request.Path.Value.ToLowerInvariant().Contains(bl));
            if (!modifyResponse)
            {
                await _next(context);
                return;
            }

            var responseBodyPooledStream = new DisposablePooledStream(_pooledStreamManager, nameof(I18NMiddleware));
            context.Response.RegisterForDisposeAsync(responseBodyPooledStream);

            var httpResponseBodyFeature = context.Features.Get<IHttpResponseBodyFeature>();
            var httpResponseFeature = context.Features.Get<IHttpResponseFeature>();

            var streamResponseBodyFeature = new StreamResponseBodyFeature(responseBodyPooledStream);
            context.Features.Set<IHttpResponseBodyFeature>(streamResponseBodyFeature);

            await _next(context).ConfigureAwait(false);

            // Force dynamic content type in order reset Content-Length header.
            httpResponseFeature.Headers.ContentLength = null;

            var httpResponseBodyStream = (Stream) responseBodyPooledStream;
            httpResponseBodyStream.Seek(0, SeekOrigin.Begin);

            var contentType = GetRequestContentType(context);
            var validContentTypes = _options.ValidContentTypes;
            var replaceNuggets = validContentTypes != null && validContentTypes.Contains(contentType);
            if (replaceNuggets)
            {
                var requestCultureInfo = GetRequestCultureInfo(context);
                var cultureDictionary = _localizationManager.GetDictionary(requestCultureInfo, !_options.CacheEnabled);

                _logger?.LogDebug(
                    $"Request path: {context.Request.Path}. Culture name: {cultureDictionary.CultureName}. Translations: {cultureDictionary.Translations.Count}.");

                var responseBody = await ReadResponseBodyAsStringAsync(httpResponseBodyStream, requestEncoding);
                
                string responseBodyTranslated;
                if (webHostEnvironment.IsDevelopment())
                {
                    var sw = new Stopwatch();
                    sw.Restart();
                    responseBodyTranslated = _nuggetReplacer.Replace(cultureDictionary, responseBody);
                    sw.Stop();

                    _logger?.LogDebug($"Replaced body in {sw.ElapsedMilliseconds} ms.");
                    const string i18NMiddlewareName = "X-" + nameof(I18NMiddleware) + "-Ms";
                    httpResponseFeature.Headers[i18NMiddlewareName] = sw.ElapsedMilliseconds.ToString();
                }
                else
                {
                    responseBodyTranslated = _nuggetReplacer.Replace(cultureDictionary, responseBody);
                }

                var stringContent = new StringContent(responseBodyTranslated, requestEncoding, contentType);
                await stringContent.CopyToAsync(httpResponseBodyFeature.Stream, cancellationToken);

                return;
            }

            await httpResponseBodyStream.CopyToAsync(httpResponseBodyFeature.Stream, cancellationToken).ConfigureAwait(false);
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

        static async Task<string> ReadResponseBodyAsStringAsync(Stream stream, Encoding encoding)
        {
            using var streamReader = new StreamReader(stream, encoding, false);
            return await streamReader.ReadToEndAsync().ConfigureAwait(false);
        }

        readonly struct DisposablePooledStream : IAsyncDisposable
        {
            readonly IPooledStreamManager _pooledStreamManager;
            readonly Stream _stream;

            public static implicit operator Stream(DisposablePooledStream stream) => stream._stream;

            public DisposablePooledStream(IPooledStreamManager pooledStreamManager, string streamName)
            {
                _pooledStreamManager = pooledStreamManager;
                _stream = _pooledStreamManager.GetStream(streamName);
            }

            public ValueTask DisposeAsync()
            {
                return _pooledStreamManager.ReturnStreamAsync(_stream);
            }
        }
    }
}