using MedicalApi.Data;
using MedicalApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MedicalApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SpravkiController : ControllerBase
{
    private readonly MedicalDbContext _db;

    public SpravkiController(MedicalDbContext db)
    {
        _db = db;
    }

    // GET /api/spravki?patientId=1&minPrice=200&sort=price&descending=true
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<string>(StatusCodes.Status400BadRequest, "text/plain")]
    public async Task<ActionResult<List<Spravka>>> GetAll(int? patientId, decimal? minPrice,
        string? sort, bool descending = false)
    {
        IQueryable<Spravka> query = _db.Spravki;

        if (patientId != null)
        {
            query = query.Where(s => s.PatientId == patientId);
        }
        if (minPrice != null)
        {
            query = query.Where(s => s.Price >= minPrice);
        }

        if (sort == "price")
        {
            query = descending ? query.OrderByDescending(s => s.Price) : query.OrderBy(s => s.Price);
        }
        else if (sort == "date")
        {
            query = descending ? query.OrderByDescending(s => s.IssueDate) : query.OrderBy(s => s.IssueDate);
        }
        else if (sort != null)
        {
            return BadRequest("Сортировать можно только по price (цена) или date (дата выдачи)");
        }

        return await query.ToListAsync();
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Spravka>> GetById(int id)
    {
        Spravka? spravka = await _db.Spravki.FirstOrDefaultAsync(s => s.Id == id);
        if (spravka == null)
        {
            return NotFound();
        }
        return spravka;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<string>(StatusCodes.Status400BadRequest, "text/plain")]
    public async Task<ActionResult<Spravka>> Create(Spravka spravka)
    {
        string? error = await ValidateAsync(spravka);
        if (error != null)
        {
            return BadRequest(error);
        }

        spravka.Id = 0;
        spravka.IssueDate = DateOnly.FromDateTime(DateTime.Today);
        await FillFromCatalogAsync(spravka);

        _db.Spravki.Add(spravka);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = spravka.Id }, spravka);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<string>(StatusCodes.Status400BadRequest, "text/plain")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Spravka>> Update(int id, Spravka spravka)
    {
        Spravka? existing = await _db.Spravki.FirstOrDefaultAsync(s => s.Id == id);
        if (existing == null)
        {
            return NotFound();
        }

        string? error = await ValidateAsync(spravka);
        if (error != null)
        {
            return BadRequest(error);
        }

        // Дата выдачи остаётся прежней, название, цена и срок действия — из каталога
        existing.PatientId = spravka.PatientId;
        existing.SpravkaTypeId = spravka.SpravkaTypeId;
        await FillFromCatalogAsync(existing);
        await _db.SaveChangesAsync();
        return Ok(existing);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(int id)
    {
        Spravka? spravka = await _db.Spravki.FirstOrDefaultAsync(s => s.Id == id);
        if (spravka == null)
        {
            return NotFound();
        }

        _db.Spravki.Remove(spravka);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Название, цену и срок действия берём из каталога, а не из тела запроса.
    // Вызывать только после ValidateAsync: вид справки должен существовать
    private async Task FillFromCatalogAsync(Spravka spravka)
    {
        SpravkaType type = await _db.SpravkaTypes.FirstAsync(t => t.Id == spravka.SpravkaTypeId);
        spravka.Name = type.Name;
        spravka.Price = type.Price;
        spravka.ValidUntil = spravka.IssueDate.AddDays(type.ValidDays);
    }

    private async Task<string?> ValidateAsync(Spravka spravka)
    {
        Patient? patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == spravka.PatientId);
        if (patient == null)
        {
            return $"Пациент с Id {spravka.PatientId} не найден. Выберите пациента из картотеки";
        }

        SpravkaType? type = await _db.SpravkaTypes.FirstOrDefaultAsync(t => t.Id == spravka.SpravkaTypeId);
        if (type == null)
        {
            return $"Вид справки с Id {spravka.SpravkaTypeId} не найден. Выберите вид из каталога /api/SpravkaTypes";
        }

        int age = patient.GetAge();
        if (age < type.MinAge || age > type.MaxAge)
        {
            return $"Справка «{type.Name}» выдаётся в возрасте от {type.MinAge} до {type.MaxAge} лет, " +
                $"а пациенту {patient.Name} сейчас {age}";
        }

        return null;
    }
}
