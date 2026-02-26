using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Core;
using HOMS_MES_Extractor_Web.Data;
using Newtonsoft.Json;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace HOMS_MES_Extractor_Web.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class POMESReasonsController : ControllerBase
    {
        private readonly HOMS_MES_Extractor_WebContext _context;

        public POMESReasonsController(HOMS_MES_Extractor_WebContext context)
        {
            _context = context;
        }

        // GET: api/POMESReasons
        [HttpGet]
        public async Task<ActionResult<IEnumerable<POMESReasons>>> GetPOMESReasons()
        {
            return await _context.POMESReasons.ToListAsync();
        }

        // GET: api/POMESReasons/5
        [HttpGet("{id}")]
        public async Task<ActionResult<POMESReasons>> GetPOMESReasons(int id)
        {
            var pOMESReasons = await _context.POMESReasons.FindAsync(id);

            if (pOMESReasons == null)
            {
                return NotFound();
            }

            return pOMESReasons;
        }

        // PUT: api/POMESReasons/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutPOMESReasons(int id, POMESReasons pOMESReasons)
        {
            if (id != pOMESReasons.Id)
            {
                return BadRequest();
            }

            _context.Entry(pOMESReasons).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!POMESReasonsExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/POMESReasons
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<POMESReasons>> PostPOMESReasons(POMESReasons pOMESReasons)
        {
            _context.POMESReasons.Add(pOMESReasons);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetPOMESReasons", new { id = pOMESReasons.Id }, pOMESReasons);
        }

        // DELETE: api/POMESReasons/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePOMESReasons(int id)
        {
            var pOMESReasons = await _context.POMESReasons.FindAsync(id);
            if (pOMESReasons == null)
            {
                return NotFound();
            }

            _context.POMESReasons.Remove(pOMESReasons);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool POMESReasonsExists(int id)
        {
            return _context.POMESReasons.Any(e => e.Id == id);
        }


        public class POMESReasonsData
        {
            public string PO { get; set; }
            public List<AdvanceReasonModel> Advance_ReasonsData { get; set; }
            public List<LinestopReasonModel> Linestop_ReasonsData { get; set; }
            //public string ForDateTimeData { get; set; }
        }
        public class AdvanceReasonModel
        {
            public string action_id { get; set; }
            public string action_label { get; set; }
            public string action_notes { get; set; }

            public string reason_id { get; set; }
            public string reason_label { get; set; }
            public string reason_notes { get; set; }
        }

        public class LinestopReasonModel
        {
            public string action_id { get; set; }
            public string action_label { get; set; }
            public string action_notes { get; set; }

            public string reason_id { get; set; }
            public string reason_label { get; set; }
            public string reason_notes { get; set; }
        }

        [HttpPost("AddReasons")]
        public async Task<IActionResult> AddReasons([FromBody] POMESReasonsData data)
        {
            if (data == null)
                return BadRequest("No data provided.");

            // Try to find an existing record with the same PO
            var entity = await _context.POMESReasons
                .FirstOrDefaultAsync(x => x.PO == data.PO);

           
                entity = new POMESReasons
                {
                    PO = data.PO,
                    Advance_Reasons = JsonConvert.SerializeObject(data.Advance_ReasonsData),
                    Linestop_Reasons = JsonConvert.SerializeObject(data.Linestop_ReasonsData),
                    CreatedBy = "System",
                    CreatedDate = DateTime.UtcNow,
                    //ActualDateTime = data.ForDateTimeData
                };

                _context.POMESReasons.Add(entity);

            await _context.SaveChangesAsync();

            return Ok(new { message = "Saved successfully." });
        }


        public class CombinedProductionView
        {
            public string PO { get; set; }
            public string Section { get; set; }
            public string LineName { get; set; }
            public string Material { get; set; }
            public string AdvanceReasons { get; set; } // We'll keep these as strings for the union
            public string LinestopReasons { get; set; }
            public DateTime? StartTime { get; set; }
            public DateTime? EndTime { get; set; }
            public string CreatedBy { get; set; }
            public DateTime? CreatedDate { get; set; }
            public string Source { get; set; } // Helpful to know where the row came from
        }

        public class ReasonDto
        {
            [JsonPropertyName("action_id")]
            public JsonElement ActionId { get; set; }

            [JsonPropertyName("reason_id")]
            public JsonElement ReasonId { get; set; }

            [JsonPropertyName("action_label")]
            public string ActionLabel { get; set; }

            [JsonPropertyName("action_notes")]
            public string ActionNotes { get; set; }

            [JsonPropertyName("reason_label")]
            public string ReasonLabel { get; set; }

            [JsonPropertyName("reason_notes")]
            public string ReasonNotes { get; set; }
        }

        public class FinalProductionView
        {
            public string PO { get; set; }
            public string Section { get; set; }
            public string LineName { get; set; }
            public string Material { get; set; }
            public List<ReasonDto> AdvanceReasons { get; set; } // Look! A real list!
            public List<ReasonDto> LinestopReasons { get; set; } // Look! A real list!
            public DateTime? StartTime { get; set; }
            public DateTime? EndTime { get; set; }
            public string CreatedBy { get; set; }
            public DateTime? CreatedDate { get; set; }
            public string Source { get; set; }
        }

        [HttpGet("LeveledData")]
        public async Task<ActionResult<IEnumerable<CombinedProductionView>>> GetLeveledData()
        {
            // 1. Get data from Production Records
            var prodQuery = _context.ProductionRecords.Select(p => new CombinedProductionView
            {
                PO = p.Po,
                Section = p.Section,
                LineName = p.LineName,
                Material = p.Material,
                // IF NULL in DB, use empty JSON array string to prevent InvalidCastException
                AdvanceReasons = p.AdvanceReasons ?? "[]",
                LinestopReasons = p.LinestopReasons ?? "[]",
                StartTime = p.StartTime,
                EndTime = p.EndTime,
                CreatedBy = p.Creator,
                CreatedDate = p.TimeCreated,
                Source = "ProductionRecord"
            });

            // 2. Get data from POMESReasons
            var reasonsQuery = _context.POMESReasons.Select(r => new CombinedProductionView
            {
                PO = r.PO,
                Section = "Printer 1",
                LineName = "N/A",
                Material = "N/A",
                // Notice the column name mapping here matches your model
                AdvanceReasons = r.Advance_Reasons ?? "[]",
                LinestopReasons = r.Linestop_Reasons ?? "[]",
                StartTime = null,
                EndTime = null,
                CreatedBy = r.CreatedBy,
                CreatedDate = r.CreatedDate,
                Source = "POMESReason"
            });

            var rawResult = await prodQuery.Concat(reasonsQuery).ToListAsync();

            var cleanResult = rawResult.Select(r => new FinalProductionView
            {
                PO = r.PO,
                Section = r.Section,
                LineName = r.LineName,
                Material = r.Material,
                StartTime = r.StartTime,
                EndTime = r.EndTime,
                CreatedBy = r.CreatedBy,
                CreatedDate = r.CreatedDate,
                Source = r.Source,

                // Deserialize safely!
                AdvanceReasons = string.IsNullOrWhiteSpace(r.AdvanceReasons) || r.AdvanceReasons == "[]"
            ? new List<ReasonDto>()
            : System.Text.Json.JsonSerializer.Deserialize<List<ReasonDto>>(r.AdvanceReasons),

                LinestopReasons = string.IsNullOrWhiteSpace(r.LinestopReasons) || r.LinestopReasons == "[]"
            ? new List<ReasonDto>()
            : System.Text.Json.JsonSerializer.Deserialize<List<ReasonDto>>(r.LinestopReasons)
            }).ToList();

            return Ok(cleanResult);
        }
    }
}
