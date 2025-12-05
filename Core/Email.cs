using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core
{
    public class Email : BaseModel
    {
        public string EmailAddress { get; set; }
        public string Section { get; set; }

    }
}
