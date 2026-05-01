using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace VideoSplitter.Data
{
    // Design-time factory for dotnet-ef to construct the DbContext outside of the MAUI host
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<VideoSplitterDbContext>
    {
        public VideoSplitterDbContext CreateDbContext(string[] args)
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "VideoSplitter");
            Directory.CreateDirectory(appFolder);
            var dbPath = Path.Combine(appFolder, "videosplitter.db");

            var optionsBuilder = new DbContextOptionsBuilder<VideoSplitterDbContext>();
            optionsBuilder.UseSqlite($"Data Source={dbPath};Mode=ReadWriteCreate;Cache=Shared");

            return new VideoSplitterDbContext(optionsBuilder.Options);
        }
    }
}
