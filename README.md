# Patient Sol

## 📋 Описание проекта

**Patient API** - это RESTful веб-сервис для управления данными пациентов (рожденные в роддоме дети). Проект разработан на ASP.NET Core 6 с использованием Entity Framework Core и запускается в Docker-контейнерах.

### Основные возможности:
- ✅ CRUD операции для сущности Patient
- ✅ Поиск пациентов по дате рождения с поддержкой FHIR-формата
- ✅ Валидация данных по справочникам
- ✅ Консольный генератор для создания 100 тестовых пациентов
- ✅ Docker-контейнеризация (API + SQL Server)
- ✅ Swagger документация
- ✅ Postman коллекция для тестирования

---

## 🏗️ Структура проекта

Проект состоит из двух основных частей:

### 1. **PatientAPI** - Веб-сервис

```
PatientAPI/
├── Controllers/
│   └── PatientsController.cs      # REST API контроллер
├── Models/
│   ├── Patient.cs                  # Модель пациента
│   └── PatientInfo.cs              # Модель информации о пациенте
├── Data/
│   └── AppDbContext.cs             # Контекст базы данных
├── Program.cs                       # Точка входа и настройка сервисов
├── appsettings.json                 # Конфигурация приложения
├── Dockerfile                        # Docker образ для API
└── docker-compose.yml                # Оркестрация контейнеров
```

**Назначение**: Веб-сервер, обрабатывающий HTTP запросы и взаимодействующий с базой данных.

### 2. **PatientConsoleWithoutDocker** - Консольный генератор

```
PatientConsoleWithoutDocker/
├── Program.cs                         # Генератор тестовых данных
└── PatientConsoleWithoutDocker.csproj # Конфигурация проекта
```

**Назначение**: Утилита для автоматической генерации 100 тестовых пациентов через API.

---

## 🗃️ Структура базы данных

### Модель Patient
```sql
CREATE TABLE Patients (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    PatientInfoId UNIQUEIDENTIFIER NOT NULL,
    Gender NVARCHAR(20),
    BirthDate DATETIME2 NOT NULL,
    Active BIT NOT NULL,
    FOREIGN KEY (PatientInfoId) REFERENCES PatientInfos(Id) ON DELETE CASCADE
)
```

### Модель PatientInfo
```sql
CREATE TABLE PatientInfos (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    Use NVARCHAR(50),
    Family NVARCHAR(100) NOT NULL,
    Given NVARCHAR(MAX) -- хранится как JSON
)
```

### Связи:
- **One-to-One**: Patient ↔ PatientInfo (каждый пациент имеет одну запись с именем)

---

## 📚 API Методы (PatientAPI)

### Базовые CRUD операции

| Метод | Endpoint | Описание |
|-------|----------|----------|
| GET | `/api/patients` | Получение всех пациентов с фильтрацией |
| GET | `/api/patients/{id}` | Получение пациента по ID |
| POST | `/api/patients` | Создание нового пациента |
| PUT | `/api/patients/{id}` | Полное обновление пациента |
| PATCH | `/api/patients/{id}` | Частичное обновление (переключение active) |
| DELETE | `/api/patients/{id}` | Удаление пациента |

### Дополнительные методы

| Метод | Endpoint | Описание |
|-------|----------|----------|
| GET | `/api/patients/count` | Получение количества пациентов |
| GET | `/api/patients/info` | Получение справочников и версии API |

### Параметры фильтрации для GET /api/patients

| Параметр | Тип | Описание | Пример |
|----------|-----|----------|---------|
| family | string | Фильтр по фамилии | `?family=Иванов` |
| given | string | Фильтр по имени | `?given=Иван` |
| gender | string | Фильтр по полу | `?gender=male` |
| active | bool | Фильтр по активности | `?active=true` |
| birthDate | string | FHIR-формат даты | `?birthDate=gt2024-01-01` |
| limit | int | Пагинация (1-1000) | `?limit=50` |
| offset | int | Смещение | `?offset=100` |

### FHIR-форматы для поиска по дате рождения

| Префикс | Значение | Пример |
|---------|----------|--------|
| eq | равно | `?birthDate=eq2024-01-13` |
| ne | не равно | `?birthDate=ne2024-01-13` |
| lt | меньше | `?birthDate=lt2024-01-01` |
| gt | больше | `?birthDate=gt2024-12-31` |
| le | меньше или равно | `?birthDate=le2024-06-30` |
| ge | больше или равно | `?birthDate=ge2024-06-30` |
| sa | начинается после | `?birthDate=sa2024-01-01` |
| eb | заканчивается до | `?birthDate=eb2024-12-31` |
| ap | примерно | `?birthDate=ap2024-01-13` |

