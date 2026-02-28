using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace PatientGenerator;

class Program
{
    private static readonly HttpClient client = new HttpClient();
    private static readonly Random random = new Random();

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

    static async Task Main(string[] args)
    {
        string baseUrl = "http://localhost:5000";
        client.BaseAddress = new Uri(baseUrl);

        Console.WriteLine("______  ___ _____ _____ _____ _   _ _____   _____ _____ _   _  _____  _____ _      _____ \r\n| ___ \\/ _ \\_   _|_   _|  ___| \\ | |_   _| /  __ \\  _  | \\ | |/  ___||  _  | |    |  ___|\r\n| |_/ / /_\\ \\| |   | | | |__ |  \\| | | |   | /  \\/ | | |  \\| |\\ `--. | | | | |    | |__  \r\n|  __/|  _  || |   | | |  __|| . ` | | |   | |   | | | | . ` | `--. \\| | | | |    |  __| \r\n| |   | | | || |  _| |_| |___| |\\  | | |   | \\__/\\ \\_/ / |\\  |/\\__/ /\\ \\_/ / |____| |___ \r\n\\_|   \\_| |_/\\_/  \\___/\\____/\\_| \\_/ \\_/    \\____/\\___/\\_| \\_/\\____/  \\___/\\_____/\\____/ \r\n                                                                                         \r\n                                                                                         ");
        Console.WriteLine("                    _               __   _____  _____ \r\n                   (_)             /  | |  _  ||  _  |\r\n__   _____ _ __ ___ _  ___  _ __   `| | | |/' || |/' |\r\n\\ \\ / / _ \\ '__/ __| |/ _ \\| '_ \\   | | |  /| ||  /| |\r\n \\ V /  __/ |  \\__ \\ | (_) | | | | _| |_\\ |_/ /\\ |_/ /\r\n  \\_/ \\___|_|  |___/_|\\___/|_| |_| \\___(_)___(_)\\___/ \r\n                                                      \r\n                                                      \r\n");
        // Проверка API через маршрут count
        Console.WriteLine("\nChecking API connection...");
        int currentCount = await GetCurrentPatientCount();

        if (currentCount == -1)
        {
            Console.WriteLine(" Cannot connect to API. Make sure it's running.");
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
            return;
        }

        Console.WriteLine($" API is accessible. Current patients: {currentCount}");

        // Проверка текущего количества записей
        if (currentCount >= 100)
        {
            Console.WriteLine("\n Cannot generate more patients!");
            Console.WriteLine($"Maximum limit of 100 patients has been reached.");
            Console.WriteLine($"Current count: {currentCount}/100");
            Console.WriteLine("\nPress any key to exit...");
            Console.WriteLine("  ___  ___________ _____   _____  _____  _____   ____ \r\n / _ \\|  __ \\  _  \\_   _| / __  \\|  _  |/ __  \\ / ___|\r\n/ /_\\ \\ |  \\/ | | | | |   `' / /'| |/' |`' / /'/ /___ \r\n|  _  | | __| | | | | |     / /  |  /| |  / /  | ___ \\\r\n| | | | |_\\ \\ |/ /  | |   ./ /___\\ |_/ /./ /___| \\_/ |\r\n\\_| |_/\\____/___/   \\_/   \\_____/ \\___/ \\_____/\\_____/\r\n                                                      \r\n                                                      \r\n");
            Console.ReadKey();
            return;
        }

        int patientsToGenerate = 100 - currentCount;
        Console.WriteLine($"\nWill generate {patientsToGenerate} new patients (to reach 100 total)");

        // Разбиваем на два потока
        int batch1 = patientsToGenerate / 2;
        int batch2 = patientsToGenerate - batch1;

        Console.WriteLine($"\nGenerating {patientsToGenerate} patients in 2 threads...\n");

        var startTime = DateTime.Now;

        // Создаем задачи для двух потоков
        var task1 = GenerateBatch(1, batch1);
        var task2 = GenerateBatch(batch1 + 1, patientsToGenerate);

        await Task.WhenAll(task1, task2);

        var elapsed = DateTime.Now - startTime;

        // Финальная проверка количества
        int finalCount = await GetCurrentPatientCount();

        Console.WriteLine("\n===================================");
        Console.WriteLine($"Generation completed in {elapsed.TotalSeconds:F1}s");
        Console.WriteLine($"Successful: {_successCount}");
        Console.WriteLine($"Failed: {_failCount}");
        Console.WriteLine($"Total attempted: {_successCount + _failCount}");
        Console.WriteLine($"Final patient count: {finalCount}/100");

        if (finalCount >= 100)
        {
            Console.WriteLine("\n Target of 100 patients reached!");
        }
        else
        {
            Console.WriteLine($"\n Target not reached. {100 - finalCount} more patients needed.");
        }
        Console.WriteLine("  ___  ___________ _____   _____  _____  _____   ____ \r\n / _ \\|  __ \\  _  \\_   _| / __  \\|  _  |/ __  \\ / ___|\r\n/ /_\\ \\ |  \\/ | | | | |   `' / /'| |/' |`' / /'/ /___ \r\n|  _  | | __| | | | | |     / /  |  /| |  / /  | ___ \\\r\n| | | | |_\\ \\ |/ /  | |   ./ /___\\ |_/ /./ /___| \\_/ |\r\n\\_| |_/\\____/___/   \\_/   \\_____/ \\___/ \\_____/\\_____/\r\n                                                      \r\n                                                      \r\n");

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }

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
            Console.WriteLine($"Error getting count: {ex.Message}");
            return -1;
        }
    }

    static async Task GenerateBatch(int start, int count)
    {
        if (count <= 0) return;

        for (int i = start; i < start + count; i++)
        {
            try
            {
                // Перед каждым запросом проверяем, не достигли ли мы лимита
                var currentCount = await GetCurrentPatientCount();
                if (currentCount >= 100)
                {
                    Console.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}] Stopping: 100 patients limit reached");
                    break;
                }

                var patient = GeneratePatient();
                var response = await client.PostAsJsonAsync("/api/patients", patient);

                if (response.IsSuccessStatusCode)
                {
                    Interlocked.Increment(ref _successCount);
                    Console.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}] Patient {i} created (total: {_successCount + _failCount})");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    if (error.Contains("Maximum patients limit reached"))
                    {
                        Console.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}] {error}");
                        break;
                    }
                    else
                    {
                        Interlocked.Increment(ref _failCount);
                        Console.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}] Patient {i} failed: {response.StatusCode}");
                    }
                }
                else
                {
                    Interlocked.Increment(ref _failCount);
                    Console.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}] Patient {i} failed: {response.StatusCode}");
                }

                await Task.Delay(50);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _failCount);
                Console.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}] Patient {i} error: {ex.Message}");
            }
        }
    }

    static object GeneratePatient()
    {
        lock (_lockObject)
        {
            // Сначала выбираем пол
            var gender = genders[random.Next(genders.Length)];

            string firstName;
            string patronymic;
            string lastName;

            // В зависимости от пола выбираем соответствующие имена
            if (gender == "male")
            {
                firstName = maleFirstNames[random.Next(maleFirstNames.Length)];
                patronymic = malePatronymics[random.Next(malePatronymics.Length)];
                // Для мужчин выбираем фамилию мужского рода
                lastName = lastNames.Where(l => l.EndsWith("ов") || l.EndsWith("ев") || l.EndsWith("ин") || l.EndsWith("ын"))
                                   .OrderBy(x => random.Next()).FirstOrDefault() ?? "Иванов";
            }
            else if (gender == "female")
            {
                firstName = femaleFirstNames[random.Next(femaleFirstNames.Length)];
                patronymic = femalePatronymics[random.Next(femalePatronymics.Length)];
                // Для женщин выбираем фамилию женского рода
                lastName = lastNames.Where(l => l.EndsWith("ова") || l.EndsWith("ева") || l.EndsWith("ина") || l.EndsWith("ына"))
                                   .OrderBy(x => random.Next()).FirstOrDefault() ?? "Иванова";
            }
            else
            {
                // Для other и unknown - случайный выбор
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
            // Генерируем дату рождения от 0 до 80 лет назад
            var start = DateTime.UtcNow.AddYears(-80);
            var range = DateTime.UtcNow.AddYears(-18) - start; // От 18 до 80 лет
            return start.AddDays(random.NextDouble() * range.TotalDays);
        }
    }

    // Класс для десериализации ответа от /api/patients/count
    class CountResponse
    {
        public int totalCount { get; set; }
    }
}