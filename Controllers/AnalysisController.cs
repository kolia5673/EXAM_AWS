using Amazon.S3;
using AIImageService.Data;
using AIImageService.Models;
using AIImageService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIImageService.Controllers;

public class AnalysisController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly S3StorageService _s3Storage;
    private readonly GeminiImageAnalysisService _gemini;

    public AnalysisController(
        ApplicationDbContext db,
        IWebHostEnvironment environment,
        S3StorageService s3Storage,
        GeminiImageAnalysisService gemini)
    {
        _db = db;
        _environment = environment;
        _s3Storage = s3Storage;
        _gemini = gemini;
    }

    public async Task<IActionResult> Index()
    {
        var history = await _db.ImageAnalyses
            .OrderByDescending(x => x.CreatedAt)
            .Take(10)
            .ToListAsync();

        return View(history);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(IFormFile? image)
    {
        if (image == null || image.Length == 0)
        {
            TempData["Error"] = "Выберите изображение.";
            return RedirectToAction(nameof(Index));
        }

        var allowedTypes = new[]
        {
            "image/jpeg",
            "image/png",
            "image/webp"
        };

        if (!allowedTypes.Contains(image.ContentType) ||
            image.Length > 10 * 1024 * 1024)
        {
            TempData["Error"] =
                "Разрешены JPG, PNG и WEBP размером до 10 МБ.";

            return RedirectToAction(nameof(Index));
        }

        // Читаем изображение для отправки в Gemini.
        byte[] imageBytes;

        await using (var memory = new MemoryStream())
        {
            await image.CopyToAsync(memory);
            imageBytes = memory.ToArray();
        }

        // Получаем настоящий результат AI-анализа.
        string prediction;

        try
        {
            prediction = await _gemini.AnalyzeAsync(
                imageBytes,
                image.ContentType,
                HttpContext.RequestAborted);
        }

        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Gemini API error: {ex.Message}");

            TempData["Error"] =
                $"Ошибка Gemini API: {ex.Message}";

            return RedirectToAction(nameof(Index));
        }

        catch (TaskCanceledException)
        {
            TempData["Error"] =
                "Gemini не ответил вовремя. Попробуй загрузить изображение ещё раз.";
            return RedirectToAction(nameof(Index));
        }

        catch (InvalidOperationException)
        {
            TempData["Error"] =
                "Gemini не смог обработать изображение. " +
                "Проверь настройки API и попробуй ещё раз.";

            return RedirectToAction(nameof(Index));
        }

        string s3Key;

        try
        {
            s3Key = await _s3Storage.UploadAsync(
                imageBytes,
                image.FileName,
                image.ContentType,
                HttpContext.RequestAborted);
        }
        catch (Amazon.S3.AmazonS3Exception ex)
        {
            Console.WriteLine($"S3 upload error: {ex.Message}");
            TempData["Error"] = "Не удалось загрузить изображение в Amazon S3. Проверь настройки AWS.";
            return RedirectToAction(nameof(Index));
        }

        var uploadsPath = Path.Combine(_environment.WebRootPath ?? "wwwroot", "uploads");

        Directory.CreateDirectory(uploadsPath);

        var extension = Path.GetExtension(image.FileName)
            .ToLowerInvariant();

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(uploadsPath, fileName);

        await using (var stream = System.IO.File.Create(filePath))
        {
            await stream.WriteAsync(
                imageBytes,
                HttpContext.RequestAborted);
        }

        // Сохраняем результат анализа в базе данных.
        var analysis = new ImageAnalysis
        {
            FileName = Path.GetFileName(image.FileName),
            ImageUrl = $"/uploads/{fileName}",
            CreatedAt = DateTime.UtcNow,
            Prediction = prediction,
            Confidence = 0
        };

        _db.ImageAnalyses.Add(analysis);
        await _db.SaveChangesAsync(
            HttpContext.RequestAborted);

        TempData["Success"] = "Изображение успешно проанализировано!";

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> History()
    {
        var history = await _db.ImageAnalyses
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return View(history);
    }
}
