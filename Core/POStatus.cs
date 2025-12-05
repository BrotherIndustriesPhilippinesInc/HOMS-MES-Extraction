using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core
{
    public class POStatus
    {
        public int Id { get; set; }
        public string PO { get; set; }
        public string POType { get; set; }
        public string ModelCode { get; set; }
        public int PlannedQty { get; set; }
        public int ProducedQty { get; set; }
        public int FinishedQty { get; set; }
        public string Production { get; set; }
        public string ProdLine { get; set; }
        public string? Status { get; set; }
        public string? ActualStart { get; set; }
        public Decimal? ComplianceRate { get; set; }

        public DateTime StartDateTime { get; set; }
    }
}
