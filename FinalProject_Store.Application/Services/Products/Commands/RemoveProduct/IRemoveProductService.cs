using FinalProject_Store.Application.Interfaces.Contexts;
using FinalProject_Store.Common.Dto;

namespace FinalProject_Store.Application.Services.Products.Commands.RemoveProduct
{
    public interface IRemoveProductService { ResultDto Execute(long productId); }
    public class RemoveProductService : IRemoveProductService
    {
        private readonly IDataBaseContext _context;
        public RemoveProductService(IDataBaseContext context) { _context = context; }
        public ResultDto Execute(long productId)
        {
            var product = _context.Products.FirstOrDefault(item => item.Id == productId);
            if (product == null) return new ResultDto { IsSuccess = false, Message = "محصول موردنظر پیدا نشد." };
            product.IsRemoved = true;
            product.RemoveTime = DateTime.Now;
            product.UpdateDate = DateTime.Now;
            _context.SaveChanges();
            return new ResultDto { IsSuccess = true, Message = "محصول با موفقیت حذف شد." };
        }
    }
}
