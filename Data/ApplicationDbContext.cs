
using AIImageService.Models;
using Microsoft.EntityFrameworkCore;

namespace AIImageService.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<ImageAnalysis> ImageAnalyses => Set<ImageAnalysis>();
}
