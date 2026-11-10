using System.IO;
using System.Security.Cryptography;
using DocumentGenerator.Domain;
using DocumentGenerator.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DocumentGenerator.Services;

public sealed class TemplateService
{
    public async Task<List<TemplateRecord>> GetAllAsync(string? search = null)
    {
        await using var db = new AppDbContext();
        // Завантажуємо з SQLite, фільтруємо в пам'яті:
        // SQLite lower()/LIKE за замовчуванням погано працюють з кирилицею,
        // а StringComparison.OrdinalIgnoreCase коректно обробляє українські літери, цифри та спецсимволи.
        var items = await db.Templates.AsNoTracking().ToListAsync();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            items = items
                .Where(x =>
                    (!string.IsNullOrEmpty(x.Name) &&
                     x.Name.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(x.Category) &&
                     x.Category.Contains(term, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }
        return items
            .OrderBy(x => x.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<TemplateRecord> ImportAsync(string sourcePath)
    {
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("Файл не знайдено.", sourcePath);
        if (!string.Equals(Path.GetExtension(sourcePath), ".docx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Підтримуються лише файли DOCX.");
        // Validate package before it enters the template library.
        _ = new DocxTemplateEngine().FindPlaceholders(sourcePath);
        var safeName = Path.GetFileName(sourcePath);
        var destinationName = $"{Guid.NewGuid():N}_{safeName}";
        var destination = Path.Combine(AppPaths.Templates, destinationName);
        File.Copy(sourcePath, destination, false);
        var record = new TemplateRecord
        {
            Name = Path.GetFileNameWithoutExtension(sourcePath),
            RelativePath = Path.Combine("templates", destinationName),
            Sha256 = await CalculateHashAsync(destination),
            CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        try
        {
            await using var db = new AppDbContext();
            db.Templates.Add(record);
            await db.SaveChangesAsync();
            return record;
        }
        catch { if (File.Exists(destination)) File.Delete(destination); throw; }
    }

    public async Task RenameAsync(int id, string name, string category)
    {
        name = name.Trim(); category = string.IsNullOrWhiteSpace(category) ? "Інше" : category.Trim();
        if (name.Length is < 1 or > 240) throw new ArgumentException("Назва має містити від 1 до 240 символів.");
        await using var db = new AppDbContext();
        var item = await db.Templates.FirstOrDefaultAsync(x => x.Id == id) ?? throw new InvalidOperationException("Шаблон не знайдено.");
        item.Name = name; item.Category = category; item.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = new AppDbContext();
        var item = await db.Templates.FirstOrDefaultAsync(x => x.Id == id) ?? throw new InvalidOperationException("Шаблон не знайдено.");
        var path = AppPaths.ResolveTemplate(item.RelativePath);
        // Move to backup first so accidental deletion is recoverable.
        if (File.Exists(path)) File.Move(path, Path.Combine(AppPaths.Backups, $"deleted_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Path.GetFileName(path)}"));
        db.Templates.Remove(item);
        await db.SaveChangesAsync();
    }

    public async Task BackupAllAsync()
    {
        var destination = Path.Combine(AppPaths.Backups, $"backup_{DateTime.Now:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(destination);
        List<TemplateRecord> items;
        await using (var db = new AppDbContext())
            items = await db.Templates.AsNoTracking().ToListAsync();
        foreach (var item in items)
        {
            var source = AppPaths.ResolveTemplate(item.RelativePath);
            if (File.Exists(source)) File.Copy(source, Path.Combine(destination, Path.GetFileName(source)), true);
        }
        if (File.Exists(AppPaths.Database)) File.Copy(AppPaths.Database, Path.Combine(destination, "document-generator.db"), true);
    }

    private static async Task<string> CalculateHashAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream));
    }
}
