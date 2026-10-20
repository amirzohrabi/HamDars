using System.IO;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using HamDars.Models;
using HamDars.Services;

namespace HamDars;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<StudyTask> _tasks = [];
    private readonly DispatcherTimer _studyTimer;
    private readonly DispatcherTimer _toastTimer;
    private readonly List<string> _quotes =
    [
        "قرار نیست امروز از همه جلو بزنی؛ فقط کافیه از دیروزت یه قدم جلوتر باشی.",
        "لازم نیست حالش رو داشته باشی؛ فقط پنج دقیقه شروع کن، انگیزه گاهی بعد از شروع می‌رسه.",
        "هر تستی که تحلیل می‌کنی، حتی اگر غلط باشه، یه چیز تازه بهت یاد می‌ده.",
        "آینده با یک حرکت بزرگ ساخته نمی‌شه؛ با قدم‌های کوچیکی ساخته می‌شه که تکرارشون می‌کنی.",
        "قرار نیست بی‌نقص باشی؛ قرارِ امروزت فقط اینه که یک قدم واقعی برداری.",
        "گوشی رو چند دقیقه کنار بذار و به خودت فرصت بده ببینی چقدر از پسش برمیای.",
        "یک صفحه، پنج تست یا ده دقیقه تمرکز؛ هر شروع کوچیکی ارزشمنده.",
        "اگه امروز سخت می‌گذره، برنامه رو کوچیک‌تر کن؛ از مسیرت حذفش نکن.",
        "تو مجبور نیستی همه‌چیز رو همین الان بلد باشی؛ برای یادگرفتن وقت داری.",
        "پیشرفت همیشه پر سر و صدا نیست؛ گاهی یعنی امروز هم برگشتی سر درس.",
        "هر بار که بعد از حواس‌پرتی برمی‌گردی، داری مهارت تمرکزت رو تمرین می‌کنی.",
        "با خودت مثل یک دوست خوب حرف بزن؛ تشویق معمولاً از سرزنش مفیدتره.",
        "لازم نیست کل مسیر کنکور رو امروز بری؛ فقط قدم بعدی رو مشخص کن.",
        "یک استراحت درست و حسابی تنبلی نیست؛ بخشی از یک برنامه‌ی هوشمندانه‌ست.",
        "به جای فکرکردن به همه‌ی کارها، فقط درس بعدی رو باز کن. همین حالا.",
        "امروزت لازم نیست عالی باشه تا مفید باشه؛ فقط یک کار رو کامل کن.",
        "نتیجه از یک روز نمیاد؛ به تلاشی که آروم‌آروم جمع می‌کنی اعتماد کن.",
        "اشتباه‌ها حکم پایان ندارن؛ نشونه‌ان که کدوم بخش رو باید دوباره ببینی.",
        "ده دقیقه تمرکز واقعی بهتر از یک ساعت نگرانی درباره‌ی شروع کردنه.",
        "تو حق داری خسته بشی؛ کمی نفس بکش، بعد با یک قدم کوچک برگرد."
    ];

    private StudentProfile? _currentProfile;
    private StudentData _data = StudentData.CreateDefault();
    private bool _loginMode;
    private bool _timerRunning;
    private bool _isBreak;
    private int _timerDurationSeconds = 25 * 60;
    private int _timerRemainingSeconds = 25 * 60;
    private int _lastQuoteIndex = -1;

    public MainWindow()
    {
        InitializeComponent();
        HomeTaskList.ItemsSource = _tasks;
        PlannerTaskList.ItemsSource = _tasks;

        _studyTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _studyTimer.Tick += StudyTimer_Tick;
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.6) };
        _toastTimer.Tick += (_, _) => { _toastTimer.Stop(); ToastBorder.Visibility = Visibility.Collapsed; };
        TodayLabel.Text = FormatPersianDate(DateTime.Now);
        Loaded += Window_Loaded;
        Closing += (_, _) => { _studyTimer.Stop(); SaveCurrentData(); };
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var rocketAnimation = new DoubleAnimation(0, -8, TimeSpan.FromSeconds(1.4))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        MotivationRocketTransform.BeginAnimation(TranslateTransform.YProperty, rocketAnimation);
        SetAuthMode(login: false);
    }

    private static string FormatPersianDate(DateTime date)
    {
        var calendar = new PersianCalendar();
        string[] days = ["یکشنبه", "دوشنبه", "سه‌شنبه", "چهارشنبه", "پنجشنبه", "جمعه", "شنبه"];
        string[] months = ["فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"];
        return $"{days[(int)date.DayOfWeek]}، {ToFa(calendar.GetDayOfMonth(date))} {months[calendar.GetMonth(date) - 1]}";
    }

    private static string ToFa(int number) => number.ToString(CultureInfo.InvariantCulture).Replace('0', '۰').Replace('1', '۱').Replace('2', '۲').Replace('3', '۳').Replace('4', '۴').Replace('5', '۵').Replace('6', '۶').Replace('7', '۷').Replace('8', '۸').Replace('9', '۹');

    private static string ToAsciiDigits(string value) => (value ?? string.Empty)
        .Replace('۰', '0').Replace('۱', '1').Replace('۲', '2').Replace('۳', '3').Replace('۴', '4')
        .Replace('۵', '5').Replace('۶', '6').Replace('۷', '7').Replace('۸', '8').Replace('۹', '9')
        .Replace('٠', '0').Replace('١', '1').Replace('٢', '2').Replace('٣', '3').Replace('٤', '4')
        .Replace('٥', '5').Replace('٦', '6').Replace('٧', '7').Replace('٨', '8').Replace('٩', '9');

    private void RegisterMode_Click(object sender, RoutedEventArgs e) => SetAuthMode(login: false);
    private void LoginMode_Click(object sender, RoutedEventArgs e) => SetAuthMode(login: true);

    private void SetAuthMode(bool login)
    {
        _loginMode = login;
        RegisterFields.Visibility = login ? Visibility.Collapsed : Visibility.Visible;
        LoginFields.Visibility = login ? Visibility.Visible : Visibility.Collapsed;
        AuthHeading.Text = login ? "خوش برگشتی! 👋" : "به مسیر خودت خوش اومدی";
        AuthSubtitle.Text = login
            ? "با نام کاربری یا شماره موبایل وارد فضای مطالعه‌ات شو."
            : "ثبت‌نام کن تا فضای مخصوص درس و پیشرفتت رو داشته باشی.";
        SubmitAuthButton.Content = login ? "ورود به هم‌درس  ←" : "ساخت حساب کاربری  ←";
        RegisterModeButton.Style = (Style)FindResource(login ? "SecondaryButton" : "PrimaryButton");
        LoginModeButton.Style = (Style)FindResource(login ? "PrimaryButton" : "SecondaryButton");
        AuthStatus.Text = "";
    }

    private void SubmitAuth_Click(object sender, RoutedEventArgs e)
    {
        AuthStatus.Text = "";
        if (_loginMode) TryLogin(); else TryRegister();
    }

    private void TryRegister()
    {
        string name = NameBox.Text.Trim();
        string phoneInput = PhoneBox.Text.Trim();
        string phone = LocalStore.NormalizePhone(phoneInput);
        string username = LocalStore.NormalizeUsername(UsernameBox.Text);
        string password = RegisterPasswordBox.Password;
        string grade = (GradeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "سایر";

        if (name.Length < 3) { AuthStatus.Text = "نام و نام خانوادگی رو کامل‌تر وارد کن."; NameBox.Focus(); return; }
        if (phone.Length < 10 || phone.Length > 13) { AuthStatus.Text = "شماره موبایل رو بررسی کن؛ فقط شماره معتبر وارد کن."; PhoneBox.Focus(); return; }
        if (!Regex.IsMatch(username, @"^[\p{L}\p{N}_-]{3,24}$")) { AuthStatus.Text = "نام کاربری باید ۳ تا ۲۴ حرف، عدد یا _ و - داشته باشه."; UsernameBox.Focus(); return; }
        if (password.Length < 8) { AuthStatus.Text = "رمز عبور باید حداقل ۸ نویسه داشته باشه."; RegisterPasswordBox.Focus(); return; }

        try
        {
            List<StudentProfile> profiles = LocalStore.LoadProfiles();
            if (profiles.Any(p => string.Equals(p.Username, username, StringComparison.OrdinalIgnoreCase)))
            { AuthStatus.Text = "این نام کاربری قبلاً استفاده شده؛ یکی دیگه انتخاب کن."; UsernameBox.Focus(); return; }
            if (profiles.Any(p => LocalStore.NormalizePhone(p.Phone) == phone))
            { AuthStatus.Text = "این شماره قبلاً برای یک حساب ثبت شده."; PhoneBox.Focus(); return; }

            StudentProfile profile = LocalStore.CreateProfile(name, phone, username, password, grade);
            profiles.Add(profile);
            LocalStore.SaveProfiles(profiles);
            RegisterPasswordBox.Clear();
            EnterDashboard(profile, showWelcome: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AuthStatus.Text = "ذخیره اطلاعات روی این کامپیوتر انجام نشد. فضای دیسک و مجوز پوشه رو بررسی کن.";
        }
    }

    private void TryLogin()
    {
        string identifier = LoginIdentifierBox.Text.Trim();
        string password = LoginPasswordBox.Password;
        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrEmpty(password))
        { AuthStatus.Text = "نام کاربری یا شماره و رمز عبور رو وارد کن."; return; }

        StudentProfile? profile;
        try
        {
            profile = LocalStore.FindLogin(identifier);
            if (profile is null || !LocalStore.VerifyPassword(profile, password))
            {
                AuthStatus.Text = "اطلاعات ورود درست نیست. نام کاربری/شماره و رمز رو بررسی کن.";
                LoginPasswordBox.Clear();
                return;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            AuthStatus.Text = "خواندن اطلاعات حساب انجام نشد. دوباره تلاش کن و دسترسی پوشه اطلاعات رو بررسی کن.";
            LoginPasswordBox.Clear();
            return;
        }
        LoginPasswordBox.Clear();
        EnterDashboard(profile, showWelcome: true);
    }

    private void EnterDashboard(StudentProfile profile, bool showWelcome)
    {
        _currentProfile = profile;
        _data = LocalStore.LoadData(profile.Username);
        _tasks.Clear();
        foreach (StudyTask task in _data.Tasks.OrderBy(t => string.IsNullOrWhiteSpace(t.Time) ? "99:99" : t.Time, StringComparer.Ordinal)) _tasks.Add(task);
        AuthView.Visibility = Visibility.Collapsed;
        DashboardView.Visibility = Visibility.Visible;
        ProfileNameLabel.Text = profile.Name;
        ProfileGradeLabel.Text = $"پایه {profile.Grade}";
        ProfileInitial.Text = profile.Name.Length > 0 ? profile.Name[..1] : "هـ";
        SettingsProfileLabel.Text = $"{profile.Name} · {profile.Grade}";
        DailyGoalCombo.SelectedItem = DailyGoalCombo.Items.OfType<ComboBoxItem>().FirstOrDefault(item => (string?)item.Tag == _data.GoalMinutes.ToString(CultureInfo.InvariantCulture)) ?? DailyGoalCombo.Items[1];
        ShowPage("home");
        UpdateDashboard();
        if (showWelcome) ShowToast($"خوش اومدی {profile.Name} 🌱");
    }

    private void SignOut_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentData();
        _studyTimer.Stop();
        _timerRunning = false;
        _currentProfile = null;
        LoginIdentifierBox.Text = "";
        LoginPasswordBox.Clear();
        AuthView.Visibility = Visibility.Visible;
        DashboardView.Visibility = Visibility.Collapsed;
        SetAuthMode(login: true);
    }

    private void ShowPage(string page)
    {
        HomePage.Visibility = page == "home" ? Visibility.Visible : Visibility.Collapsed;
        PlannerPage.Visibility = page == "planner" ? Visibility.Visible : Visibility.Collapsed;
        FocusPage.Visibility = page == "focus" ? Visibility.Visible : Visibility.Collapsed;
        ProgressPage.Visibility = page == "progress" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = page == "settings" ? Visibility.Visible : Visibility.Collapsed;
        var (title, crumb) = page switch
        {
            "home" => ($"سلام، {_currentProfile?.Name ?? "قهرمان یادگیری"}! 👋", "فضای مطالعه / داشبورد"),
            "planner" => ("برنامه‌ریزی درسی", "فضای مطالعه / برنامه‌ریزی"),
            "focus" => ("وقت تمرکز رسیده", "فضای مطالعه / تایمر تمرکز"),
            "progress" => ("ببین چقدر جلو اومدی", "فضای مطالعه / گزارش پیشرفت"),
            _ => ("تنظیمات هم‌درس", "حساب کاربری / تنظیمات")
        };
        PageHeading.Text = title;
        PageCrumb.Text = crumb;
        StyleNav(NavHome, page == "home"); StyleNav(NavPlanner, page == "planner"); StyleNav(NavFocus, page == "focus"); StyleNav(NavProgress, page == "progress"); StyleNav(NavSettings, page == "settings");
    }

    private void StyleNav(Button button, bool active)
    {
        button.Background = active ? new SolidColorBrush(Color.FromRgb(48, 41, 79)) : Brushes.Transparent;
        button.Foreground = active ? new SolidColorBrush(Color.FromRgb(189, 180, 255)) : new SolidColorBrush(Color.FromRgb(165, 174, 192));
    }

    private void NavHome_Click(object sender, RoutedEventArgs e) => ShowPage("home");
    private void NavPlanner_Click(object sender, RoutedEventArgs e) => ShowPage("planner");
    private void NavFocus_Click(object sender, RoutedEventArgs e) => ShowPage("focus");
    private void NavProgress_Click(object sender, RoutedEventArgs e) => ShowPage("progress");
    private void NavSettings_Click(object sender, RoutedEventArgs e) => ShowPage("settings");
    private void GoPlanner_Click(object sender, RoutedEventArgs e) => ShowPage("planner");

    private int StudiedMinutes => _tasks.Where(t => t.Done).Sum(t => t.Duration) + _data.SessionMinutes;
    private int CompletedTasks => _tasks.Count(t => t.Done);
    private int TargetProgress => _data.GoalMinutes <= 0 ? 0 : Math.Clamp((int)Math.Round(StudiedMinutes * 100.0 / _data.GoalMinutes), 0, 100);

    private void UpdateDashboard()
    {
        int studied = StudiedMinutes;
        int completed = CompletedTasks;
        StudyMinutesLabel.Text = $"{ToFa(studied)} دقیقه";
        TasksDoneLabel.Text = $"{ToFa(completed)}/{ToFa(_tasks.Count)}";
        SessionsLabel.Text = $"{ToFa(_data.Sessions)} جلسه";
        GoalProgressLabel.Text = $"{ToFa(TargetProgress)}٪";
        TaskSummaryLabel.Text = $"{ToFa(completed)} از {ToFa(_tasks.Count)} کار انجام شده";
        HomeEmptyLabel.Visibility = _tasks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        PlannerEmptyLabel.Visibility = _tasks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        PlannedMinutesLabel.Text = $"{ToFa(_tasks.Sum(t => t.Duration))} دقیقه";
        PlannerDoneLabel.Text = $"{ToFa(completed)} کار";
        PlannerRemainingLabel.Text = $"{ToFa(_tasks.Count - completed)} کار";
        ProgressMinutesLabel.Text = $"{ToFa(studied)} دقیقه";
        ProgressTasksLabel.Text = ToFa(completed);
        ProgressSessionsLabel.Text = ToFa(_data.Sessions);
        FocusSessionsLabel.Text = $"{ToFa(_data.Sessions)} جلسه";
        ProgressBarCaption.Text = $"{ToFa(TargetProgress)}٪ از هدف روزانه";
        GoalProgressBar.Value = TargetProgress;
        if (_data.GoalMinutes > studied) GoalProgressLabel.ToolTip = $"{ToFa(_data.GoalMinutes - studied)} دقیقه تا هدف";
        else GoalProgressLabel.ToolTip = "به هدف امروز رسیدی!";
        SaveCurrentData();
    }

    private void TaskDoneChanged(object sender, RoutedEventArgs e)
    {
        SaveCurrentData();
        UpdateDashboard();
    }

    private void SaveCurrentData()
    {
        if (_currentProfile is null) return;
        try
        {
            _data.Tasks = _tasks.ToList();
            LocalStore.SaveData(_currentProfile.Username, _data);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Keep the app usable in memory if local saving is unavailable.
        }
    }

    private void AddTask_Click(object sender, RoutedEventArgs e)
    {
        TaskSubjectBox.Text = "";
        TaskDurationBox.Text = "45";
        TaskTimeBox.Text = "17:00";
        TaskCategoryCombo.SelectedIndex = 1;
        TaskFormError.Text = "";
        TaskModal.Visibility = Visibility.Visible;
        TaskSubjectBox.Focus();
    }

    private void CloseTaskModal_Click(object sender, RoutedEventArgs e) => TaskModal.Visibility = Visibility.Collapsed;

    private void SaveTask_Click(object sender, RoutedEventArgs e)
    {
        string subject = TaskSubjectBox.Text.Trim();
        if (subject.Length < 2) { TaskFormError.Text = "نام درس رو وارد کن."; return; }
        if (!int.TryParse(ToAsciiDigits(TaskDurationBox.Text.Trim()), out int duration) || duration is < 5 or > 360)
        { TaskFormError.Text = "مدت باید بین ۵ تا ۳۶۰ دقیقه باشه."; return; }
        string time = ToAsciiDigits(TaskTimeBox.Text.Trim());
        if (time.Length > 0 && !Regex.IsMatch(time, @"^(?:[01]\d|2[0-3]):[0-5]\d$"))
        { TaskFormError.Text = "ساعت رو به شکل 17:00 وارد کن یا خالی بذار."; return; }
        string category = (TaskCategoryCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "سایر درس‌ها";
        _tasks.Add(new StudyTask { Subject = subject, Category = category, Duration = duration, Time = time });
        var orderedTasks = _tasks.OrderBy(task => string.IsNullOrWhiteSpace(task.Time) ? "99:99" : task.Time, StringComparer.Ordinal).ToList();
        _tasks.Clear();
        foreach (StudyTask task in orderedTasks) _tasks.Add(task);
        TaskModal.Visibility = Visibility.Collapsed;
        UpdateDashboard();
        ShowToast("درس به برنامه امروز اضافه شد.");
    }

    private void DeleteTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not string taskId) return;
        StudyTask? task = _tasks.FirstOrDefault(item => item.Id == taskId);
        if (task is null) return;

        MessageBoxResult result = MessageBox.Show(
            $"درس «{task.Subject}» از برنامه حذف شود؟",
            "تأیید حذف درس",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (result != MessageBoxResult.Yes) return;

        _tasks.Remove(task);
        UpdateDashboard();
        ShowToast("درس از برنامه حذف شد.");
    }

    private void ResetTasks_Click(object sender, RoutedEventArgs e)
    {
        foreach (StudyTask task in _tasks) task.Done = false;
        UpdateDashboard();
        ShowToast("تیک‌های امروز پاک شد.");
    }

    private void NewMotivation_Click(object sender, RoutedEventArgs e)
    {
        int next;
        do { next = RandomNumberGenerator.GetInt32(_quotes.Count); } while (next == _lastQuoteIndex && _quotes.Count > 1);
        _lastQuoteIndex = next;
        MotivationQuote.Opacity = 0.2;
        MotivationQuote.Text = _quotes[next];
        var animation = new DoubleAnimation(0.2, 1, TimeSpan.FromMilliseconds(330))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        MotivationQuote.BeginAnimation(OpacityProperty, animation);
        MotivationCounter.Text = $"✦ جمله {ToFa(next + 1)} از {ToFa(_quotes.Count)}";
    }

    private void TimerPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || !int.TryParse(button.Tag?.ToString(), out int minutes)) return;
        _isBreak = minutes == 5;
        _timerDurationSeconds = minutes * 60;
        _timerRemainingSeconds = _timerDurationSeconds;
        _timerRunning = false;
        _studyTimer.Stop();
        UpdateTimerUi();
    }

    private void TimerStart_Click(object sender, RoutedEventArgs e)
    {
        if (_timerRemainingSeconds <= 0)
        {
            _timerRemainingSeconds = _timerDurationSeconds;
        }
        _timerRunning = !_timerRunning;
        if (_timerRunning) _studyTimer.Start(); else _studyTimer.Stop();
        UpdateTimerUi();
    }

    private void TimerReset_Click(object sender, RoutedEventArgs e)
    {
        _studyTimer.Stop();
        _timerRunning = false;
        _timerRemainingSeconds = _timerDurationSeconds;
        UpdateTimerUi();
    }

    private void StudyTimer_Tick(object? sender, EventArgs e)
    {
        if (_timerRemainingSeconds > 0) _timerRemainingSeconds--;
        if (_timerRemainingSeconds <= 0)
        {
            _studyTimer.Stop();
            _timerRunning = false;
            if (!_isBreak)
            {
                _data.Sessions++;
                _data.SessionMinutes += Math.Max(1, (int)Math.Round(_timerDurationSeconds / 60.0));
                SaveCurrentData();
                UpdateDashboard();
                ShowToast("جلسه تمرکز کامل شد؛ آفرین! 🎉");
            }
            else ShowToast("استراحت تموم شد؛ آماده‌ای؟ 🌱");
        }
        UpdateTimerUi();
    }

    private void UpdateTimerUi()
    {
        int minutes = Math.Max(0, _timerRemainingSeconds) / 60;
        int seconds = Math.Max(0, _timerRemainingSeconds) % 60;
        TimerText.Text = $"{ToFa(minutes).PadLeft(2, '۰')}:{ToFa(seconds).PadLeft(2, '۰')}";
        TimerKindLabel.Text = _isBreak ? "زمان استراحت" : "زمان تمرکز";
        TimerStatusLabel.Text = _timerRunning ? "در حال اجرا؛ فقط روی قدم بعدی تمرکز کن." : _timerRemainingSeconds == 0 ? "جلسه به پایان رسید؛ خسته نباشی!" : "هر وقت آماده‌ای شروع کن";
        TimerStartButton.Content = _timerRunning ? "Ⅱ توقف" : _timerRemainingSeconds == 0 ? "▶ شروع دوباره" : "▶ شروع جلسه";
    }

    private void DailyGoal_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _currentProfile is null || DailyGoalCombo.SelectedItem is not ComboBoxItem item || !int.TryParse(item.Tag?.ToString(), out int goal)) return;
        _data.GoalMinutes = goal;
        UpdateDashboard();
        ShowToast("هدف روزانه ذخیره شد.");
    }

    private void ShowToast(string message)
    {
        ToastText.Text = message;
        ToastBorder.Visibility = Visibility.Visible;
        _toastTimer.Stop();
        _toastTimer.Start();
    }
}
