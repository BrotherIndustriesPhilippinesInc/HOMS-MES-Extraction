using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Core
{
    [Table("production_records", Schema = "public")]
    public class ProductionRecord
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("po_id")]
        public int? PoId { get; set; }

        [Column("po")]
        public string Po { get; set; }

        [Column("section")]
        public string Section { get; set; }

        [Column("work_center")]
        public string WorkCenter { get; set; }

        [Column("area")]
        public string Area { get; set; }

        [Column("line_name")]
        public string LineName { get; set; }

        [Column("material")]
        public string Material { get; set; }

        [Column("description")]
        public string Description { get; set; }

        [Column("plan_quantity")]
        public int PlanQuantity { get; set; }

        [Column("takt_time")]
        public decimal TaktTime { get; set; } // Use decimal for precision in time/money

        [Column("actual_quantity")]
        public int ActualQuantity { get; set; }

        [Column("variance")]
        public int Variance { get; set; }

        [Column("shift")]
        public string Shift { get; set; }

        [Column("hourly_time")]
        public TimeSpan? HourlyTime { get; set; }

        [Column("direct_operators")]
        public int DirectOperators { get; set; }

        [Column("start_time")]
        public DateTime? StartTime { get; set; }

        [Column("end_time")]
        public DateTime? EndTime { get; set; }

        [Column("creator")]
        public string Creator { get; set; }

        [Column("time_created")]
        public DateTime TimeCreated { get; set; } = DateTime.UtcNow;

        [Column("updated_by")]
        public string UpdatedBy { get; set; }

        [Column("time_updated")]
        public DateTime? TimeUpdated { get; set; }

        [Column("production_action")]
        public string ProductionAction { get; set; }

        [Column("hourly_plan")]
        public int HourlyPlan { get; set; }

        [Column("target")]
        public int Target { get; set; }

        [Column("compliance_rate")]
        public double ComplianceRate { get; set; }

        [Column("esp_id")]
        public int? EspId { get; set; }

        [Column("advance_reasons")]
        public string AdvanceReasons { get; set; }

        [Column("linestop_reasons")]
        public string LinestopReasons { get; set; }

        [Column("unique_session")]
        public Guid? UniqueSession { get; set; } // Often UUID in Postgres

        [Column("ended_by")]
        public string EndedBy { get; set; }

        [Column("commulative_plan")]
        public int CommulativePlan { get; set; }

        [Column("commulative_actual")]
        public int CommulativeActual { get; set; }

        [Column("islinestop")]
        public bool IsLinestop { get; set; }

        [Column("actual_quantity_sum")]
        public int ActualQuantitySum { get; set; }

        [Column("original_plan")]
        public int OriginalPlan { get; set; }
    }
}