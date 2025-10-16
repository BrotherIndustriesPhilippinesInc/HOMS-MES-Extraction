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
    public class PoRecordsController : ControllerBase
    {
        private readonly HOMS_MES_Extractor_WebContext _context;

        public PoRecordsController(HOMS_MES_Extractor_WebContext context)
        {
            _context = context;
        }

        // GET: api/PoRecords
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PoRecord>>> GetPoRecord()
        {
            return await _context.PoRecord.ToListAsync();
        }

        // GET: api/PoRecords/5
        [HttpGet("{id}")]
        public async Task<ActionResult<PoRecord>> GetPoRecord(int id)
        {
            var poRecord = await _context.PoRecord.FindAsync(id);

            if (poRecord == null)
            {
                return NotFound();
            }

            return poRecord;
        }

        // PUT: api/PoRecords/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutPoRecord(int id, PoRecord poRecord)
        {
            if (id != poRecord.Id)
            {
                return BadRequest();
            }

            _context.Entry(poRecord).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PoRecordExists(id))
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

        // POST: api/PoRecords
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<PoRecord>> PostPoRecord(PoRecord poRecord)
        {
            _context.PoRecord.Add(poRecord);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetPoRecord", new { id = poRecord.Id }, poRecord);
        }

        // DELETE: api/PoRecords/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePoRecord(int id)
        {
            var poRecord = await _context.PoRecord.FindAsync(id);
            if (poRecord == null)
            {
                return NotFound();
            }

            _context.PoRecord.Remove(poRecord);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool PoRecordExists(int id)
        {
            return _context.PoRecord.Any(e => e.Id == id);
        }

        // POST: api/PoRecords/bulk
        [HttpPost("bulk")]
        public async Task<ActionResult> PostPoRecordsBulk([FromBody] List<PoRecord> poRecords)
        {
            if (poRecords == null || !poRecords.Any())
                return BadRequest("No records provided.");

            // Set CreatedDate and UpdatedDate
            DateTime now = DateTime.UtcNow;
            foreach (var record in poRecords)
            {
                record.CreatedDate = now;
            }

            // Bulk insert
            await _context.PoRecord.AddRangeAsync(poRecords);
            await _context.SaveChangesAsync();

            return Ok(new { Count = poRecords.Count, Message = "Bulk insert successful." });
        }

    }
}
