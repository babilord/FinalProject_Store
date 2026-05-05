using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinalProject_Store.Common.Dto
{
    public class ResultDto
    {
        public string Message { get; set; }
        public bool IsSuccess { get; set; }
    }
    public class ResultDto <T>
    {
        public string Message { get; set; }
        public bool IsSuccess { get; set; }
        public T Data { get;set; }  
    }
}
 