---

## 🖥️ PatientConsoleWithoutDocker - Консольный генератор

### Назначение
Консольное приложение для автоматического заполнения базы данных тестовыми пациентами.

### Функциональность
- Генерирует 100 уникальных пациентов (если в базе меньше 100)
- Использует реалистичные данные (имена, фамилии, отчества с учетом пола)
- Работает в два потока для ускорения генерации
- Проверяет текущее количество пациентов перед добавлением
- Предотвращает превышение лимита в 100 пациентов

### Генерируемые данные
- **Мужские имена**: Иван, Петр, Сергей, Алексей, Дмитрий, Андрей, Михаил, Николай, Александр, Владимир
- **Женские имена**: Анна, Мария, Елена, Ольга, Наталья, Екатерина, Татьяна, Ирина, Светлана, Юлия
- **Фамилии**: Иванов/Иванова, Петров/Петрова, Сидоров/Сидорова и др.
- **Возраст**: от 18 до 80 лет
- **Пол**: male, female, other, unknown
- **Активность**: случайный выбор true/false

### Запуск генератора
```bash
cd PatientConsoleWithoutDocker
dotnet run
```

---

## 🔧 Запуск проектов

### Запуск PatientAPI (веб-сервис)

#### Вариант 1: Через Docker Compose (рекомендуемый)
```bash
cd PatientAPI
docker-compose up --build
```

#### Вариант 2: Локальный запуск
```bash
cd PatientAPI
dotnet restore
dotnet ef database update
dotnet run
```

### Запуск PatientConsoleWithoutDocker (генератор данных)
```bash
cd PatientConsoleWithoutDocker
dotnet run
```

---

## 🔌 Доступ к сервисам

| Сервис | URL | Порт |
|--------|-----|------|
| PatientAPI | http://localhost:5000 | 5000 |
| Swagger UI | http://localhost:5000/swagger | 5000 |
| SQL Server | localhost,1433 | 1433 |

---

## 📮 Postman коллекция

В проекте included Postman коллекция `PatientCollection.postman_collection.json` со всеми методами API:

- **GetAll** - получение всех пациентов
- **GetById** - получение по ID
- **Add** - добавление пациента
- **Update** - обновление пациента
- **Delete** - удаление пациента
- **SearchBirthDate_eq** - поиск по дате (равно)
- **SearchBirthDate_ne** - поиск по дате (не равно)
- **SearchBirthDate_lt** - поиск по дате (меньше)
- **SearchBirthDate_gt** - поиск по дате (больше)
- **SearchBirthDate_le** - поиск по дате (меньше или равно)
- **SearchBirthDate_ge** - поиск по дате (больше или равно)
- **SearchBirthDate_sa** - поиск по дате (начинается после)
- **SearchBirthDate_eb** - поиск по дате (заканчивается до)
- **SearchBirthDate_ap** - поиск по дате (примерно)
- **GetValidValues** - получение справочников
- **GetCount** - получение количества записей

---

## 📊 Справочники

### Gender
```json
["male", "female", "other", "unknown"]
```

### Active
```json
[true, false]
```

---

## 📝 Пример JSON для создания пациента

```json
{
  "name": {
    "id": "d8ff176f-bd0a-4b8e-b329-871952e32e1f",
    "use": "official",
    "family": "Иванов",
    "given": ["Иван", "Иванович"]
  },
  "gender": "male",
  "birthDate": "2024-01-13T18:25:43",
  "active": true
}
```

---

## 🛡️ Валидация данных

API обеспечивает многоуровневую валидацию:

1. **Обязательные поля**: name.family, birthDate
2. **Справочники**: gender, active
3. **Длина полей**: защита от DoS-атак
4. **Формат данных**: FHIR для дат, regex для имен
5. **Логические проверки**: дата не в будущем, возраст не более 150 лет

---

## 🐳 Docker конфигурация (PatientAPI)

### Состав контейнеров:
- **db**: SQL Server 2022
- **patientapi**: ASP.NET Core Web API

### Сеть:
- **patient_network**: bridge network для связи контейнеров

### Тома:
- **sql_data**: персистентное хранение данных БД

---

## 🧪 Тестирование

### Swagger
Откройте http://localhost:5000/swagger для интерактивного тестирования API

### Postman
Импортируйте `PatientCollection.postman_collection.json` в Postman

### Консольный генератор
```bash
cd PatientConsoleWithoutDocker
dotnet run
```
Сгенерирует 100 тестовых пациентов (если их меньше 100)

---

## 📄 Лицензия

MIT

---

## 👥 Автор

**Герасимов Вячеслав Александрович**

