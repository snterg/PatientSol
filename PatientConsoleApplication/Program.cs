using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace PatientConsoleApplication;

class Program
{
    private static readonly HttpClient client = new HttpClient();
    private static readonly Random random = new Random();
    private static string _baseUrl = "http://localhost:5000";

    // Мужские имена
    private static readonly string[] maleFirstNames = { "Иван", "Петр", "Сергей", "Алексей", "Дмитрий", "Андрей", "Михаил", "Николай", "Александр", "Владимир" };
    private static readonly string[] malePatronymics = { "Иванович", "Петрович", "Сергеевич", "Алексеевич", "Дмитриевич", "Андреевич", "Михайлович", "Николаевич" };

    // Женские имена
    private static readonly string[] femaleFirstNames = { "Анна", "Мария", "Елена", "Ольга", "Наталья", "Екатерина", "Татьяна", "Ирина", "Светлана", "Юлия" };
    private static readonly string[] femalePatronymics = { "Ивановна", "Петровна", "Сергеевна", "Алексеевна", "Дмитриевна", "Андреевна", "Михайловна", "Николаевна" };

    // Фамилии (общие)
    private static readonly string[] lastNames = {
        "Иванов", "Петров", "Сидоров", "Смирнов", "Кузнецов", "Попов", "Васильев",
        "Иванова", "Петрова", "Сидорова", "Смирнова", "Кузнецова", "Попова", "Васильева"
    };

    private static readonly string[] genders = { "male", "female", "other", "unknown" };

    private static int _successCount = 0;
    private static int _failCount = 0;
    private static readonly object _lockObject = new object();
    private static bool _apiAvailable = false;

    #region Точка входа

    static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        // Настройка подключения к API
        await SetupApiConnection();

