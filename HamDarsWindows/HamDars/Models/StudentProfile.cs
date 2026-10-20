namespace HamDars.Models;

public sealed class StudentProfile
{
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Username { get; set; } = "";
    public string Grade { get; set; } = "";
    public string PasswordSalt { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
