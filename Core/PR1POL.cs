using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core
{
    [Table("pr_one_pol")]
    public class PR1POL
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        [Column("work_center")]
        public string WorkCenter { get; set; }
        [Column("line_name")]
        public string LineName { get; set; }
        [Column("pc_status")]
        public string? PCStatus { get; set; }
        [Column("prod_status")]
        public string? ProdStatus { get; set; }
        [Column("prd_order_no")]
        public string PrdOrderNo { get; set; }
        [Column("sales_order")]
        public string SalesOrder { get; set; }
        [Column("material")]
        public string Material { get; set; }
        [Column("description")]
        public string Description { get; set; }
        [Column("qty")]
        public int Qty { get; set; }

        [Column("creator")]
        public string Creator { get; set; }
        [Column("time_created")]
        public DateTime TimeCreated { get; set; }
        [Column("updated_by")]
        public string? UpdatedBy { get; set; }
        [Column("time_updated")]
        public DateTime? TimeUpdated { get; set; }


    }
}
