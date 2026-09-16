using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// 1. CORS Policy Configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddControllers();

// 💡 SMART PORT FIX: Agar Railway par hai toh PORT uthaye, warna local par default chalne de
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// 3. Swagger / OpenAPI services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 🛠️ DEVELOPER EXCEPTION PAGE: Production par bhi exact C# error dekhne ke liye taake pata chale 500 kyun aa raha hai
app.UseDeveloperExceptionPage();

// 4. Swagger UI enable karna
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Aura Tracker API V1");
    c.RoutePrefix = string.Empty; // Yeh karne se app kholte hi direct Swagger page open ho jayega!
});

// ❌ app.UseHttpsRedirection(); ko yahan se bilkul hata diya gaya hai kyunke Railway khud SSL/HTTPS handle karta hai.

// 5. Middleware Pipeline
// CORS ko routing aur controllers se pehle hona lazmi hai
app.UseCors("AllowAll");

app.UseAuthorization();

app.MapControllers();

app.Run();
