using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Implementation;
using OrchardCore.Environment.Cache;

namespace OrchardCore.DynamicCache.EventHandlers
{
    /// <summary>
    /// Caches shapes in the default <see cref="IDynamicCacheService"/> implementation.
    /// It uses the shape's metadata cache context to define the cache parameters.
    /// </summary>
    public class DynamicCacheShapeDisplayEvents : IShapeDisplayEvents
    {
        private readonly Dictionary<string, CacheContext> _cached = new Dictionary<string, CacheContext>();
        private readonly Dictionary<string, CacheContext> _openScopes = new Dictionary<string, CacheContext>();

        private readonly IDynamicCacheService _dynamicCacheService;
        private readonly ICacheScopeManager _cacheScopeManager;
        private readonly HtmlEncoder _htmlEncoder;
        private readonly CacheOptions _cacheOptions;

        public DynamicCacheShapeDisplayEvents(
            IDynamicCacheService dynamicCacheService,
            ICacheScopeManager cacheScopeManager,
            HtmlEncoder htmlEncoder,
            IOptions<CacheOptions> options)
        {
            _dynamicCacheService = dynamicCacheService;
            _cacheScopeManager = cacheScopeManager;
            _htmlEncoder = htmlEncoder;
            _cacheOptions = options.Value;
        }

        public async Task DisplayingAsync(ShapeDisplayContext context)
        {
            // The shape has cache settings and no content yet
            if (context.Shape.Metadata.IsCached && context.ChildContent == null)
            {
                var cacheContext = context.Shape.Metadata.Cache();
                _cacheScopeManager.EnterScope(cacheContext);
                _openScopes[cacheContext.CacheId] = cacheContext;

                var cachedContent = await _dynamicCacheService.GetCachedValueAsync(cacheContext);

                if (cachedContent != null)
                {
                    // The contents of this shape was found in the cache.
                    // Add the cacheContext to _cached so that we don't try to cache the content again in the DisplayedAsync method.
                    _cached[cacheContext.CacheId] = cacheContext;
                    context.ChildContent = new HtmlString(cachedContent);
                }
                else if (_cacheOptions.DebugMode)
                {
                    context.Shape.Metadata.Wrappers.Add("CachedShapeWrapper");
                }
            }
        }

        public async Task DisplayedAsync(ShapeDisplayContext context)
        {
            var cacheContext = context.Shape.Metadata.Cache();

            // If the shape is not configured to be cached, continue as usual
            if (cacheContext == null)
            {
                if (context.ChildContent == null)
                {
                    context.ChildContent = HtmlString.Empty;
                }

                return;
            }

            // If we have got this far, then this shape is configured to be cached.
            // We need to determine whether or not the ChildContent of this shape was retrieved from the cache by the DisplayingAsync method above, as opposed to generated by the View Engine.
            // ChildContent will be generated by the View Engine if it was not available in the cache when we rendered the shape.
            // In this instance, we need insert the ChildContent into the cache so that subsequent attempt to render this shape can take advantage of the cached content.

            // If the ChildContent was retrieved form the cache, then the Cache Context will be present in the _cached collection (see the DisplayingAsync method in this class).
            // So, if the cache context is not present in the _cached collection, we need to insert the ChildContent value into the cache:
            if (!_cached.ContainsKey(cacheContext.CacheId) && context.ChildContent != null)
            {
                // The content is pre-encoded in the cache so we don't have to do it every time it's rendered
                using var sb = StringBuilderPool.GetInstance();
                using var sw = new StringWriter(sb.Builder);

                // 'ChildContent' may be a 'ViewBufferTextWriterContent' on which we can't
                // call 'WriteTo()' twice, so here we update it with a new 'HtmlString()'.
                context.ChildContent.WriteTo(sw, _htmlEncoder);
                var contentHtmlString = new HtmlString(sw.ToString());
                context.ChildContent = contentHtmlString;

                await _dynamicCacheService.SetCachedValueAsync(cacheContext, contentHtmlString.Value);
                await sw.FlushAsync();
            }
        }

        public Task DisplayingFinalizedAsync(ShapeDisplayContext context)
        {
            var cacheContext = context.Shape.Metadata.Cache();

            if (cacheContext != null && _openScopes.ContainsKey(cacheContext.CacheId))
            {
                _cacheScopeManager.ExitScope();
                _openScopes.Remove(cacheContext.CacheId);
            }

            return Task.CompletedTask;
        }
    }
}
