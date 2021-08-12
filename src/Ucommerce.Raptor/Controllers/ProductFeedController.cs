using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using Raptor.Ucommerce.Exceptions;
using Raptor.Ucommerce.Models;
using Ucommerce;
using Ucommerce.EntitiesV2;
using Ucommerce.Infrastructure;
using Ucommerce.Search;
using Ucommerce.Search.Slugs;
using Umbraco.Web.WebApi;
using PriceGroup = Ucommerce.Search.Models.PriceGroup;
using Product = Ucommerce.Search.Models.Product;
using ProductCatalog = Ucommerce.Search.Models.ProductCatalog;

namespace Raptor.Ucommerce.Controllers
{
    public class ProductFeedController : UmbracoApiController
    {
        public int  BatchSize => 200;
        public int MaxDegreeOfParallelism => 5;

        private IUrlService _urlService;
        private IIndex<Product> _productIndex;
        private IIndex<ProductCatalog> _catalogIndex;
        private IIndex<PriceGroup> _priceGroupIndex;
        private ISessionProvider _sessionProvider;

        public IHttpActionResult Get(string cultureCode, string catalogId = null)
        {
            //Resolve Ucommerce dependencies
            _urlService = ObjectFactory.Instance.Resolve<IUrlService>();
            _productIndex = ObjectFactory.Instance.Resolve<IIndex<Product>>();
            _catalogIndex = ObjectFactory.Instance.Resolve<IIndex<ProductCatalog>>();
            _priceGroupIndex = ObjectFactory.Instance.Resolve<IIndex<PriceGroup>>();
            _sessionProvider = ObjectFactory.Instance.Resolve<ISessionProvider>();

            var currentCatalog = catalogId != null && Guid.TryParse(catalogId, out var catalogGuid)? FindCatalog(catalogGuid, cultureCode) : FindCatalog(Guid.Empty, cultureCode);

            if (currentCatalog == null) throw new ObjectNotFoundException(catalogId, typeof(ProductCatalog));

            //Getting products that a visible on the site without any variants.
            //Changes this logic to include variants if you have variant specific pages in your shop
            var productGuids = LoadAllProductGuids(currentCatalog.Guid);

            return Json<IList<ProductFeedModel>>(MapProducts(productGuids, currentCatalog, cultureCode).ToList());
        }

        private IList<Guid> LoadAllProductGuids(Guid catalogId)
        {
            using var session = _sessionProvider.GetSession();
            return session.Query<global::Ucommerce.EntitiesV2.Product>()
                .Where(x => x.CategoryProductRelations.Any(c => c.Category.ProductCatalog.Guid == catalogId) && x.ParentProduct == null && x.DisplayOnSite)
                .Select(x => x.Guid)
                .ToList();
        }

        private ProductCatalog FindCatalog(Guid catalogId, string cultureCode)
        {
            var query = _catalogIndex.Find(CultureInfo.CreateSpecificCulture(cultureCode));

            if (catalogId != Guid.Empty)
                query = query.Where(x => x.Guid == catalogId);

            return query
                .FirstOrDefault();
        }

        private ResultSet<Product> FindProducts(IList<Guid> productGuids, string cultureCode)
        {
            return _productIndex.Find(CultureInfo.CreateSpecificCulture(cultureCode))
                .Where(x => productGuids.Contains(x.Guid))
                .Take(Convert.ToUInt32(BatchSize))
                .ToList();
        }

        private IList<ProductFeedModel> MapProducts(IList<Guid> productGuids, ProductCatalog catalog, string cultureCode)
        {
            var result = new List<ProductFeedModel>();

            Parallel.ForEach(
                Batch(productGuids),
                new ParallelOptions { MaxDegreeOfParallelism = MaxDegreeOfParallelism },
                (productGuidsInProcess) =>
                {
                    var products = FindProducts(productGuidsInProcess, cultureCode);
                    foreach (var product in products)
                    {
                        result.Add(MapProduct(product, catalog));
                    }
                }
            );

            return result;
        }

        private ProductFeedModel MapProduct(Product product, ProductCatalog catalog)
        {
            var productUrl = _urlService.GetUrl(catalog, product);

            var priceGroup = _priceGroupIndex.Find().Where(x => x.Guid == catalog.DefaultPriceGroup).First();
            var price = product.PricesInclTax[priceGroup.Name];

            return new ProductFeedModel()
            {
                ProductId = product.Guid.ToString(),
                DisplayName = product.DisplayName,
                ShortDescription = product.ShortDescription,
                Price = new Money(price, priceGroup.CurrencyISOCode).ToString(),
                ImageUrl = product.ThumbnailImageUrl,
                Url = productUrl
            };
        }

        private IEnumerable<List<Guid>> Batch(IList<Guid> data)
        {
            var batch = new List<Guid>(BatchSize);
            foreach (var o in data)
            {
                batch.Add(o);
                if (batch.Count == BatchSize)
                {
                    var rc = batch;
                    batch = new List<Guid>(BatchSize);
                    yield return rc;
                }
            }
            if (batch.Count > 0)
            {
                yield return batch;
            }
        }
    }
}
