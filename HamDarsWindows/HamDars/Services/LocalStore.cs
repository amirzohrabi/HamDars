using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using HamDars.Models;

namespace HamDars.Services;

/// <summary>
/// Local storage repository for the desktop app. Passwords are stored as PBKDF2 hashes, never plaintext.
/// For multi-device sign-in, replace this service with a secure remote authentication backend.
/// </summary>
public static class LocalStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HamDars");
    private static readonly string ProfilesFile = Path.Combine(Root, "profiles.json");

    static LocalStore() => Directory.CreateDirectory(Root);

    public static List<StudentProfile> LoadProfiles()
    {
        try
        {
            if (!File.Exists(ProfilesFile)) return [];
            List<StudentProfile> profiles = JsonSerializer.Deserialize<List<StudentProfile>>(File.ReadAllText(ProfilesFile)) ?? [];
            return profiles
                .Where(profile => profile is not null
                    && !string.IsNullOrWhiteSpace(profile.Username)
                    && !string.IsNullOrWhiteSpace(profile.PasswordSalt)
                    && !string.IsNullOrWhiteSpace(profile.PasswordHash))
                .ToList();
        }
        catch (IOException) { return []; }
        catch (UnauthorizedAccessException) { return []; }
        catch (JsonException)
        {
            QuarantineCorruptFile(ProfilesFile);
            return [];
        }
    }

    public static void SaveProfiles(List<StudentProfile> profiles) =>
        WriteAtomically(ProfilesFile, JsonSerializer.Serialize(profiles, JsonOptions));

    public static StudentProfile CreateProfile(string name, string phone, string username, string password, string grade)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 210_000, HashAlgorithmName.SHA256, 32);
        return new StudentProfile
        {
            Name = name.Trim(), Phone = NormalizePhone(phone), Username = NormalizeUsername(username), Grade = grade,
            PasswordSalt = Convert.ToBase64String(salt), PasswordHash = Convert.ToBase64String(hash), CreatedAt = DateTime.Now
        };
    }

    public static bool VerifyPassword(StudentProfile profile, string password)
    {
        try
        {
            byte[] salt = Convert.FromBase64String(profile.PasswordSalt);
            byte[] expected = Convert.FromBase64String(profile.PasswordHash);
            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, 210_000, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException) { return false; }
    }

    public static StudentProfile? FindLogin(string identifier)
    {
        string username = NormalizeUsername(identifier);
        string phone = NormalizePhone(identifier);
        return LoadProfiles().FirstOrDefault(p =>
            string.Equals(p.Username, username, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrWhiteSpace(phone) && p.Phone == phone));
    }

    public static StudentData LoadData(string username)
    {
        string file = DataFile(username);
        try
        {
            if (File.Exists(file))
            {
                var data = JsonSerializer.Deserialize<StudentData>(File.ReadAllText(file));
                if (data is not null)
                {
                    // Defend against missing or malformed fields in older/corrupted local JSON files.
                    data.Tasks ??= [];
                    data.Tasks = data.Tasks.Where(task => task is not null).ToList();
                    foreach (StudyTask task in data.Tasks)
                    {
                        if (string.IsNullOrWhiteSpace(task.Id)) task.Id = Guid.NewGuid().ToString("N");
                        task.Subject ??= "درس بدون عنوان";
                        task.Category = string.IsNullOrWhiteSpace(task.Category) ? "سایر درس‌ها" : task.Category;
                        task.Time ??= string.Empty;
                        if (task.Duration is < 5 or > 360) task.Duration = 30;
                    }
                    if (data.GoalMinutes is < 30 or > 720) data.GoalMinutes = 120;
                    data.Sessions = Math.Max(0, data.Sessions);
                    data.SessionMinutes = Math.Max(0, data.SessionMinutes);

                    // Reset daily metrics at the start of a new local day, while keeping the planned tasks.
                    if (data.UpdatedAt.Date != DateTime.Today)
                    {
                        foreach (StudyTask task in data.Tasks) task.Done = false;
                        data.Sessions = 0;
                        data.SessionMinutes = 0;
                        data.UpdatedAt = DateTime.Today;
                    }
                    return data;
                }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (JsonException) { QuarantineCorruptFile(file); }
        return StudentData.CreateDefault();
    }

    public static void SaveData(string username, StudentData data)
    {
        data.UpdatedAt = DateTime.Today;
        WriteAtomically(DataFile(username), JsonSerializer.Serialize(data, JsonOptions));
    }

    private static void QuarantineCorruptFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            File.Move(path, path + ".corrupt-" + stamp, overwrite: false);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void WriteAtomically(string path, string contents)
    {
        Directory.CreateDirectory(Root);
        string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            if (File.Exists(path))
                File.Replace(temporaryPath, path, destinationBackupFileName: null);
            else
                File.Move(temporaryPath, path);
        }
        catch
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            throw;
        }
    }

    public static string NormalizeUsername(string value) => value.Trim().ToLowerInvariant();

    public static string NormalizePhone(string value)
    {
        string normalized = (value ?? "").Trim()
            .Replace('۰', '0').Replace('۱', '1').Replace('۲', '2').Replace('۳', '3').Replace('۴', '4')
            .Replace('۵', '5').Replace('۶', '6').Replace('۷', '7').Replace('۸', '8').Replace('۹', '9')
            .Replace('٠', '0').Replace('١', '1').Replace('٢', '2').Replace('٣', '3').Replace('٤', '4')
            .Replace('٥', '5').Replace('٦', '6').Replace('٧', '7').Replace('٨', '8').Replace('٩', '9');
        string digits = Regex.Replace(normalized, "[^0-9]", "");
        if (digits.StartsWith("98") && digits.Length == 12) digits = "0" + digits[2..];
        if (digits.Length == 10 && digits.StartsWith('9')) digits = "0" + digits;
        return digits;
    }

    private static string DataFile(string username)
    {
        string safe = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(NormalizeUsername(username)))).ToLowerInvariant()[..24];
        return Path.Combine(Root, $"study_{safe}.json");
    }
}
