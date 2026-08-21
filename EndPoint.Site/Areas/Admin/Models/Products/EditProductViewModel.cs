using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace EndPoint.Site.Areas.Admin.Models.Products
{
    public class EditProductViewModel
    {
        public long Id { get; set; }

        [Required(ErrorMessage = "نام محصول را وارد کنید.")]
        [MinLength(2, ErrorMessage = "نام محصول باید حداقل دو کاراکتر باشد.")]
        [MaxLength(300, ErrorMessage = "نام محصول نمی‌تواند بیشتر از ۳۰۰ کاراکتر باشد.")]
        [Display(Name = "نام محصول")]
        public string Name { get; set; }

        [MaxLength(200, ErrorMessage = "نام برند نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد.")]
        [Display(Name = "برند")]
        public string Brand { get; set; }

        [MaxLength(4000, ErrorMessage = "توضیحات نمی‌تواند بیشتر از ۴۰۰۰ کاراکتر باشد.")]
        [Display(Name = "توضیحات")]
        public string Description { get; set; }

        [Range(1, double.MaxValue, ErrorMessage = "قیمت محصول باید بیشتر از صفر باشد.")]
        [Display(Name = "قیمت")]
        public decimal Price { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "موجودی محصول نمی‌تواند منفی باشد.")]
        [Display(Name = "موجودی")]
        public int Inventory { get; set; }

        [Range(1, long.MaxValue, ErrorMessage = "دسته‌بندی محصول را انتخاب کنید.")]
        [Display(Name = "دسته‌بندی")]
        public long CategoryId { get; set; }

        [Display(Name = "فعال")]
        public bool IsActive { get; set; }

        public List<SelectListItem> Categories { get; set; } = new List<SelectListItem>();
    }
}
