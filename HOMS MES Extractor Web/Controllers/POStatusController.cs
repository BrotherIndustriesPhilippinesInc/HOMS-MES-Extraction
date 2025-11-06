using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Core;
using HOMS_MES_Extractor_Web.Data;

namespace HOMS_MES_Extractor_Web.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class POStatusController : ControllerBase
    {
        private readonly HOMS_MES_Extractor_WebContext _context;

        public POStatusController(HOMS_MES_Extractor_WebContext context)
        {
            _context = context;
        }

        // GET: api/POStatus
        [HttpGet]
        public async Task<ActionResult<IEnumerable<POStatus>>> GetPOStatus()
        {
            return await _context.POStatus.ToListAsync();
        }

        // GET: api/POStatus/5
        [HttpGet("{id}")]
        public async Task<ActionResult<POStatus>> GetPOStatus(int id)
        {
            var pOStatus = await _context.POStatus.FindAsync(id);

            if (pOStatus == null)
            {
                return NotFound();
            }

            return pOStatus;
        }

        // PUT: api/POStatus/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutPOStatus(int id, POStatus pOStatus)
        {
            if (id != pOStatus.Id)
            {
                return BadRequest();
            }

            _context.Entry(pOStatus).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!POStatusExists(id))
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

        // POST: api/POStatus
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<POStatus>> PostPOStatus(POStatus pOStatus)
        {
            _context.POStatus.Add(pOStatus);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetPOStatus", new { id = pOStatus.Id }, pOStatus);
        }

        // DELETE: api/POStatus/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePOStatus(int id)
        {
            var pOStatus = await _context.POStatus.FindAsync(id);
            if (pOStatus == null)
            {
                return NotFound();
            }

            _context.POStatus.Remove(pOStatus);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool POStatusExists(int id)
        {
            return _context.POStatus.Any(e => e.Id == id);
        }

        [HttpPost("CheckActivity")]
        public async Task<ActionResult> PostPoStatusCheckActivity([FromBody] List<POStatus> poStatusList)
        {
            if (poStatusList == null || !poStatusList.Any())
                return BadRequest("No records provided.");

            var now = DateTime.UtcNow;
            var poNumbers = poStatusList.Select(p => p.PO).ToList();

            // Fetch existing PO entries from DB
            var existingPos = await _context.POStatus
                .Where(p => poNumbers.Contains(p.PO))
                .ToListAsync();

            var newRecords = new List<POStatus>();

            foreach (var po in poStatusList)
            {
                var existing = existingPos
                    .Where(x => x.PO == po.PO)
                    .OrderByDescending(x => x.StartDateTime)
                    .FirstOrDefault();

                bool notInDb = existing == null;
                bool qtyChanged = !notInDb && po.ProducedQty != existing.ProducedQty;
                bool over30Min = notInDb || (now - existing.StartDateTime).TotalMinutes > 30;

                // Insert if:
                // 1. PO not in DB (new PO), OR
                // 2. ProducedQty changed AND > 30 minutes since last record
                if (notInDb || (qtyChanged && over30Min))
                {
                    po.StartDateTime = now; // record current start time
                    newRecords.Add(po);
                }
            }

            if (newRecords.Any())
            {
                await _context.POStatus.AddRangeAsync(newRecords);
                await _context.SaveChangesAsync();
            }

            return Ok(new
            {
                InsertedCount = newRecords.Count,
                Message = $"{newRecords.Count} record(s) inserted successfully."
            });
        }

    }
}
