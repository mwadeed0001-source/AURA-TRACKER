using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// 1. CORS Policy Configuration (Flutter Web ke liye taake Failed to fetch ka error na aaye)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// 2. Add services to the container.
builder.Services.AddControllers();

// 3. Swagger / OpenAPI services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 4. Development environment mein Swagger UI enable karna
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(); // Yeh browser mein Swagger page open karega
}

// 5. Middleware Pipeline (Yahan tarteeb bohot ahem hai)
app.UseHttpsRedirection();

// CORS ko routing aur controllers se pehle hona lazmi hai
app.UseCors("AllowAll");

app.UseAuthorization();

app.MapControllers();

app.Run();
