using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core
{
    public class POMESReasons: BaseModel
    {
        public int Id { get; set; }
        public string PO { get; set; }

        [Column(TypeName = "jsonb")]
        public string Advance_Reasons { get; set; }

        [Column(TypeName = "jsonb")]
        public string Linestop_Reasons { get; set; }

        public string? ActualDateTime { get; set; }
    }
}