        // Главное меню
        bool exit = false;
        while (!exit)
        {
            // Обновляем статус API перед каждым показом меню
            await UpdateApiStatus();

            ShowMainMenu();
            string choice = Console.ReadLine() ?? "";

            switch (choice)
            {
                case "1":
                    await AutoFillDatabase();
                    break;
                case "2":
                    await FillFromFile();
                    break;
                case "3":
                    await ManualFill();
                    break;
                case "4":
                    await ShowCurrentCount();
                    break;
                case "5":
                    await TestConnection();
                    break;
                case "0":
                    exit = true;
                    Console.WriteLine("Выход из программы...");
                    break;
                default:
                    Console.WriteLine("Неверный выбор. Пожалуйста, выберите пункт из меню.");
                    break;
            }

            if (!exit)
            {
                Console.WriteLine("\nНажмите любую клавишу для продолжения...");
                Console.ReadKey();
            }
        }
    }

    #endregion

    #region UI Methods (Методы интерфейса)

    static void ShowAsciiArt()
    {
        Console.WriteLine(@"______  ___ _____ _____ _____ _   _ _____   _____ _____ _   _  _____  _____ _      _____ ");
        Console.WriteLine(@"| ___ \/ _ \_   _|_   _|  ___| \ | |_   _| /  __ \  _  | \ | |/  ___||  _  | |    |  ___|");
        Console.WriteLine(@"| |_/ / /_\ \| |   | | | |__ |  \| | | |   | /  \/ | | |  \| |\ `--. | | | | |    | |__  ");
        Console.WriteLine(@"|  __/|  _  || |   | | |  __|| . ` | | |   | |   | | | | . ` | `--. \| | | | |    |  __| ");
        Console.WriteLine(@"| |   | | | || |  _| |_| |___| |\  | | |   | \__/\ \_/ / |\  |/\__/ /\ \_/ / |____| |___ ");
        Console.WriteLine(@"\_|   \_| |_/\_/  \___/\____/\_| \_/ \_/    \____/\___/\_| \_/\____/  \___/\_____/\____/ ");
        Console.WriteLine();
        Console.WriteLine(@"                    _               __   _____  _____ ");
        Console.WriteLine(@"                   (_)             /  | |  _  ||  _  |");
        Console.WriteLine(@"__   _____ _ __ ___ _  ___  _ __   `| | | |/' || |/' |");
        Console.WriteLine(@"\ \ / / _ \ '__/ __| |/ _ \| '_ \   | | |  /| ||  /| |");
        Console.WriteLine(@" \ V /  __/ |  \__ \ | (_) | | | | _| |_\ |_/ /\ |_/ /");
        Console.WriteLine(@"  \_/ \___|_|  |___/_|\___/|_| |_| \___(_)___(_)\___/ ");
        Console.WriteLine();
    }

    static void ShowMainMenu()
    {
        Console.Clear();
        ShowAsciiArt();
        Console.WriteLine();
        Console.WriteLine("Текущие настройки:");
        Console.WriteLine($"  API URL: {_baseUrl}");
        Console.WriteLine($"  Статус API: {(_apiAvailable ? "Доступен" : "Недоступен")}");
        Console.WriteLine();
        Console.WriteLine("ГЛАВНОЕ МЕНЮ:");
        Console.WriteLine("-----------------------------------------");
        Console.WriteLine("1. Заполнить базу данных автоматически (до 100 записей)");
        Console.WriteLine("2. Заполнить базу данных из файла (patientList.json)");
        Console.WriteLine("3. Заполнить базу данных вручную");
        Console.WriteLine("4. Показать текущее количество записей");
        Console.WriteLine("5. Проверить подключение к API");
        Console.WriteLine("0. Выход");
        Console.WriteLine("-----------------------------------------");
        Console.Write("Ваш выбор: ");
    }

    static async Task SetupApiConnection()
    {
        ShowAsciiArt();
        Console.Write("Введите URL API (по умолчанию http://localhost:5000): ");
        string? input = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(input))
        {
            _baseUrl = input;
        }
        else
        {
            Console.WriteLine("URL по умолчанию");
        }

        client.BaseAddress = new Uri(_baseUrl);
        client.Timeout = TimeSpan.FromSeconds(5); // Устанавливаем таймаут для быстрого определения недоступности

        _apiAvailable = await TestConnection();
    }

    /// <summary>
    /// Обновляет статус API без вывода сообщений
    /// </summary>
    static async Task UpdateApiStatus()
    {
        try
        {
            var response = await client.GetAsync("/api/patients/info");
            _apiAvailable = response.IsSuccessStatusCode;
        }
        catch
        {
            _apiAvailable = false;
        }
    }

    static async Task<bool> TestConnection()
    {
        try
        {
            Console.Write("Проверка подключения к API... ");
            var response = await client.GetAsync("/api/patients/info");
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("[OK] Успешно");
                return true;
            }
            else
            {
                Console.WriteLine($"[ОШИБКА] {response.StatusCode}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ОШИБКА] {ex.Message}");
            return false;
        }
    }

    static async Task ShowCurrentCount()
    {
        int currentCount = await GetCurrentPatientCount();
        if (currentCount >= 0)
        {
            Console.WriteLine($"\nТекущее количество пациентов в базе: {currentCount}/100");
            Console.WriteLine($"Осталось свободных мест: {100 - currentCount}");
        }
        else
        {
            Console.WriteLine("\nНе удалось получить количество пациентов.");
        }
    }

    #endregion

    #region Core Methods (Основные методы)

    static async Task<int> GetCurrentPatientCount()
    {
        try
        {
            var response = await client.GetAsync("/api/patients/count");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<CountResponse>();
                return result?.totalCount ?? 0;
            }
            return -1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при получении количества: {ex.Message}");
            return -1;
        }
    }

    static async Task AutoFillDatabase()
    {
        // Проверяем доступность API перед началом операции
        if (!_apiAvailable)
        {
            Console.WriteLine("[ОШИБКА] API недоступно. Операция невозможна.");
            return;
        }

        Console.Clear();
        Console.WriteLine("=== АВТОМАТИЧЕСКОЕ ЗАПОЛНЕНИЕ БАЗЫ ДАННЫХ ===");
        Console.WriteLine();

        int currentCount = await GetCurrentPatientCount();

        if (currentCount == -1)
        {
            Console.WriteLine("Не удалось подключиться к API. Проверьте соединение.");
            return;
        }

        Console.WriteLine($"Текущее количество пациентов: {currentCount}/100");

        if (currentCount >= 100)
        {
            Console.WriteLine("\n[ОШИБКА] Достигнут максимальный лимит в 100 пациентов!");
            Console.WriteLine("Невозможно добавить больше записей.");
            return;
        }

        int patientsToGenerate = 100 - currentCount;
        Console.WriteLine($"Будет сгенерировано {patientsToGenerate} новых пациентов.");

        Console.Write("\nЗапустить генерацию? (y/n): ");
        if (Console.ReadLine()?.ToLower() != "y")
        {
            Console.WriteLine("Операция отменена.");
            return;
        }

        // Сброс счетчиков
        _successCount = 0;
        _failCount = 0;

        Console.WriteLine($"\nГенерация {patientsToGenerate} пациентов в 2 потоках...\n");

        var startTime = DateTime.Now;

        // Разбиваем на два потока
        int batch1 = patientsToGenerate / 2;
        int batch2 = patientsToGenerate - batch1;

        var task1 = GenerateBatch(1, batch1);
        var task2 = GenerateBatch(batch1 + 1, patientsToGenerate);

        await Task.WhenAll(task1, task2);

        var elapsed = DateTime.Now - startTime;

        int finalCount = await GetCurrentPatientCount();

        Console.WriteLine("\n=========================================");
        Console.WriteLine($"Генерация завершена за {elapsed.TotalSeconds:F1} сек");
        Console.WriteLine($"Успешно: {_successCount}");
        Console.WriteLine($"Ошибок: {_failCount}");
        Console.WriteLine($"Всего попыток: {_successCount + _failCount}");
        Console.WriteLine($"Финальное количество: {finalCount}/100");
        Console.WriteLine("=========================================");

        if (finalCount >= 100)
        {
            Console.WriteLine("\n[ГОТОВО] Цель достигнута! База данных заполнена до 100 записей.");
        }
    }

    static async Task FillFromFile()
    {
        // Проверяем доступность API перед началом операции
        if (!_apiAvailable)
        {
            Console.WriteLine("[ОШИБКА] API недоступно. Операция невозможна.");
            return;
        }

        Console.Clear();
        Console.WriteLine("=== ЗАПОЛНЕНИЕ ИЗ ФАЙЛА patientList.json ===");
        Console.WriteLine();

        string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "patientList.json");

        if (!File.Exists(filePath))
        {
            Console.WriteLine($"[ОШИБКА] Файл {filePath} не найден!");
            Console.WriteLine("Создайте файл patientList.json в корневой папке приложения.");

            // Создаем пример файла
            CreateExampleFile(filePath);
            return;
        }

        try
        {
            string jsonContent = File.ReadAllText(filePath);
            var patients = JsonSerializer.Deserialize<List<object>>(jsonContent);

            if (patients == null || patients.Count == 0)
            {
                Console.WriteLine("[ОШИБКА] Файл не содержит данных или имеет неверный формат.");
                return;
            }

            Console.WriteLine($"Найдено {patients.Count} записей в файле.");

            int currentCount = await GetCurrentPatientCount();
            if (currentCount == -1)
            {
                Console.WriteLine("Не удалось подключиться к API.");
                return;
            }

            int availableSlots = 100 - currentCount;
            if (availableSlots <= 0)
            {
                Console.WriteLine("[ОШИБКА] База данных уже содержит 100 записей.");
                return;
            }

            int toAdd = Math.Min(patients.Count, availableSlots);
            Console.WriteLine($"Будет добавлено {toAdd} записей (доступно мест: {availableSlots})");

            Console.Write("\nПродолжить? (y/n): ");
            if (Console.ReadLine()?.ToLower() != "y")
            {
                Console.WriteLine("Операция отменена.");
                return;
            }

            _successCount = 0;
            _failCount = 0;

            Console.WriteLine($"\nДобавление пациентов из файла...\n");

            for (int i = 0; i < toAdd; i++)
            {
                var patient = patients[i];

                // Проверка лимита перед каждым запросом
                currentCount = await GetCurrentPatientCount();
                if (currentCount >= 100)
                {
                    Console.WriteLine($"Достигнут лимит в 100 записей. Остановлено на {i} записи.");
                    break;
                }

                try
                {
                    var response = await client.PostAsJsonAsync("/api/patients", patient);

                    if (response.IsSuccessStatusCode)
                    {
                        _successCount++;
                        Console.WriteLine($"[{i + 1}/{toAdd}] [OK] Пациент добавлен");
                    }
                    else
                    {
                        _failCount++;
                        var error = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"[{i + 1}/{toAdd}] [ОШИБКА] {response.StatusCode}");
                    }

                    await Task.Delay(50);
                }
                catch (Exception ex)
                {
                    _failCount++;
                    Console.WriteLine($"[{i + 1}/{toAdd}] [ОШИБКА] {ex.Message}");
                }
            }

            int finalCount = await GetCurrentPatientCount();
            Console.WriteLine($"\n[ГОТОВО] Добавлено: {_successCount}, Ошибок: {_failCount}");
            Console.WriteLine($"Текущее количество записей: {finalCount}/100");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ОШИБКА] Ошибка при чтении файла: {ex.Message}");
        }
    }

    static async Task ManualFill()
    {
        // Проверяем доступность API перед началом операции
        if (!_apiAvailable)
        {
            Console.WriteLine("[ОШИБКА] API недоступно. Операция невозможна.");
            return;
        }

        Console.Clear();
        Console.WriteLine("=== РУЧНОЕ ЗАПОЛНЕНИЕ БАЗЫ ДАННЫХ ===");
        Console.WriteLine("(Через каждые 5 пациентов будет предложено автоматическое заполнение)");
        Console.WriteLine();

        int currentCount = await GetCurrentPatientCount();
        if (currentCount == -1)
        {
            Console.WriteLine("Не удалось подключиться к API.");
            return;
        }

        int added = 0;

        while (currentCount < 100)
        {
            Console.Clear();
            Console.WriteLine($"=== РУЧНОЕ ЗАПОЛНЕНИЕ (добавлено: {added}) ===");
            Console.WriteLine($"Текущее количество записей: {currentCount}/100");
            Console.WriteLine($"Осталось мест: {100 - currentCount}");
            Console.WriteLine();

            // Через каждые 5 добавленных пациентов предлагаем авто-заполнение
            if (added > 0 && added % 5 == 0)
            {
                Console.WriteLine("[ИНФО] Вы добавили 5 пациентов вручную!");
                Console.Write("Хотите заполнить оставшиеся места автоматически? (y/n): ");

                if (Console.ReadLine()?.ToLower() == "y")
                {
                    int remaining = 100 - currentCount;
                    Console.WriteLine($"Будет автоматически добавлено {remaining} пациентов.");

                    _successCount = 0;
                    _failCount = 0;

                    int batch1 = remaining / 2;
                    int batch2 = remaining - batch1;

                    var task1 = GenerateBatch(1, batch1);
                    var task2 = GenerateBatch(batch1 + 1, remaining);

                    await Task.WhenAll(task1, task2);

                    added += _successCount;
                    Console.WriteLine($"\n[ГОТОВО] Автоматически добавлено: {_successCount}");
                    break;
                }
            }

            // Создание нового пациента вручную
            var patient = CreateManualPatient();

            try
            {
                var response = await client.PostAsJsonAsync("/api/patients", patient);

                if (response.IsSuccessStatusCode)
                {
                    added++;
                    Console.WriteLine($"\n[OK] Пациент успешно добавлен!");
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"\n[ОШИБКА] {response.StatusCode}");
                    Console.WriteLine($"Детали: {error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[ОШИБКА] {ex.Message}");
            }

            currentCount = await GetCurrentPatientCount();

            if (currentCount < 100)
            {
                Console.WriteLine("\nНажмите любую клавишу для продолжения или 'q' для выхода...");
                if (Console.ReadKey().KeyChar == 'q')
                    break;
            }
        }

        Console.WriteLine($"\n[ГОТОВО] Ручное заполнение завершено. Всего добавлено: {added}");
    }

    static async Task GenerateBatch(int start, int count)
    {
        if (count <= 0) return;

        for (int i = start; i < start + count; i++)
        {
            try
            {
                var currentCount = await GetCurrentPatientCount();
                if (currentCount >= 100)
                {
                    Console.WriteLine($"[Поток {Thread.CurrentThread.ManagedThreadId}] [СТОП] Достигнут лимит в 100 пациентов");
                    break;
                }

                var patient = GeneratePatient();
                var response = await client.PostAsJsonAsync("/api/patients", patient);

                if (response.IsSuccessStatusCode)
                {
                    Interlocked.Increment(ref _successCount);
                    Console.WriteLine($"[Поток {Thread.CurrentThread.ManagedThreadId}] [OK] Пациент {i} создан (всего: {_successCount + _failCount})");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    if (error.Contains("Maximum patients limit reached"))
                    {
                        Console.WriteLine($"[Поток {Thread.CurrentThread.ManagedThreadId}] [СТОП] {error}");
                        break;
                    }
                    else
                    {
                        Interlocked.Increment(ref _failCount);
                        Console.WriteLine($"[Поток {Thread.CurrentThread.ManagedThreadId}] [ОШИБКА] Пациент {i} ошибка: {response.StatusCode}");
                    }
                }
                else
                {
                    Interlocked.Increment(ref _failCount);
                    Console.WriteLine($"[Поток {Thread.CurrentThread.ManagedThreadId}] [ОШИБКА] Пациент {i} ошибка: {response.StatusCode}");
                }

                await Task.Delay(50);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _failCount);
                Console.WriteLine($"[Поток {Thread.CurrentThread.ManagedThreadId}] [ОШИБКА] Пациент {i} исключение: {ex.Message}");
            }
        }
    }

    #endregion

    #region Generation Methods (Методы генерации данных)

    static object CreateManualPatient()
    {
        Console.WriteLine("\n--- Ввод данных пациента ---");

        Console.Write("Фамилия (family): ");
        string family = Console.ReadLine() ?? "Иванов";

        Console.Write("Имя (given1): ");
        string given1 = Console.ReadLine() ?? "Иван";

        Console.Write("Отчество (given2): ");
        string given2 = Console.ReadLine() ?? "Иванович";

        Console.Write("Пол (male/female/other/unknown): ");
        string gender = Console.ReadLine()?.ToLower() ?? "male";

        Console.Write("Дата рождения (ГГГГ-ММ-ДД): ");
        if (!DateTime.TryParse(Console.ReadLine(), out DateTime birthDate))
        {
            birthDate = GenerateRandomBirthDate();
            Console.WriteLine($"Использована случайная дата: {birthDate:yyyy-MM-dd}");
        }

        Console.Write("Активен (true/false): ");
        if (!bool.TryParse(Console.ReadLine(), out bool active))
        {
            active = random.Next(2) == 1;
            Console.WriteLine($"Использовано значение: {active}");
        }

        return new
        {
            name = new
            {
                id = Guid.NewGuid(),
                use = "official",
                family = family,
                given = new[] { given1, given2 }
            },
            gender = gender,
            birthDate = birthDate.ToString("yyyy-MM-ddTHH:mm:ss"),
            active = active
        };
    }

    static object GeneratePatient()
    {
        lock (_lockObject)
        {
            var gender = genders[random.Next(genders.Length)];

            string firstName;
            string patronymic;
            string lastName;

            if (gender == "male")
            {
                firstName = maleFirstNames[random.Next(maleFirstNames.Length)];
                patronymic = malePatronymics[random.Next(malePatronymics.Length)];
                lastName = lastNames.Where(l => l.EndsWith("ов") || l.EndsWith("ев") || l.EndsWith("ин") || l.EndsWith("ын"))
                                   .OrderBy(x => random.Next()).FirstOrDefault() ?? "Иванов";
            }
            else if (gender == "female")
            {
                firstName = femaleFirstNames[random.Next(femaleFirstNames.Length)];
                patronymic = femalePatronymics[random.Next(femalePatronymics.Length)];
                lastName = lastNames.Where(l => l.EndsWith("ова") || l.EndsWith("ева") || l.EndsWith("ина") || l.EndsWith("ына"))
                                   .OrderBy(x => random.Next()).FirstOrDefault() ?? "Иванова";
            }
            else
            {
                var useMale = random.Next(2) == 0;
                if (useMale)
                {
                    firstName = maleFirstNames[random.Next(maleFirstNames.Length)];
                    patronymic = malePatronymics[random.Next(malePatronymics.Length)];
                    lastName = lastNames.Where(l => l.EndsWith("ов") || l.EndsWith("ев")).OrderBy(x => random.Next()).FirstOrDefault() ?? "Иванов";
                }
                else
                {
                    firstName = femaleFirstNames[random.Next(femaleFirstNames.Length)];
                    patronymic = femalePatronymics[random.Next(femalePatronymics.Length)];
                    lastName = lastNames.Where(l => l.EndsWith("ова") || l.EndsWith("ева")).OrderBy(x => random.Next()).FirstOrDefault() ?? "Иванова";
                }
            }

            return new
            {
                name = new
                {
                    id = Guid.NewGuid(),
                    use = "official",
                    family = lastName,
                    given = new[] { firstName, patronymic }
                },
                gender = gender,
                birthDate = GenerateRandomBirthDate().ToString("yyyy-MM-ddTHH:mm:ss"),
                active = random.Next(2) == 1
            };
        }
    }

    static DateTime GenerateRandomBirthDate()
    {
        lock (_lockObject)
        {
            var start = DateTime.UtcNow.AddYears(-80);
            var range = DateTime.UtcNow.AddYears(-18) - start;
            return start.AddDays(random.NextDouble() * range.TotalDays);
        }
    }

    static void CreateExampleFile(string filePath)
    {
        var examplePatients = new[]
        {
            new
            {
                name = new
                {
                    id = Guid.NewGuid(),
                    use = "official",
                    family = "Иванов",
                    given = new[] { "Иван", "Иванович" }
                },
                gender = "male",
                birthDate = "2024-01-13T18:25:43",
                active = true
            },
            new
            {
                name = new
                {
                    id = Guid.NewGuid(),
                    use = "official",
                    family = "Петрова",
                    given = new[] { "Анна", "Сергеевна" }
                },
                gender = "female",
                birthDate = "2023-05-20T10:30:00",
                active = true
            },
            new
            {
                name = new
                {
                    id = Guid.NewGuid(),
                    use = "official",
                    family = "Сидоров",
                    given = new[] { "Петр", "Алексеевич" }
                },
                gender = "male",
                birthDate = "2022-11-08T15:45:00",
                active = false
            }
        };

        string json = JsonSerializer.Serialize(examplePatients, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json);

        Console.WriteLine($"[ГОТОВО] Создан пример файла: {filePath}");
        Console.WriteLine("Вы можете отредактировать его и добавить своих пациентов.");
    }

    #endregion

    #region Helper Classes (Вспомогательные классы)

    class CountResponse
    {
        public int totalCount { get; set; }
    }

    #endregion
}