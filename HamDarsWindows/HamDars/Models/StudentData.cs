namespace HamDars.Models;

public sealed class StudentData
{
    public List<StudyTask> Tasks { get; set; } = [];
    public int Sessions { get; set; }
    public int SessionMinutes { get; set; }
    public int GoalMinutes { get; set; } = 120;
    public bool RemindersEnabled { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.Today;

    public static StudentData CreateDefault() => new()
    {
        Tasks =
        [
            new StudyTask { Subject = "زیست‌شناسی — مرور نکته‌ها", Category = "علوم و زیست", Duration = 45, Time = "16:00" },
            new StudyTask { Subject = "ریاضی — حل تمرین", Category = "ریاضی", Duration = 40, Time = "17:00" },
            new StudyTask { Subject = "ادبیات — آرایه‌ها", Category = "ادبیات", Duration = 30, Time = "18:00" }
        ]
    };
}
