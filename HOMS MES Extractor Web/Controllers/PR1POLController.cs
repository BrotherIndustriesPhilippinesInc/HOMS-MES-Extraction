using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Core;
using HOMS_MES_Extractor_Web.Data;
using HOMS_MES_Extractor_Web.DTO;

namespace HOMS_MES_Extractor_Web.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PR1POLController : ControllerBase
    {
        private readonly HOMS_MES_Extractor_WebContext _context;

        public PR1POLController(HOMS_MES_Extractor_WebContext context)
        {
            _context = context;
        }

        // GET: api/PR1POL
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PR1POL>>> GetPR1POL()
        {
            return await _context.PR1POL.ToListAsync();
        }

        // GET: api/PR1POL/5
        [HttpGet("{id}")]
        public async Task<ActionResult<PR1POL>> GetPR1POL(int id)
        {
            var pR1POL = await _context.PR1POL.FindAsync(id);

            if (pR1POL == null)
            {
                return NotFound();
            }

            return pR1POL;
        }

        // PUT: api/PR1POL/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutPR1POL(int id, PR1POL pR1POL)
        {
            if (id != pR1POL.Id)
            {
                return BadRequest();
            }

            _context.Entry(pR1POL).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PR1POLExists(id))
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

        // POST: api/PR1POL
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<PR1POL>> PostPR1POL(PR1POL pR1POL)
        {
            _context.PR1POL.Add(pR1POL);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetPR1POL", new { id = pR1POL.Id }, pR1POL);
        }

        // DELETE: api/PR1POL/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePR1POL(int id)
        {
            var pR1POL = await _context.PR1POL.FindAsync(id);
            if (pR1POL == null)
            {
                return NotFound();
            }

            _context.PR1POL.Remove(pR1POL);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool PR1POLExists(int id)
        {
            return _context.PR1POL.Any(e => e.Id == id);
        }

        [HttpGet("with-pr1pol")]
        public async Task<ActionResult<IEnumerable<PoRecordPolDto>>> GetPoRecordsWithPR1POL(
            [FromQuery] string type = "",
            [FromQuery] string prodLine = "",
            [FromQuery] string po = "",
            [FromQuery] string dateFrom = "",
            [FromQuery] string dateTo = "")
        {
            var query = from record in _context.PoRecord
                        join pol in _context.PR1POL
                            on record.PO equals pol.PrdOrderNo
                        select new PoRecordPolDto
                        {
                            Id = record.Id,
                            PO = record.PO,
                            ProdLine = record.ProdLine,
                            Qty = pol.Qty,
                            Summary = record.Summary,
                            Type = record.Type,
                            CreatedDate = record.CreatedDate,
                            CreatedDateStr = record.CreatedDate
                                .ToLocalTime()
                                .ToString("yyyy-MM-dd hh:mm tt") // or "MMM dd, yyyy HH:mm"
                        };

            // Apply dynamic filters
            if (!string.IsNullOrEmpty(type))
                query = query.Where(x => x.Type == type);

            if (!string.IsNullOrEmpty(prodLine))
                query = query.Where(x => x.ProdLine == prodLine);

            if (!string.IsNullOrEmpty(po))
                query = query.Where(x => x.PO == po);

            // Date filtering (handles both same-day and range)
            if (DateTime.TryParse(dateFrom, out var fromDate))
            {
                // Convert to UTC kind for Npgsql
                fromDate = DateTime.SpecifyKind(fromDate.Date, DateTimeKind.Utc);

                if (DateTime.TryParse(dateTo, out var toDate))
                {
                    toDate = DateTime.SpecifyKind(toDate.Date, DateTimeKind.Utc);

                    if (fromDate.Date == toDate.Date)
                    {
                        // Same date -> only that day
                        query = query.Where(x => x.CreatedDate >= fromDate &&
                                                 x.CreatedDate < fromDate.AddDays(1));
                    }
                    else
                    {
                        // Range
                        query = query.Where(x => x.CreatedDate >= fromDate &&
                                                 x.CreatedDate < toDate.AddDays(1));
                    }
                }
                else
                {
                    // Only from-date given
                    query = query.Where(x => x.CreatedDate >= fromDate);
                }
            }

            var result = await query.OrderByDescending(x => x.CreatedDate).ToListAsync();
            return Ok(result);
        }



    }
}
