using AIImageService.Data;
using AIImageService.Services;
using Microsoft.EntityFrameworkCore;
using Amazon;
using Amazon.S3;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient<GeminiImageAnalysisService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(3);
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("PostgreSQL")));

var awsRegion = builder.Configuration["AWS:Region"]
    ?? "eu-central-1";

builder.Services.AddSingleton<IAmazonS3>(_ =>
    new AmazonS3Client(RegionEndpoint.GetBySystemName(awsRegion)));

builder.Services.AddScoped<S3StorageService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Analysis}/{action=Index}/{id?}");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<ApplicationDbContext>();

    db.Database.EnsureCreated();
}

app.Run();
