using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Core;
using HOMS_MES_Extractor_Web.Data;
using System.Net.Http.Json;
using System.Text;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace HOMS_MES_Extractor_Web.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly HOMS_MES_Extractor_WebContext _context;
        private readonly HttpClient _httpClient;

        public UsersController(HOMS_MES_Extractor_WebContext context, HttpClient httpClient)
        {
            _context = context;
            _httpClient = new HttpClient();
        }

        // GET: api/Users
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetAllUsers()
        {
            // 1. GET ALL APPROVERS FROM PORTAL API
            var response = await _httpClient.GetAsync(
                "http://apbiphbpswb01:80/PortalAPI/api/SystemApproverLists/SearchViaSystem?systemID=64"
            );

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode,
                    $"Portal API call failed with status {response.StatusCode}.");
            }

            var json = await response.Content.ReadAsStringAsync();
            var approvers = JArray.Parse(json);

            // 2. GET ALL LOCAL USERS
            var localUsers = await _context.Users.ToListAsync();

            // 3. JOIN (PortalAPI.id == LocalDB.PortalID)
            var result =
                from local in localUsers
                join api in approvers
                      on local.PortalID equals (int)api["id"]
                select new
                {
                    // Local DB fields
                    local.Id,
                    local.PortalID,
                    local.IsAdmin,

                    // API fields
                    employeeNumber = api["employeeNumber"]?.ToString(),
                    fullName = api["fullName"]?.ToString(),
                    section = api["section"]?.ToString(),
                    position = api["position"]?.ToString(),
                    emailAddress = api["emailAddress"]?.ToString(),
                    adid = api["adid"]?.ToString(),

                };

            return Ok(result);
        }

        [HttpGet("Sync")]
        public async Task<ActionResult<IEnumerable<object>>> SyncUsers()
        {
            await SyncPortalApproversToLocalDb(); // auto sync before returning

            // then your joined output...
            return await GetAllUsers();
        }

        private async Task SyncPortalApproversToLocalDb()
        {
            // 1. CALL PORTAL API
            var response = await _httpClient.GetAsync(
                "http://apbiphbpswb01:80/PortalAPI/api/SystemApproverLists/SearchViaSystem?systemID=64"
            );

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Portal API call failed: {response.StatusCode}");

            var json = await response.Content.ReadAsStringAsync();
            JArray approvers = JArray.Parse(json);

            // 2. GET ALL LOCAL USERS
            var localUsers = await _context.Users.ToListAsync();
            var localPortalIds = localUsers.Select(u => u.PortalID).ToHashSet();

            List<Users> newUsers = new List<Users>();

            // 3. LOOP THROUGH PORTAL API RECORDS AND INSERT MISSING
            foreach (var a in approvers)
            {
                if (!int.TryParse(a["id"]?.ToString(), out int portalId))
                    continue; // skip invalid rows

                // If local does NOT contain this PortalID → ADD
                if (!localPortalIds.Contains(portalId))
                {
                    newUsers.Add(new Users
                    {
                        PortalID = portalId,
                        IsAdmin = false // default
                    });
                }
            }

            // 4. SAVE CHANGES (ONLY IF NEEDED)
            if (newUsers.Count > 0)
            {
                _context.Users.AddRange(newUsers);
                await _context.SaveChangesAsync();
            }
        }


        // GET: api/Users/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Users>> GetUsers(int id)
        {
            var users = await _context.Users.FindAsync(id);

            if (users == null)
            {
                return NotFound();
            }

            return users;
        }

        // PUT: api/Users/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutUsers(int id, Users users)
        {
            if (id != users.Id)
            {
                return BadRequest();
            }

            _context.Entry(users).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!UsersExists(id))
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

        public class UserBIPHID
        {
            public string BIPHID { get; set; }
            public bool IsAdmin { get; set; }
        }

        // POST: api/Users
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Users>> PostUsers(UserBIPHID input)
        {
            // --- (A) LOCAL DB DUPLICATE CHECK ---
            var existingLocal = await _context.Users
                .FirstOrDefaultAsync(u => u.PortalID.ToString() == input.BIPHID);

            if (existingLocal != null)
            {
                return Conflict($"User with BIPHID '{input.BIPHID}' already exists in this system.");
            }

            // 1. Call external API (Lookup Portal User)
            var response = await _httpClient.GetAsync(
                "http://apbiphbpswb01:80/PortalAPI/api/SystemApproverLists/SearchViaSystem?systemID=64"
            );

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode,
                    $"Portal API call failed with status {response.StatusCode}.");
            }

            var json = await response.Content.ReadAsStringAsync();
            JArray approvers = JArray.Parse(json);

            // 2. Find matching BIPHID
            var matched = approvers.FirstOrDefault(x =>
                x["employeeNumber"]?.ToString().Equals(input.BIPHID,
                    StringComparison.OrdinalIgnoreCase) == true
            );

            if (matched == null)
            {
                return BadRequest($"Employee number '{input.BIPHID}' not found in the Portal Approver list.");
            }

            if (!int.TryParse(matched["id"]?.ToString(), out int portalId))
            {
                return BadRequest("Could not parse a valid 'id' (Portal ID) from the API response.");
            }

            // --- (B) PORTAL API DUPLICATE CHECK ---
            var responseDupCheck = await _httpClient.GetAsync(
                $"http://apbiphbpswb01:80/PortalAPI/api/SystemApproverLists/SearchViaSystem?systemID=77"
            );

            if (!responseDupCheck.IsSuccessStatusCode)
            {
                return StatusCode((int)responseDupCheck.StatusCode,
                    "Portal API failed during duplicate check.");
            }

            var jsonDup = JArray.Parse(await responseDupCheck.Content.ReadAsStringAsync());

            bool alreadyExistsInPortal = jsonDup.Any(x =>
                x["employeeNumber"]?.ToString() == input.BIPHID
            );

            if (alreadyExistsInPortal)
            {
                return Conflict($"Employee '{input.BIPHID}' is already registered as a System Approver.");
            }

            // 3. Create LOCAL DB entity
            var newUser = new Users
            {
                PortalID = portalId,
                IsAdmin = input.IsAdmin
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // 3.1 Create Portal record
            var portalPayload = new
            {
                employeeNumber = input.BIPHID,
                systemID = 77,
                systemName = "Logistic Dashboard",
                approverNumber = 0
            };

            var jsonContent = new StringContent(
                JsonConvert.SerializeObject(portalPayload),
                Encoding.UTF8,
                "application/json"
            );

            var response2 = await _httpClient.PostAsync(
                "http://apbiphbpswb01:80/PortalAPI/api/SystemApproverLists",
                jsonContent
            );

            if (!response2.IsSuccessStatusCode)
            {
                return StatusCode((int)response2.StatusCode,
                    $"Portal API insert failed with status {response2.StatusCode}");
            }

            // 4. Return 201 Created
            return CreatedAtAction(nameof(GetUsers),
                new { id = newUser.Id },
                newUser
            );
        }

        // DELETE: api/Users/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUsers(int id)
        {
            var users = await _context.Users.FindAsync(id);
            if (users == null)
            {
                return NotFound();
            }

            _context.Users.Remove(users);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool UsersExists(int id)
        {
            return _context.Users.Any(e => e.Id == id);
        }
    }
}
