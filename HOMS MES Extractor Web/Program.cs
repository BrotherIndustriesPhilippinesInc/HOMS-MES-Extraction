using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using HOMS_MES_Extractor_Web.Data;
var builder = WebApplication.CreateBuilder(args);

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod(); // important for DELETE/PUT
    });
});


builder.Services.AddDbContext<HOMS_MES_Extractor_WebContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("HOMS_MES_Extractor_WebContext") ?? throw new InvalidOperationException("Connection string 'HOMS_MES_Extractor_WebContext' not found.")));

// ✅ Add controllers (API only)
builder.Services.AddControllers();

builder.Services.AddHttpClient();

// ✅ Optional: Enable OpenAPI / Swagger for testing
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseCors("AllowAll");

// ✅ Use Swagger in development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", context =>
{
    context.Response.Redirect("/swagger");
    return Task.CompletedTask;
});

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

// ✅ Add controllers (API only)
app.MapControllers();

app.Run();
