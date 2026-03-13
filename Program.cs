using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

var dataPath = Path.Combine(AppContext.BaseDirectory, "data", "tasks.json");
builder.Services.AddSingleton<ITaskStore>(_ => new JsonTaskStore(dataPath));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/tasks", async (ITaskStore store) => Results.Ok(await store.GetAllAsync()));

app.MapPost("/api/tasks", async (TaskCreateRequest request, ITaskStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.Title))
    {
        return Results.BadRequest("Title is required.");
    }

    var created = await store.AddAsync(request.Title.Trim(), request.ReminderAtUtc);
    return Results.Created($"/api/tasks/{created.Id}", created);
});

app.MapPut("/api/tasks/{id:int}", async (int id, TaskUpdateRequest request, ITaskStore store) =>
{
    var updated = await store.UpdateAsync(id, request);
    return updated is null ? Results.NotFound() : Results.Ok(updated);
});

app.MapDelete("/api/tasks/{id:int}", async (int id, ITaskStore store) =>
{
    var deleted = await store.DeleteAsync(id);
    return deleted ? Results.NoContent() : Results.NotFound();
});

app.Run();

public record TaskItem
{
    public int Id { get; init; }
    public string Title { get; set; } = string.Empty;
    public bool IsDone { get; set; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ReminderAtUtc { get; set; }
}

public sealed record TaskCreateRequest
{
    public string Title { get; init; } = string.Empty;
    public DateTime? ReminderAtUtc { get; init; }
}

public sealed record TaskUpdateRequest
{
    public string? Title { get; init; }
    public bool? IsDone { get; init; }
    public DateTime? ReminderAtUtc { get; init; }
    public bool? ClearReminder { get; init; }
}

public interface ITaskStore
{
    Task<List<TaskItem>> GetAllAsync();
    Task<TaskItem?> GetByIdAsync(int id);
    Task<TaskItem> AddAsync(string title, DateTime? reminderAtUtc);
    Task<TaskItem?> UpdateAsync(int id, TaskUpdateRequest request);
    Task<bool> DeleteAsync(int id);
}

public sealed class JsonTaskStore : ITaskStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public JsonTaskStore(string path)
    {
        _path = path;
    }

    public async Task<List<TaskItem>> GetAllAsync()
    {
        await _mutex.WaitAsync();
        try
        {
            return await LoadAsync();
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<TaskItem?> GetByIdAsync(int id)
    {
        await _mutex.WaitAsync();
        try
        {
            var tasks = await LoadAsync();
            return tasks.FirstOrDefault(t => t.Id == id);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<TaskItem> AddAsync(string title, DateTime? reminderAtUtc)
    {
        await _mutex.WaitAsync();
        try
        {
            var tasks = await LoadAsync();
            var nextId = tasks.Count == 0 ? 1 : tasks.Max(t => t.Id) + 1;
            var item = new TaskItem
            {
                Id = nextId,
                Title = title,
                IsDone = false,
                CreatedAt = DateTime.UtcNow,
                ReminderAtUtc = reminderAtUtc
            };
            tasks.Add(item);
            await SaveAsync(tasks);
            return item;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<TaskItem?> UpdateAsync(int id, TaskUpdateRequest request)
    {
        await _mutex.WaitAsync();
        try
        {
            var tasks = await LoadAsync();
            var task = tasks.FirstOrDefault(t => t.Id == id);
            if (task == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(request.Title))
            {
                task.Title = request.Title.Trim();
            }

            if (request.IsDone.HasValue)
            {
                task.IsDone = request.IsDone.Value;
            }

            if (request.ClearReminder == true)
            {
                task.ReminderAtUtc = null;
            }
            else if (request.ReminderAtUtc.HasValue)
            {
                task.ReminderAtUtc = request.ReminderAtUtc.Value;
            }

            await SaveAsync(tasks);
            return task;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await _mutex.WaitAsync();
        try
        {
            var tasks = await LoadAsync();
            var removed = tasks.RemoveAll(t => t.Id == id) > 0;
            if (removed)
            {
                await SaveAsync(tasks);
            }
            return removed;
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task<List<TaskItem>> LoadAsync()
    {
        var directory = Path.GetDirectoryName(_path) ?? "data";
        Directory.CreateDirectory(directory);

        if (!File.Exists(_path))
        {
            return new List<TaskItem>();
        }

        var json = await File.ReadAllTextAsync(_path);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<TaskItem>();
        }

        return JsonSerializer.Deserialize<List<TaskItem>>(json, JsonOptions) ?? new List<TaskItem>();
    }

    private async Task SaveAsync(List<TaskItem> tasks)
    {
        var directory = Path.GetDirectoryName(_path) ?? "data";
        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(tasks, JsonOptions);
        await File.WriteAllTextAsync(_path, json);
    }
}
