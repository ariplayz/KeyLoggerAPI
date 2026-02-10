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

// Update this route to receive the file correctly
app.MapPost("/upload", async (HttpRequest request) =>
{
    if (!request.HasFormContentType || request.Form.Files.Count == 0)
    {
        return Results.BadRequest("No file uploaded.");
    }

    var username = request.Form["username"].ToString();
    if (string.IsNullOrWhiteSpace(username))
    {
        return Results.BadRequest("Username is required.");
    }

    // Sanitize username to prevent directory traversal
    var safeUsername = Path.GetFileName(username);

    var baseUploadPath = "/root/uploads";
    var userUploadPath = Path.Combine(baseUploadPath, safeUsername);
    Directory.CreateDirectory(userUploadPath);
 
    var file = request.Form.Files[0]; // Get the first uploaded file
    var filePath = Path.Combine(userUploadPath, file.FileName);

    // Save the file
    using (var stream = new FileStream(filePath, FileMode.Create))
    {
        await file.CopyToAsync(stream);
    }

    return Results.Ok(new { filePath });
});

app.Run();