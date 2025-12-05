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
using System.Globalization;
using System.Net.Mail;
using System.Net;
using System.Net.Mime;
using System.Text;

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
                item.ComplianceRate = (decimal)item.ProducedQty / item.PlannedQty * 100;
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

            public HourlyOutputModel Output { get; set; } = new();

            public int TaktTime { get; set; }
        }

        public class Target
        {
            public Dictionary<string, int> hourly { get; set; } = new();
            public Dictionary<string, int> cumulative { get; set; } = new();
        }

        public class HourlyOutputModel
        {
            public Dictionary<string, int> HourlyOutput { get; set; } = new();
            public Dictionary<string, int> HourlyOutputCommulative { get; set; } = new();
        }

        [HttpGet("DelayStatus")]
        public async Task<ActionResult<List<POStatusTaktTimeModel>>> GetDelayStatus([FromQuery] string PO = "")
        {
            var results = new List<POStatusTaktTimeModel>();
            var today = DateTime.UtcNow.Date;

            var poList = await _context.POStatus
                .Where(x => x.StartDateTime.Date == today && (string.IsNullOrEmpty(PO) || x.PO == PO))
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
                    .Where(x => x.PO == po && x.ProducedQty != 0)
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
                        Output = new HourlyOutputModel
                        {
                            HourlyOutput = new Dictionary<string, int> { { "Message", 0 } },
                            HourlyOutputCommulative = new Dictionary<string, int> { { "Message", 0 } }
                        },
                        Status = "Unknown",
                        TaktTime = 0
                    });
                    continue;
                }

                // Compute hourly output for snapshots
                var hourlyOutput = new Dictionary<string, int>();
                var cumulativeOutput = new Dictionary<string, int>();
                int previous = 0;

                var grouped = poDetails
                    .GroupBy(x => x.StartDateTime.ToLocalTime().ToString("yyyy-MM-dd HH"))
                    .OrderBy(g => g.Key);

                foreach (var g in grouped)
                {
                    var sampleTime = DateTime.ParseExact(g.Key, "yyyy-MM-dd HH", null);
                    string label = sampleTime.ToString("yyyy-MM-dd HH tt");

                    int cumulative = g.OrderBy(x => x.StartDateTime).Last().ProducedQty;
                    int actual = cumulative - previous;

                    // STOP output generation once production is done
                    if (previous >= plannedQty)
                        break;

                    if (cumulative > plannedQty)
                        actual = plannedQty - previous;

                    hourlyOutput[label] = actual;
                    previous = cumulative;

                    cumulativeOutput[label] = cumulative;

                }


                // Get hourly and cumulative targets and return proper JSON object instead of string
                var targetData = GetHourlyAndCumulativeTargets(startTimes, first, taktInfo.TaktTime);


                var latestActual = cumulativeOutput.Values.LastOrDefault();

                var matchingTargetKey = cumulativeOutput.Keys.Last();
                var latestTarget = targetData.cumulative
                    .Where(x => x.Key == matchingTargetKey)
                    .Select(x => x.Value)
                    .FirstOrDefault();

                results.Add(new POStatusTaktTimeModel
                {
                    poStatus = last,
                    Target = targetData,
                    Output = new HourlyOutputModel
                    {
                        HourlyOutput = hourlyOutput,
                        HourlyOutputCommulative = cumulativeOutput
                    },
                    Status = latestActual < latestTarget ? "Behind" : "Ahead",
                    TaktTime = taktInfo.TaktTime
                    // Optional: you can calculate delay by comparing HourlyOutput vs. Target.hourly
                });
            }

            return Ok(results);
        }

        [HttpGet("DelayStatusOptimized")]
        public async Task<ActionResult<List<POStatusTaktTimeModel>>> GetDelayStatusOptimized([FromQuery] string PO = "")
        {
            var results = new List<POStatusTaktTimeModel>();
            var today = DateTime.UtcNow.Date;

            // 1️⃣ Fetch all POStatus for today once
            var allPOStatusToday = await _context.POStatus
                .Where(x => x.StartDateTime.Date == today && x.ProducedQty != 0 && (string.IsNullOrEmpty(PO) || x.PO == PO))
                .OrderBy(x => x.Id)
                .ToListAsync();

            if (!allPOStatusToday.Any())
                return Ok(results);

            // Group by PO
            var poGroups = allPOStatusToday
                .GroupBy(x => x.PO)
                .ToDictionary(g => g.Key, g => g.ToList());

            // 2️⃣ Fetch takt times once
            List<TaktTimeModel> taktTimeList;
            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.GetAsync("http://apbiphbpswb02/homs/api/admin/getTaktTimeV2.php");
                response.EnsureSuccessStatusCode();
                var jsonString = await response.Content.ReadAsStringAsync();
                var jsonObject = JsonConvert.DeserializeObject<JObject>(jsonString);
                taktTimeList = jsonObject["data"]?.ToObject<List<TaktTimeModel>>() ?? new List<TaktTimeModel>();
            }

            // 3️⃣ Process each PO
            foreach (var po in poGroups.Keys)
            {
                var poDetails = poGroups[po];
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
                        Output = new HourlyOutputModel
                        {
                            HourlyOutput = new Dictionary<string, int> { { "Message", 0 } },
                            HourlyOutputCommulative = new Dictionary<string, int> { { "Message", 0 } }
                        },
                        Status = "No Takt Time",
                        TaktTime = 0
                    });
                    continue;
                }

                // 4️⃣ Detect start/resume times
                var startTimes = new List<DateTime>();
                int lastQty = 0;
                DateTime? lastTime = null;
                TimeSpan breakThreshold = TimeSpan.FromMinutes(30);

                foreach (var item in poDetails)
                {
                    var localTime = item.StartDateTime.ToLocalTime();
                    bool newSession = (lastQty == 0 && item.ProducedQty > 0) ||
                                      (lastTime != null && item.ProducedQty > lastQty && (localTime - lastTime.Value) > breakThreshold);
                    if (newSession)
                        startTimes.Add(localTime);

                    lastQty = item.ProducedQty;
                    lastTime = localTime;
                }

                // 5️⃣ Compute hourly output
                var hourlyOutput = new Dictionary<string, int>();
                var cumulativeOutput = new Dictionary<string, int>();
                int previous = 0;

                var grouped = poDetails.GroupBy(x => x.StartDateTime.ToLocalTime().Hour);
                foreach (var g in grouped)
                {
                    var sampleTime = g.First().StartDateTime.ToLocalTime();
                    string label = sampleTime.ToString("yyyy-MM-dd hh tt");

                    int cumulative = g.Last().ProducedQty;
                    int actual = cumulative - previous;

                    if (previous >= plannedQty)
                        break;

                    if (cumulative > plannedQty)
                        actual = plannedQty - previous;

                    hourlyOutput[label] = actual;
                    cumulativeOutput[label] = cumulative;

                    previous = cumulative;
                }

                // 6️⃣ Get target data
                var targetData = GetHourlyAndCumulativeTargets(startTimes, first, taktInfo.TaktTime);

                // 7️⃣ Check latest output vs current time
                var now = DateTime.Now;
                var latestHourKey = cumulativeOutput.Keys
                    .Where(k => DateTime.ParseExact(k, "yyyy-MM-dd hh tt", null) <= now)
                    .OrderBy(k => k)
                    .LastOrDefault();

                int latestActual = latestHourKey != null ? cumulativeOutput[latestHourKey] : 0;
                int latestTarget = latestHourKey != null && targetData.cumulative.TryGetValue(latestHourKey, out var val) ? val : 0;

                string actualStatus = "";

                int targetHours = targetData.hourly.Count;
                int actualHours = hourlyOutput.Count;

                if (cumulativeOutput.LastOrDefault().Value == targetData.cumulative.LastOrDefault().Value)
                {
                    actualStatus = "Completed";
                }
                else if (latestActual < latestTarget)
                {
                    actualStatus = "Delayed";
                }
                else
                {
                    actualStatus = "Advanced";
                }

                if (actualHours > targetHours && cumulativeOutput.LastOrDefault().Value != targetData.cumulative.LastOrDefault().Value)
                {
                    actualStatus = "Delayed";
                }

                results.Add(new POStatusTaktTimeModel
                {
                    poStatus = last,
                    Target = targetData,
                    Output = new HourlyOutputModel
                    {
                        HourlyOutput = hourlyOutput,
                        HourlyOutputCommulative = cumulativeOutput
                    },
                    Status = actualStatus,
                    TaktTime = taktInfo.TaktTime
                });
            }

            return Ok(results);
        }

        [HttpGet("DelayStatusOptimized24H")]
        public async Task<ActionResult<List<POStatusTaktTimeModel>>> GetDelayStatusOptimized24H([FromQuery] string PO = "")
        {
            var results = new List<POStatusTaktTimeModel>();
            var since = DateTime.UtcNow.AddHours(-24);

            // 1️⃣ Fetch all POStatus for today once
            var allPOStatusToday = await _context.POStatus
                .Where(x => x.StartDateTime >= since && x.ProducedQty != 0
                    && (string.IsNullOrEmpty(PO) || x.PO == PO))
                .OrderBy(x => x.Id)
                .ToListAsync();

            if (!allPOStatusToday.Any())
                return Ok(results);

            // Group by PO
            var poGroups = allPOStatusToday
                .GroupBy(x => x.PO)
                .ToDictionary(g => g.Key, g => g.ToList());

            // 2️⃣ Fetch takt times once
            List<TaktTimeModel> taktTimeList;
            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.GetAsync("http://apbiphbpswb02/homs/api/admin/getTaktTimeV2.php");
                response.EnsureSuccessStatusCode();
                var jsonString = await response.Content.ReadAsStringAsync();
                var jsonObject = JsonConvert.DeserializeObject<JObject>(jsonString);
                taktTimeList = jsonObject["data"]?.ToObject<List<TaktTimeModel>>() ?? new List<TaktTimeModel>();
            }

            // 3️⃣ Process each PO
            foreach (var po in poGroups.Keys)
            {
                var poDetails = poGroups[po]
                    .OrderBy(x => x.StartDateTime)
                    .ToList();
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
                        Output = new HourlyOutputModel
                        {
                            HourlyOutput = new Dictionary<string, int> { { "Message", 0 } },
                            HourlyOutputCommulative = new Dictionary<string, int> { { "Message", 0 } }
                        },
                        Status = "No Takt Time",
                        TaktTime = 0
                    });
                    continue;
                }

                // 4️⃣ Detect start/resume times
                // 4️⃣ Detect start/resume times (FIXED)
                /*
                   Rules:
                   - First Start → first record
                   - Resume → ProducedQty increases after being flat for >= 20 minutes
                */

                var startTimes = new List<DateTime>();

                // Always add the first timestamp
                startTimes.Add(poDetails.First().StartDateTime.ToLocalTime());

                int lastQty = poDetails.First().ProducedQty;
                DateTime lastChangeTime = poDetails.First().StartDateTime.ToLocalTime();
                DateTime lastTime = lastChangeTime;

                TimeSpan idleThreshold = TimeSpan.FromMinutes(20);

                foreach (var item in poDetails.Skip(1))
                {
                    var localTime = item.StartDateTime.ToLocalTime();

                    bool qtyIncreased = item.ProducedQty > lastQty;
                    bool qtySame = item.ProducedQty == lastQty;

                    if (qtySame)
                    {
                        // Still idle, update lastTime only
                        lastTime = localTime;
                    }
                    else if (qtyIncreased)
                    {
                        // Check if idle period exceeded threshold
                        if ((localTime - lastChangeTime) >= idleThreshold)
                        {
                            startTimes.Add(localTime);  // RESUME DETECTED
                        }

                        // Update lastChangeTime because qty increased
                        lastChangeTime = localTime;
                        lastTime = localTime;
                    }

                    lastQty = item.ProducedQty;
                }


                // 5️⃣ Compute hourly output
                var hourlyOutput = new Dictionary<string, int>();
                var cumulativeOutput = new Dictionary<string, int>();
                int previous = 0;

                var grouped = poDetails.GroupBy(x => x.StartDateTime.ToLocalTime().Hour);
                foreach (var g in grouped)
                {
                    var sampleTime = g.First().StartDateTime.ToLocalTime();
                    string label = sampleTime.ToString("yyyy-MM-dd hh tt");

                    int cumulative = g.Last().ProducedQty;
                    int actual = cumulative - previous;

                    if (previous >= plannedQty)
                        break;

                    if (cumulative > plannedQty)
                        actual = plannedQty - previous;

                    hourlyOutput[label] = actual;
                    cumulativeOutput[label] = cumulative;

                    previous = cumulative;
                }

                // 6️⃣ Get target data
                var targetData = GetHourlyAndCumulativeTargets(startTimes, first, taktInfo.TaktTime);

                // 7️⃣ Check latest output vs current time
                var now = DateTime.Now;
                var latestHourKey = cumulativeOutput.Keys
                    .Where(k => DateTime.ParseExact(k, "yyyy-MM-dd hh tt", null) <= now)
                    .OrderBy(k => k)
                    .LastOrDefault();

                int latestActual = latestHourKey != null ? cumulativeOutput[latestHourKey] : 0;
                int latestTarget = latestHourKey != null && targetData.cumulative.TryGetValue(latestHourKey, out var val) ? val : 0;

                string actualStatus = "";

                int targetHours = targetData.hourly.Count;
                int actualHours = hourlyOutput.Count;

                if (cumulativeOutput.LastOrDefault().Value == targetData.cumulative.LastOrDefault().Value)
                {
                    actualStatus = "Completed";
                }
                else if (latestActual < latestTarget)
                {
                    actualStatus = "Delayed";
                }
                else
                {
                    actualStatus = "Advance";
                }

                if (actualHours > targetHours && cumulativeOutput.LastOrDefault().Value != targetData.cumulative.LastOrDefault().Value)
                {
                    actualStatus = "Delayed";
                }

                results.Add(new POStatusTaktTimeModel
                {
                    poStatus = last,
                    Target = targetData,
                    Output = new HourlyOutputModel
                    {
                        HourlyOutput = hourlyOutput,
                        HourlyOutputCommulative = cumulativeOutput
                    },
                    Status = actualStatus,
                    TaktTime = taktInfo.TaktTime
                });
            }

            //Remove completed status
            results = results.Where(x => x.Status != "Completed").ToList();
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

            // Go through each session
            foreach (var start in startTimes.OrderBy(x => x))
            {
                // Session starts at the hour of the start time
                var current = new DateTime(start.Year, start.Month, start.Day, start.Hour, 0, 0);

                // We'll assume each session continues producing until planned quantity is done
                while (remaining > 0)
                {
                    string label = current.ToString("yyyy-MM-dd hh tt");
                    int hourlyTarget = (int)Math.Min(unitsPerHour, remaining);

                    // Merge hourly targets if the same hour appears in multiple sessions
                    if (result.hourly.ContainsKey(label))
                        result.hourly[label] += hourlyTarget;
                    else
                        result.hourly[label] = hourlyTarget;

                    cumulative += hourlyTarget;
                    result.cumulative[label] = cumulative;

                    remaining -= hourlyTarget;
                    current = current.AddHours(1);

                    // Stop if remaining is 0
                    if (remaining <= 0)
                        break;
                }
            }

            return result;
        }

        public class Lines
        {
            public int Id { get; set; }
            public string LineName { get; set; }
        }

        public class LinesPO
        {
            public string LineName { get; set; }

            public string PO { get; set; }
        }

        [HttpGet("DelayStatusFullTimeline")]
        public async Task<ActionResult<List<POStatusTaktTimeModel>>> GetDelayStatusFullTimeline([FromQuery] string PO = "")
        {
            var results = new List<POStatusTaktTimeModel>();
            var since24h = DateTime.UtcNow.AddHours(-24);

            // 1️⃣ Identify POs active in the last 24h
            var activePOs = await _context.POStatus
                .Where(x => x.StartDateTime >= since24h && x.ProducedQty != 0
                    && (string.IsNullOrEmpty(PO) || x.PO == PO))
                .Select(x => x.PO)
                .Distinct()
                .ToListAsync();

            if (!activePOs.Any())
                return Ok(results);

            // 2️⃣ Fetch full timeline for these POs
            var allPOStatus = await _context.POStatus
                .Where(x => activePOs.Contains(x.PO))
                .OrderBy(x => x.Id)
                .ToListAsync();

            // Group by PO
            var poGroups = allPOStatus
                .GroupBy(x => x.PO)
                .ToDictionary(g => g.Key, g => g.ToList());

            // 3️⃣ Fetch takt times once
            List<TaktTimeModel> taktTimeList;
            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.GetAsync("http://apbiphbpswb02/homs/api/admin/getTaktTimeV2.php");
                response.EnsureSuccessStatusCode();
                var jsonString = await response.Content.ReadAsStringAsync();
                var jsonObject = JsonConvert.DeserializeObject<JObject>(jsonString);
                taktTimeList = jsonObject["data"]?.ToObject<List<TaktTimeModel>>() ?? new List<TaktTimeModel>();
            }

            // 4️⃣ Process each PO
            foreach (var po in poGroups.Keys)
            {
                var poDetails = poGroups[po].OrderBy(x => x.StartDateTime).ToList();
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
                        Output = new HourlyOutputModel
                        {
                            HourlyOutput = new Dictionary<string, int> { { "Message", 0 } },
                            HourlyOutputCommulative = new Dictionary<string, int> { { "Message", 0 } }
                        },
                        Status = "No Takt Time",
                        TaktTime = 0
                    });
                    continue;
                }

                // 5️⃣ Find the first non-zero production point
                var firstNonZero = poDetails.FirstOrDefault(x => x.ProducedQty > 0);
                if (firstNonZero == null) continue;

                var firstNonZeroLocal = firstNonZero.StartDateTime.ToLocalTime();
                var startTimes = new List<DateTime> { firstNonZeroLocal };

                // Detect resume points
                int lastQty = firstNonZero.ProducedQty;
                DateTime lastChangeTime = firstNonZeroLocal;
                TimeSpan idleThreshold = TimeSpan.FromMinutes(20);

                foreach (var item in poDetails.SkipWhile(x => x.Id <= firstNonZero.Id))
                {
                    var localTime = item.StartDateTime.ToLocalTime();
                    if (item.ProducedQty > lastQty)
                    {
                        if ((localTime - lastChangeTime) >= idleThreshold)
                            startTimes.Add(localTime);
                        lastChangeTime = localTime;
                    }
                    lastQty = item.ProducedQty;
                }

                // 6️⃣ Compute hourly output
                var hourlyOutput = new Dictionary<string, int>();
                var cumulativeOutput = new Dictionary<string, int>();
                int previous = 0; // count from zero

                var grouped = poDetails
                    .Where(x => x.StartDateTime >= firstNonZero.StartDateTime)
                    .GroupBy(x => new DateTime(x.StartDateTime.ToLocalTime().Year, x.StartDateTime.ToLocalTime().Month,
                                               x.StartDateTime.ToLocalTime().Day, x.StartDateTime.ToLocalTime().Hour, 0, 0));

                foreach (var g in grouped)
                {
                    string label = g.Key.ToString("yyyy-MM-dd hh tt");
                    int cumulative = g.Last().ProducedQty;
                    int actual = cumulative - previous;
                    if (actual > 0)
                    {
                        hourlyOutput[label] = actual;
                        cumulativeOutput[label] = cumulative;
                    }
                    previous = cumulative;
                }

                // 7️⃣ Compute target starting at first non-zero production
                var targetData = GetHourlyAndCumulativeTargets(startTimes, firstNonZero, taktInfo.TaktTime);

                // 8️⃣ Determine status based on time
                string actualStatus = "Delayed"; // default

                if (cumulativeOutput.Any())
                {
                    // latest actual snapshot
                    var latestActual = cumulativeOutput.Values.Last();

                    // latest target **up to that time**
                    var targetAtLatestTime = targetData.cumulative
                        .Where(t => DateTime.Parse(t.Key) <= DateTime.Parse(cumulativeOutput.Keys.Last()))
                        .OrderBy(t => DateTime.Parse(t.Key))
                        .Select(t => t.Value)
                        .LastOrDefault();

                    if (latestActual >= plannedQty) // everything done
                        actualStatus = "Completed";
                    else if (latestActual < targetAtLatestTime)
                        actualStatus = "Delayed";
                    else
                        actualStatus = "Advance";
                }
                else
                {
                    // if no hourly output yet, but ProducedQty already equals PlannedQty
                    if (last.ProducedQty >= plannedQty)
                        actualStatus = "Completed";
                }



                results.Add(new POStatusTaktTimeModel
                {
                    poStatus = last,
                    Target = targetData,
                    Output = new HourlyOutputModel
                    {
                        HourlyOutput = hourlyOutput,
                        HourlyOutputCommulative = cumulativeOutput
                    },
                    Status = actualStatus,
                    TaktTime = taktInfo.TaktTime
                });
            }

            results = results.Where(x => x.Status != "Completed").ToList();
            return Ok(results);
        }

        private async Task<ActionResult<List<POStatusTaktTimeModel>>> GetDelayStatusFullTimelineDelayedOnly([FromQuery] string PO = "")
        {
            var results = new List<POStatusTaktTimeModel>();
            var since24h = DateTime.UtcNow.AddHours(-24);

            // 1️⃣ Identify POs active in the last 24h
            var activePOs = await _context.POStatus
                .Where(x => 
                x.StartDateTime >= since24h && 
                x.ProducedQty != 0 && 
                (string.IsNullOrEmpty(PO) || x.PO == PO))

                .Select(x => x.PO)
                .Distinct()
                .ToListAsync();

            if (!activePOs.Any())
                return Ok(results);

            // 2️⃣ Fetch full timeline for these POs
            var allPOStatus = await _context.POStatus
                .Where(x => activePOs.Contains(x.PO))
                .OrderBy(x => x.Id)
                .ToListAsync();

            // Group by PO
            var poGroups = allPOStatus
                .GroupBy(x => x.PO)
                .ToDictionary(g => g.Key, g => g.ToList());

            // 3️⃣ Fetch takt times once
            List<TaktTimeModel> taktTimeList;
            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.GetAsync("http://apbiphbpswb02/homs/api/admin/getTaktTimeV2.php");
                response.EnsureSuccessStatusCode();
                var jsonString = await response.Content.ReadAsStringAsync();
                var jsonObject = JsonConvert.DeserializeObject<JObject>(jsonString);
                taktTimeList = jsonObject["data"]?.ToObject<List<TaktTimeModel>>() ?? new List<TaktTimeModel>();
            }

            // 4️⃣ Process each PO
            foreach (var po in poGroups.Keys)
            {
                var poDetails = poGroups[po].OrderBy(x => x.StartDateTime).ToList();
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
                        Output = new HourlyOutputModel
                        {
                            HourlyOutput = new Dictionary<string, int> { { "Message", 0 } },
                            HourlyOutputCommulative = new Dictionary<string, int> { { "Message", 0 } }
                        },
                        Status = "No Takt Time",
                        TaktTime = 0
                    });
                    continue;
                }

                // 5️⃣ Find the first non-zero production point
                var firstNonZero = poDetails.FirstOrDefault(x => x.ProducedQty > 0);
                if (firstNonZero == null) continue;

                var firstNonZeroLocal = firstNonZero.StartDateTime.ToLocalTime();
                var startTimes = new List<DateTime> { firstNonZeroLocal };

                // Detect resume points
                int lastQty = firstNonZero.ProducedQty;
                DateTime lastChangeTime = firstNonZeroLocal;
                TimeSpan idleThreshold = TimeSpan.FromMinutes(20);

                foreach (var item in poDetails.SkipWhile(x => x.Id <= firstNonZero.Id))
                {
                    var localTime = item.StartDateTime.ToLocalTime();
                    if (item.ProducedQty > lastQty)
                    {
                        if ((localTime - lastChangeTime) >= idleThreshold)
                            startTimes.Add(localTime);
                        lastChangeTime = localTime;
                    }
                    lastQty = item.ProducedQty;
                }

                // 6️⃣ Compute hourly output
                var hourlyOutput = new Dictionary<string, int>();
                var cumulativeOutput = new Dictionary<string, int>();
                int previous = 0; // count from zero

                var grouped = poDetails
                    .Where(x => x.StartDateTime >= firstNonZero.StartDateTime)
                    .GroupBy(x => new DateTime(x.StartDateTime.ToLocalTime().Year, x.StartDateTime.ToLocalTime().Month,
                                               x.StartDateTime.ToLocalTime().Day, x.StartDateTime.ToLocalTime().Hour, 0, 0));

                foreach (var g in grouped)
                {
                    string label = g.Key.ToString("yyyy-MM-dd hh tt");
                    int cumulative = g.Last().ProducedQty;
                    int actual = cumulative - previous;
                    if (actual > 0)
                    {
                        hourlyOutput[label] = actual;
                        cumulativeOutput[label] = cumulative;
                    }
                    previous = cumulative;
                }

                // 7️⃣ Compute target starting at first non-zero production
                var targetData = GetHourlyAndCumulativeTargets(startTimes, firstNonZero, taktInfo.TaktTime);

                // 8️⃣ Determine status based on time
                string actualStatus = "Delayed"; // default

                if (cumulativeOutput.Any())
                {
                    // latest actual snapshot
                    var latestActual = cumulativeOutput.Values.Last();

                    // latest target **up to that time**
                    var targetAtLatestTime = targetData.cumulative
                        .Where(t => DateTime.Parse(t.Key) <= DateTime.Parse(cumulativeOutput.Keys.Last()))
                        .OrderBy(t => DateTime.Parse(t.Key))
                        .Select(t => t.Value)
                        .LastOrDefault();

                    if (latestActual >= plannedQty) // everything done
                        actualStatus = "Completed";
                    else if (latestActual < targetAtLatestTime)
                        actualStatus = "Delayed";
                    else
                        actualStatus = "Advance";
                }
                else
                {
                    // if no hourly output yet, but ProducedQty already equals PlannedQty
                    if (last.ProducedQty >= plannedQty)
                        actualStatus = "Completed";
                }



                results.Add(new POStatusTaktTimeModel
                {
                    poStatus = last,
                    Target = targetData,
                    Output = new HourlyOutputModel
                    {
                        HourlyOutput = hourlyOutput,
                        HourlyOutputCommulative = cumulativeOutput
                    },
                    Status = actualStatus,
                    TaktTime = taktInfo.TaktTime
                });
            }

            results = results.Where(x => x.Status != "Completed" && x.Status != "No Takt Time").ToList();
            return Ok(results);
        }

        [HttpGet("SendDelayEmail")]
        public async Task<IActionResult> SendDelayEmail()
        {
            var pr1Emails = await _context.Email
                .Where(x => x.Section == "Printer 1")
                .ToListAsync();

            var delaysResult = await GetDelayStatusFullTimelineDelayedOnly();

            List<POStatusController.POStatusTaktTimeModel> delays;

            if (delaysResult.Result is OkObjectResult okResult && okResult.Value is List<POStatusController.POStatusTaktTimeModel> list)
            {
                delays = list;
            }
            else
            {
                // handle error or empty list
                delays = new List<POStatusController.POStatusTaktTimeModel>();
            }

            // Build Body
            var bodyBuilder = new StringBuilder();
            bodyBuilder.Append(@"
            <html>
            <head>
                <style>
                    body { font-family: Arial, sans-serif; background-color: #f7f7f7; margin: 0; padding: 0; }
                    .container { max-width: 600px; margin: 20px auto; background-color: #ffffff; padding: 20px; border-radius: 8px; box-shadow: 0 0 10px rgba(0,0,0,0.1);}
                    h1 { color: #d9534f; text-align: center; }
                    table { width: 100%; border-collapse: collapse; margin-top: 20px; }
                    th, td { padding: 10px; border-bottom: 1px solid #ddd; text-align: left; }
                    th { background-color: #f2f2f2; color: #333; }
                    a { color: #0275d8; text-decoration: none; }
                    a:hover { text-decoration: underline; }
                    .footer { font-size: 12px; color: #999; text-align: center; margin-top: 20px; }
                </style>
            </head>
            <body>
                <div class='container'>
                    <h1>Printer 1 Delay Notification</h1>
                    <p>The following POs have experienced delays / advance:</p>
                    <table>
                        <thead>
                            <tr>
                                <th>Time</th>
                                <th>PO Number</th>
                                <th>Production Line</th>
                                <th>Status</th>
                                <th>Action</th>
                            </tr>
                        </thead>
                        <tbody>
            ");

                        // Loop through delays
                        foreach (var delay in delays)
                        {
                            var hourlyKeys = delay.Target.hourly.Keys
                                .Select(k => DateTime.Parse(k))
                                .Where(dt => dt <= DateTime.Now)
                                .OrderBy(dt => dt)
                                .ToList();

                            var latestTime = hourlyKeys.LastOrDefault();
                            string displayTime = latestTime != default ? latestTime.ToString("yyyy-MM-dd hh tt") : "N/A";

                            bodyBuilder.Append($@"
                                <tr>
                                    <td>{displayTime}</td>
                                    <td>{delay.poStatus.PO}</td>
                                    <td>{delay.poStatus.ProdLine}</td>
                                    <td>{delay.Status}</td>
                                    <td><a href='http://apbiphbpswb02/homs/production/mes_po_reason?po={delay.poStatus.PO}&forDateTime={displayTime}'>Submit Reason</a></td>
                                </tr>
                            ");
                        }

                        bodyBuilder.Append(@"
                        </tbody>
                    </table>
                    <p class='footer'>This is an automated notification from HOMS. Please do not reply to this email.</p>
                </div>
            </body>
            </html>
            ");

            string body = bodyBuilder.ToString();

            //Send Email
            using var smtp = new SmtpClient("10.113.10.1", 25)
            {
                Credentials = new NetworkCredential("", ""),
                EnableSsl = false
            };

            using var message = new MailMessage();
            foreach (var email in pr1Emails)
            {
                message.To.Add(email.EmailAddress);
            }

            message.Subject = "[BIPH_BPS] Printer 1 Delay Notification";
            message.IsBodyHtml = true;

            var htmlView = AlternateView.CreateAlternateViewFromString(
                body,
                null,
                MediaTypeNames.Text.Html
            );
            message.AlternateViews.Add(htmlView);

            message.From = new MailAddress("HOMS@brother-biph.ph.com");

            await smtp.SendMailAsync(message);

            return Ok();
        }
    }
}
