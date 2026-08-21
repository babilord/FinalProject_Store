using FinalProject_Store.Application.Interfaces.Contexts;
using FinalProject_Store.Common.Dto;

namespace FinalProject_Store.Application.Services.Products.Commands.ProductStatusChange
{
    public interface IProductStatusChangeService { ResultDto Execute(long productId); }
    public class ProductStatusChangeService : IProductStatusChangeService
    {
        private readonly IDataBaseContext _context;
        public ProductStatusChangeService(IDataBaseContext context) { _context = context; }
        public ResultDto Execute(long productId)
        {
            var product = _context.Products.FirstOrDefault(item => item.Id == productId);
            if (product == null) return new ResultDto { IsSuccess = false, Message = "محصول موردنظر پیدا نشد." };
            product.IsActive = !product.IsActive;
            product.UpdateDate = DateTime.Now;
            _context.SaveChanges();
            return new ResultDto { IsSuccess = true, Message = $"محصول با موفقیت {(product.IsActive ? "فعال" : "غیرفعال")} شد." };
        }
    }
}
