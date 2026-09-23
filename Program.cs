using CloudFileSystem.Application;
using CloudFileSystem.Application.Commands;
using CloudFileSystem.Daos;
using CloudFileSystem.Handlers;
using CloudFileSystem.Managers;
using CloudFileSystem.Managers.Impl;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IFileDao, FileDao>();
builder.Services.AddSingleton<IDirectoryDao, DirectoryDao>();
builder.Services.AddSingleton<IFileManager, FileManager>();
builder.Services.AddSingleton<IDirectoryManager, DirectoryManager>();
builder.Services.AddSingleton<FileSystemCommandHistory>();
builder.Services.AddSingleton<IFileSystemHandler, FileSystemHandler>();

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowAngular");

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
