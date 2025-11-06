using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Core;

namespace HOMS_MES_Extractor_Web.Data
{
    public class HOMS_MES_Extractor_WebContext : DbContext
    {
        public HOMS_MES_Extractor_WebContext (DbContextOptions<HOMS_MES_Extractor_WebContext> options)
            : base(options)
        {
        }

        public DbSet<Core.PoRecord> PoRecord { get; set; } = default!;
        public DbSet<Core.PR1POL> PR1POL { get; set; } = default!;
        public DbSet<Core.POStatus> POStatus { get; set; } = default!;
    }
}
