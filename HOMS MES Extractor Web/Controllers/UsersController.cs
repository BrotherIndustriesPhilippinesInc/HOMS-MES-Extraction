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
            // 1️⃣ LOCAL DUPLICATE CHECK (based on BIPHID or portalID if available)
            bool existsLocal = await _context.Users
                .AnyAsync(u => u.PortalID.ToString() == input.BIPHID);

            if (existsLocal)
                return Conflict($"User with BIPHID '{input.BIPHID}' already exists in this system.");

            // 2️⃣ INSERT INTO PORTAL FIRST
            var portalPayload = new
            {
                employeeNumber = input.BIPHID,
                systemID = 64,
                systemName = "Hourly Output Monitoring System V2",
                approverNumber = 0
            };

            var content = new StringContent(
                JsonConvert.SerializeObject(portalPayload),
                Encoding.UTF8,
                "application/json"
            );

            var postPortal = await _httpClient.PostAsync(
                "http://apbiphbpswb01:80/PortalAPI/api/SystemApproverLists",
                content
            );

            if (!postPortal.IsSuccessStatusCode)
                return StatusCode((int)postPortal.StatusCode,
                    $"Portal API insert failed: {postPortal.StatusCode}");

            // 3️⃣ GET RESPONSE AND RETRIEVE PORTAL ID
            var portalJson = await postPortal.Content.ReadAsStringAsync();
            var portalResponse = JObject.Parse(portalJson);

            if (!int.TryParse(portalResponse["id"]?.ToString(), out int portalId))
                return StatusCode(500, "Failed to retrieve PortalID from Portal API response.");

            // 4️⃣ INSERT INTO LOCAL DB
            var newUser = new Users
            {
                PortalID = portalId,
                IsAdmin = input.IsAdmin
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // 5️⃣ RETURN RESULT
            return CreatedAtAction(nameof(GetUsers), new { id = newUser.Id }, newUser);
        }



        public class UserProfile()
        {
            public string emp_id { get; set; }
        }
        // DELETE: api/Users/5
        [HttpPost("DeleteUsers")]
        public async Task<IActionResult> DeleteUsers([FromBody] UserProfile emp_id)
        {
            // 1. Get Portal list
            var response = await _httpClient.GetAsync(
                "http://apbiphbpswb01:80/PortalAPI/api/SystemApproverLists/SearchViaSystem?systemID=64"
            );

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode,
                    $"Portal API lookup failed with status {response.StatusCode}.");
            }

            var portalList = JArray.Parse(await response.Content.ReadAsStringAsync());

            // 2. Find the external record by employee number
            var matchedPortal = portalList.FirstOrDefault(x =>
                x["employeeNumber"]?.ToString() == emp_id.emp_id
            );

            if (matchedPortal == null)
            {
                return NotFound($"Employee '{emp_id.emp_id}' does not exist in Portal Approver List.");
            }

            // 3. Extract Portal ID
            if (!int.TryParse(matchedPortal["id"]?.ToString(), out int portalId))
            {
                return BadRequest("Invalid Portal ID found in external record.");
            }

            // 4. Match local DB using PortalID
            var localUser = await _context.Users
                .FirstOrDefaultAsync(u => u.PortalID == portalId);

            if (localUser == null)
            {
                return NotFound($"Local user with PortalID '{portalId}' not found.");
            }

            // 5. Delete from Portal API first
            var postData = new StringContent(
                JsonConvert.SerializeObject(new { employeeNumber = emp_id.emp_id, systemID = 64 }),
                Encoding.UTF8,
                "application/json"
            );

            var deleteResponse = await _httpClient.PostAsync(
                $"http://apbiphbpswb01:80/PortalAPI/api/SystemApproverLists/Delete",
                postData
            );

            if (!deleteResponse.IsSuccessStatusCode)
            {
                return StatusCode((int)deleteResponse.StatusCode,
                    $"Portal API deletion failed ({deleteResponse.StatusCode}). Local record NOT removed.");
            }

            // 6. Delete local DB record
            _context.Users.Remove(localUser);
            await _context.SaveChangesAsync();

            return Ok(new { message = "ok" });
        }

        public class UserUpdateDTO
        {
            public bool IsAdmin { get; set; }
        }

        [HttpPost("UpdateUser/{biphid}")]
        public async Task<IActionResult> UpdateUser(string biphid, UserUpdateDTO input)
        {
            // 1️⃣ Find local user by BIPHID (or PortalID)
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.PortalID.ToString() == biphid);

            if (existingUser == null)
                return NotFound($"User with BIPHID '{biphid}' not found locally.");

            // 2️⃣ Update allowed fields
            existingUser.IsAdmin = input.IsAdmin;

            // 3️⃣ Save changes
            await _context.SaveChangesAsync();

            return Ok(existingUser);
        }



        private bool UsersExists(int id)
        {
            return _context.Users.Any(e => e.Id == id);
        }
    }
}
