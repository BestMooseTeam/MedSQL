using MedicalApi.Data;
using MedicalApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MedicalApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PatientsController : ControllerBase
{
    private readonly MedicalDbContext _db;

    public PatientsController(MedicalDbContext db)
    {
        _db = db;
    }

    // GET /api/patients
    // GET /api/patients?name=ай — поиск по части имени без учёта регистра
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<List<Patient>> GetAll(string? name)
    {
        IQueryable<Patient> query = _db.Patients;

        if (!string.IsNullOrWhiteSpace(name))
        {
            // ILike: в отличие от Contains, не зависит от регистра — «ай» найдёт и Айбека.
            // % и _ в ILIKE значат «любые символы», поэтому из того, что ввёл человек,
            // их надо экранировать: иначе поиск «50%» найдёт вообще всех
            string pattern = name.Trim()
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");
            query = query.Where(p => EF.Functions.ILike(p.Name, $"%{pattern}%", "\\"));
        }

        return await query.OrderBy(p => p.Name).ToListAsync();
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Patient>> GetById(int id)
    {
        Patient? patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == id);
        if (patient == null)
        {
            return NotFound();
        }
        return patient;
    }

    [HttpGet("{id}/spravki")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<Spravka>>> GetSpravki(int id)
    {
        bool exists = await _db.Patients.AnyAsync(p => p.Id == id);
        if (!exists)
        {
            return NotFound();
        }
        return await _db.Spravki.Where(s => s.PatientId == id).ToListAsync();
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<string>(StatusCodes.Status400BadRequest, "text/plain")]
    public async Task<ActionResult<Patient>> Create(Patient patient)
    {
        string? error = await ValidateAsync(patient, 0);
        if (error != null)
        {
            return BadRequest(error);
        }

        // Id из тела запроса игнорируем: номер выдаёт база.
        // Иначе присланный "id": 1 упадёт на PK_Patients, а "id": 50 сломает нумерацию позже
        patient.Id = 0;

        _db.Patients.Add(patient);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = patient.Id }, patient);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<string>(StatusCodes.Status400BadRequest, "text/plain")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Patient>> Update(int id, Patient patient)
    {
        Patient? existing = await _db.Patients.FirstOrDefaultAsync(p => p.Id == id);
        if (existing == null)
        {
            return NotFound();
        }

        string? error = await ValidateAsync(patient, id);
        if (error != null)
        {
            return BadRequest(error);
        }

        existing.Name = patient.Name;
        existing.BirthDate = patient.BirthDate;
        existing.Iin = patient.Iin;
        existing.Phone = patient.Phone;
        existing.Address = patient.Address;
        existing.Email = patient.Email;
        existing.DistrictNumber = patient.DistrictNumber;
        existing.HasInsurance = patient.HasInsurance;

        // Если данные не изменились, EF Core не отправит UPDATE вовсе
        await _db.SaveChangesAsync();
        return Ok(existing);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<string>(StatusCodes.Status400BadRequest, "text/plain")]
    public async Task<ActionResult> Delete(int id)
    {
        Patient? patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == id);
        if (patient == null)
        {
            return NotFound();
        }

        // Справки и записи на приём — медицинская история, молча её не удаляем.
        // Без этой проверки база вернёт ошибку внешнего ключа и клиент получит 500
        int spravkiCount = await _db.Spravki.CountAsync(s => s.PatientId == id);
        int appointmentsCount = await _db.Appointments.CountAsync(a => a.PatientId == id);
        if (spravkiCount > 0 || appointmentsCount > 0)
        {
            return BadRequest($"Нельзя удалить пациента: у него справок {spravkiCount}, записей на приём {appointmentsCount}. " +
                "Сначала удалите их");
        }

        _db.Patients.Remove(patient);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // id — номер редактируемого пациента, чтобы ИИН не считался дублем самого себя. При создании 0
    private async Task<string?> ValidateAsync(Patient patient, int id)
    {
        if (string.IsNullOrWhiteSpace(patient.Name))
        {
            return "Укажите ФИО пациента";
        }
        if (patient.Name.Length > 100)
        {
            return "ФИО не должно быть длиннее 100 символов";
        }

        if (patient.BirthDate == default)
        {
            return "Укажите дату рождения в формате 2000-05-15";
        }
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);
        if (patient.BirthDate > today)
        {
            return "Дата рождения не может быть в будущем";
        }
        if (patient.BirthDate < today.AddYears(-120))
        {
            return "Проверьте дату рождения: пациенту получается больше 120 лет";
        }

        if (string.IsNullOrWhiteSpace(patient.Iin))
        {
            return "Укажите ИИН пациента";
        }
        if (patient.Iin.Length != 12 || !patient.Iin.All(char.IsAsciiDigit))
        {
            return "ИИН должен состоять ровно из 12 цифр, без пробелов";
        }
        string birthDigits = patient.BirthDate.ToString("yyMMdd");
        if (!patient.Iin.StartsWith(birthDigits))
        {
            return $"Первые 6 цифр ИИН — дата рождения в виде ГГММДД. Для этой даты ИИН должен начинаться с {birthDigits}";
        }
        Patient? sameIin = await _db.Patients.FirstOrDefaultAsync(p => p.Iin == patient.Iin && p.Id != id);
        if (sameIin != null)
        {
            return $"Пациент с ИИН {patient.Iin} уже есть в картотеке: {sameIin.Name}, Id {sameIin.Id}";
        }

        if (string.IsNullOrWhiteSpace(patient.Phone))
        {
            return "Укажите телефон";
        }
        if (!patient.Phone.All(c => char.IsAsciiDigit(c) || " +-()".Contains(c)))
        {
            return "Телефон может содержать только цифры, пробелы и символы + - ( )";
        }
        int phoneDigits = patient.Phone.Count(char.IsAsciiDigit);
        if (phoneDigits < 10 || phoneDigits > 15)
        {
            return "В номере телефона должно быть от 10 до 15 цифр, например +7 701 123 45 67";
        }

        if (patient.Address != null && patient.Address.Length > 200)
        {
            return "Адрес не должен быть длиннее 200 символов";
        }

        // Email необязателен: у старых пациентов его нет. Но если указан, должен быть похож на адрес
        if (!string.IsNullOrWhiteSpace(patient.Email))
        {
            patient.Email = patient.Email.Trim();
            if (patient.Email.Length > 100)
            {
                return "Email не должен быть длиннее 100 символов";
            }
            int at = patient.Email.IndexOf('@');
            if (at <= 0 || at == patient.Email.Length - 1 || !patient.Email.Substring(at).Contains('.'))
            {
                return "Email указан неверно: нужен вид name@example.kz";
            }
        }

        if (patient.DistrictNumber < 1 || patient.DistrictNumber > 100)
        {
            return "Номер участка должен быть от 1 до 100";
        }

        return null;
    }
}
