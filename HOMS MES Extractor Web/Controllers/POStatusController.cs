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
using Newtonsoft.Json.Linq;
using System.Diagnostics;

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

        [HttpPost("CheckActivityEventStream")]
        public async Task<ActionResult> PostPoStatusCheckActivityEventStream([FromBody] List<POStatus> poStatusList)
        {
            foreach (var item in poStatusList)
            {
                await _context.POStatus.AddAsync(item);
            }
            await _context.SaveChangesAsync();
            return Ok();
        }

        class TaktTimeModel
        {
            [JsonProperty("model_code")]
            public string ModelCode { get; set; }

            [JsonProperty("takt_time")]
            public int TaktTime { get; set; }

            [JsonProperty("section")]
            public string Section { get; set; }
        }

        public class POStatusTaktTimeModel
        {
            public POStatus poStatus { get; set; }

            public Target Target { get; set; } = new Target();

            // Delayed or OnTime
            public string Status { get; set; }

            public Dictionary<string, int> HourlyOutput { get; set; } = new();
        }

        public class Target
        {
            public Dictionary<string, int> hourly { get; set; } = new();
            public Dictionary<string, int> cumulative { get; set; } = new();
        }

        [HttpGet("DelayStatus")]
        public async Task<ActionResult<List<POStatusTaktTimeModel>>> GetDelayStatus()
        {
            var results = new List<POStatusTaktTimeModel>();
            var today = DateTime.UtcNow.Date;

            var poList = await _context.POStatus
                .Where(x => x.StartDateTime.Date == today)
                .Select(x => x.PO)
                .Distinct()
                .ToListAsync();

            // Fetch takt time data
            var taktTimeList = new List<TaktTimeModel>();
            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.GetAsync("http://apbiphbpswb02/homs/api/admin/getTaktTimeV2.php");
                response.EnsureSuccessStatusCode();

                var jsonString = await response.Content.ReadAsStringAsync();
                var jsonObject = JsonConvert.DeserializeObject<JObject>(jsonString);
                var data = jsonObject["data"]?.ToObject<List<TaktTimeModel>>();
                taktTimeList = data ?? new List<TaktTimeModel>();
            }


            foreach (var po in poList)
            {
                var poDetails = await _context.POStatus
                    .Where(x => x.PO == po)
                    .OrderBy(x => x.Id)
                    .ToListAsync();

                if (!poDetails.Any())
                    continue;

                // Detect start/resume times
                var startTimes = new List<DateTime>();
                int lastQty = 0;
                DateTime? lastTime = null;
                TimeSpan breakThreshold = TimeSpan.FromMinutes(30);

                foreach (var item in poDetails)
                {
                    bool newSession = false;
                    var localTime = item.StartDateTime.ToLocalTime();

                    if (item.ProducedQty > lastQty &&
                        lastTime != null &&
                        (localTime - lastTime.Value) > breakThreshold)
                        newSession = true;

                    if (lastQty == 0 && item.ProducedQty > 0)
                        newSession = true;

                    if (newSession)
                        startTimes.Add(localTime);

                    lastQty = item.ProducedQty;
                    lastTime = localTime;
                }

                var first = poDetails.First();
                var last = poDetails.Last();
                var modelCode = last.ModelCode;
                var plannedQty = last.PlannedQty;
                var taktInfo = taktTimeList.FirstOrDefault(x => x.ModelCode == modelCode);

                if (taktInfo == null || taktInfo.TaktTime <= 0)
                {
                    results.Add(new POStatusTaktTimeModel
                    {
                        poStatus = last,
                        Target = new Target
                        {
                            hourly = new Dictionary<string, int> { { "Message", 0 } },
                            cumulative = new Dictionary<string, int> { { "Message", 0 } }
                        },
                        HourlyOutput = new Dictionary<string, int> { { "Message", 0 } },
                        Status = "Unknown"
                    });
                    continue;
                }

                // Compute hourly output for snapshots
                // Compute hourly output for snapshots
                var hourlyOutput = poDetails
                .GroupBy(x => x.StartDateTime.ToLocalTime().ToString("hh tt"))
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(x => x.ProducedQty)
                );


                // Get hourly and cumulative targets and return proper JSON object instead of string
                var targetData = GetHourlyAndCumulativeTargets(startTimes, first, taktInfo.TaktTime);

                results.Add(new POStatusTaktTimeModel
                {
                    poStatus = last,
                    Target = targetData,
                    HourlyOutput = hourlyOutput,
                    Status = "OnTime" // Optional: you can calculate delay by comparing HourlyOutput vs. Target.hourly
                });

                // Replace the string field with a dynamic object
                //var lastResult = results.Last();
                //lastResult.GetType().GetProperty("Target").SetValue(lastResult, targetData);
            }

            return Ok(results);
        }

        private Target GetHourlyAndCumulativeTargets(List<DateTime> startTimes, POStatus po, double taktTime)
        {
            var result = new Target
            {
                hourly = new Dictionary<string, int>(),
                cumulative = new Dictionary<string, int>()
            };

            if (startTimes == null || startTimes.Count == 0)
                startTimes = new List<DateTime> { po.StartDateTime.ToLocalTime() };

            double unitsPerHour = 3600 / taktTime;
            double remaining = po.PlannedQty;
            int cumulative = 0;

            for (int i = 0; i < startTimes.Count; i++)
            {
                var start = startTimes[i];
                var end = (i + 1 < startTimes.Count) ? startTimes[i + 1] : DateTime.MaxValue;

                var current = new DateTime(start.Year, start.Month, start.Day, start.Hour, 0, 0);

                while (remaining > 0 && current < end)
                {
                    string label = current.ToString("hh tt");
                    int hourlyTarget = (int)Math.Min(unitsPerHour, remaining);

                    // Add target for this hour
                    if (result.hourly.ContainsKey(label))
                        result.hourly[label] += hourlyTarget;
                    else
                        result.hourly[label] = hourlyTarget;

                    cumulative += hourlyTarget;
                    result.cumulative[label] = cumulative;

                    remaining -= hourlyTarget;
                    current = current.AddHours(1);
                }

                if (remaining <= 0)
                    break;
            }

            return result;
        }


    }
}
