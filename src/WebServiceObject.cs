using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atomus.Service
{
    public class WebServiceObject
    {
        public object Object { get; set; }
        public CompilerResults CompilerResults { get; set; }
        //public bool IsFirstExecute { get; set; }
        public WebServiceInfo WebServiceInfo { get; set; }
    }
}
