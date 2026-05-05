using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinalProject_Store.Common
{
    public static class Pagination
    {
        // this IEnumerable <TSource>: TSource یعنی یک کوئری از ما میگیره
        public static IEnumerable<TSource> ToPaged <TSource>(this IEnumerable<TSource> source, int page, int pageSize, out int rowCount )
        {
            rowCount = source.Count();
            return source.Skip((page - 1) * pageSize).Take(pageSize);
        }
    }
}
