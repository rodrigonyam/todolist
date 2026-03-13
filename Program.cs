using System.Text.Json;
using System.Linq;

// Simple console to-do list that stores tasks in data/tasks.json next to the app
internal static class Program
{
    private const string DataDirectory = "data";
    private const string FileName = "tasks.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static void Main()
    {
        var storagePath = Path.Combine(Directory.GetCurrentDirectory(), DataDirectory, FileName);
        var tasks = LoadTasks(storagePath);

        while (true)
        {
            Console.Clear();
            HandleDueReminders(storagePath, tasks);
            Console.WriteLine("=== To-Do List ===");
            ListTasks(tasks);

            Console.WriteLine();
            Console.WriteLine("Choose an action:");
            Console.WriteLine("1) Add task");
            Console.WriteLine("2) Edit task");
            Console.WriteLine("3) Delete task");
            Console.WriteLine("4) Toggle done");
            Console.WriteLine("5) Set/Clear reminder");
            Console.WriteLine("6) Exit");
            Console.Write("Selection: ");

            var choice = Console.ReadLine();
            switch (choice)
            {
                case "1":
                    AddTask(tasks);
                    SaveTasks(storagePath, tasks);
                    break;
                case "2":
                    EditTask(tasks);
                    SaveTasks(storagePath, tasks);
                    break;
                case "3":
                    DeleteTask(tasks);
                    SaveTasks(storagePath, tasks);
                    break;
                case "4":
                    ToggleTask(tasks);
                    SaveTasks(storagePath, tasks);
                    break;
                case "5":
                    SetOrClearReminder(tasks);
                    SaveTasks(storagePath, tasks);
                    break;
                case "6":
                    return;
                default:
                    Console.WriteLine("Invalid choice. Press Enter to continue...");
                    Console.ReadLine();
                    break;
            }
        }
    }

    private static List<TaskItem> LoadTasks(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? DataDirectory);
            if (!File.Exists(path))
            {
                return new List<TaskItem>();
            }

            var json = File.ReadAllText(path);
            return string.IsNullOrWhiteSpace(json)
                ? new List<TaskItem>()
                : JsonSerializer.Deserialize<List<TaskItem>>(json, JsonOptions) ?? new List<TaskItem>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load tasks: {ex.Message}");
            Console.WriteLine("Starting with an empty list. Press Enter to continue...");
            Console.ReadLine();
            return new List<TaskItem>();
        }
    }

    private static void SaveTasks(string path, List<TaskItem> tasks)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? DataDirectory);
            File.WriteAllText(path, JsonSerializer.Serialize(tasks, JsonOptions));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save tasks: {ex.Message}");
            Console.WriteLine("Press Enter to continue...");
            Console.ReadLine();
        }
    }

    private static void ListTasks(List<TaskItem> tasks)
    {
        if (tasks.Count == 0)
        {
            Console.WriteLine("No tasks yet. Add one!");
            return;
        }

        foreach (var task in tasks.OrderBy(t => t.Id))
        {
            var status = task.IsDone ? "[x]" : "[ ]";
            var reminderText = task.ReminderAtUtc.HasValue
                ? $" (reminder: {task.ReminderAtUtc.Value.ToLocalTime():yyyy-MM-dd HH:mm})"
                : string.Empty;

            Console.WriteLine($"{task.Id,3} {status} {task.Title}{reminderText}");
        }
    }

    private static void AddTask(List<TaskItem> tasks)
    {
        Console.Write("Enter task description: ");
        var title = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(title))
        {
            Console.WriteLine("Task not added (empty input). Press Enter to continue...");
            Console.ReadLine();
            return;
        }

        var nextId = tasks.Count == 0 ? 1 : tasks.Max(t => t.Id) + 1;
        tasks.Add(new TaskItem
        {
            Id = nextId,
            Title = title.Trim(),
            IsDone = false,
            CreatedAt = DateTime.UtcNow
        });
    }

    private static void EditTask(List<TaskItem> tasks)
    {
        var task = PromptForTask(tasks, "edit");
        if (task == null)
        {
            return;
        }

        Console.Write("Enter new description (leave blank to keep current): ");
        var newTitle = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(newTitle))
        {
            task.Title = newTitle.Trim();
        }
    }

    private static void DeleteTask(List<TaskItem> tasks)
    {
        var task = PromptForTask(tasks, "delete");
        if (task == null)
        {
            return;
        }

        tasks.Remove(task);
    }

    private static void ToggleTask(List<TaskItem> tasks)
    {
        var task = PromptForTask(tasks, "toggle");
        if (task == null)
        {
            return;
        }

        task.IsDone = !task.IsDone;
    }

    private static void SetOrClearReminder(List<TaskItem> tasks)
    {
        var task = PromptForTask(tasks, "set/clear reminder for");
        if (task == null)
        {
            return;
        }

        Console.WriteLine("Enter reminder time (local) in format yyyy-MM-dd HH:mm, or leave blank to clear:");
        var input = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(input))
        {
            task.ReminderAtUtc = null;
            Console.WriteLine("Reminder cleared. Press Enter to continue...");
            Console.ReadLine();
            return;
        }

        if (DateTime.TryParse(input, out var localTime))
        {
            var utc = DateTime.SpecifyKind(localTime, DateTimeKind.Local).ToUniversalTime();
            task.ReminderAtUtc = utc;
            Console.WriteLine("Reminder set. Press Enter to continue...");
            Console.ReadLine();
        }
        else
        {
            Console.WriteLine("Could not parse that time. Press Enter to continue...");
            Console.ReadLine();
        }
    }

    private static void HandleDueReminders(string storagePath, List<TaskItem> tasks)
    {
        var nowUtc = DateTime.UtcNow;
        var due = tasks.Where(t => !t.IsDone && t.ReminderAtUtc.HasValue && t.ReminderAtUtc.Value <= nowUtc).ToList();
        if (due.Count == 0)
        {
            return;
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("-- Reminders --");
        foreach (var task in due)
        {
            Console.WriteLine($"Reminder: {task.Title} (was set for {task.ReminderAtUtc!.Value.ToLocalTime():yyyy-MM-dd HH:mm})");
            task.ReminderAtUtc = null; // clear after showing so it does not repeat every loop
        }
        Console.ResetColor();
        SaveTasks(storagePath, tasks);
        Console.WriteLine();
        Console.WriteLine("Press Enter to continue...");
        Console.ReadLine();
    }

    private static TaskItem? PromptForTask(List<TaskItem> tasks, string action)
    {
        if (tasks.Count == 0)
        {
            Console.WriteLine("No tasks available. Press Enter to continue...");
            Console.ReadLine();
            return null;
        }

        Console.Write($"Enter task id to {action}: ");
        var input = Console.ReadLine();
        if (!int.TryParse(input, out var id))
        {
            Console.WriteLine("Invalid number. Press Enter to continue...");
            Console.ReadLine();
            return null;
        }

        var task = tasks.FirstOrDefault(t => t.Id == id);
        if (task == null)
        {
            Console.WriteLine("Task not found. Press Enter to continue...");
            Console.ReadLine();
            return null;
        }

        return task;
    }

    private sealed class TaskItem
    {
        public int Id { get; init; }
        public string Title { get; set; } = string.Empty;
        public bool IsDone { get; set; }
        public DateTime CreatedAt { get; init; }
        public DateTime? ReminderAtUtc { get; set; }
    }
}
