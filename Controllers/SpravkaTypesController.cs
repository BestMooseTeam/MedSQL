using MedicalApi.Data;
using MedicalApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MedicalApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SpravkaTypesController : ControllerBase
{
    private readonly MedicalDbContext _db;

    public SpravkaTypesController(MedicalDbContext db)
    {
        _db = db;
    }

    // Каталог хранится в базе и после перезапуска сервера не пропадает
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<List<SpravkaType>> GetAll()
    {
        return await _db.SpravkaTypes.OrderBy(t => t.Name).ToListAsync();
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SpravkaType>> GetById(int id)
    {
        SpravkaType? type = await _db.SpravkaTypes.FirstOrDefaultAsync(t => t.Id == id);
        if (type == null)
        {
            return NotFound();
        }
        return type;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<string>(StatusCodes.Status400BadRequest, "text/plain")]
    public async Task<ActionResult<SpravkaType>> Create(SpravkaType type)
    {
        string? error = await ValidateAsync(type, 0);
        if (error != null)
        {
            return BadRequest(error);
        }

        // Номер выдаёт база, присланный в теле Id игнорируем
        type.Id = 0;

        _db.SpravkaTypes.Add(type);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = type.Id }, type);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<string>(StatusCodes.Status400BadRequest, "text/plain")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SpravkaType>> Update(int id, SpravkaType type)
    {
        SpravkaType? existing = await _db.SpravkaTypes.FirstOrDefaultAsync(t => t.Id == id);
        if (existing == null)
        {
            return NotFound();
        }

        string? error = await ValidateAsync(type, id);
        if (error != null)
        {
            return BadRequest(error);
        }

        existing.Name = type.Name;
        existing.Price = type.Price;
        existing.ValidDays = type.ValidDays;
        existing.NeedsDoctorExam = type.NeedsDoctorExam;
        existing.MinAge = type.MinAge;
        existing.MaxAge = type.MaxAge;
        await _db.SaveChangesAsync();
        return Ok(existing);
    }

    // Уже выписанные справки хранят своё название и цену, но ссылаются на вид внешним ключом.
    // Поэтому вид, по которому уже выписывали справки, удалить нельзя: это сломало бы историю
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<string>(StatusCodes.Status400BadRequest, "text/plain")]
    public async Task<ActionResult> Delete(int id)
    {
        SpravkaType? type = await _db.SpravkaTypes.FirstOrDefaultAsync(t => t.Id == id);
        if (type == null)
        {
            return NotFound();
        }

        int usedCount = await _db.Spravki.CountAsync(s => s.SpravkaTypeId == id);
        if (usedCount > 0)
        {
            return BadRequest($"Нельзя удалить вид справки: по нему уже выписано справок {usedCount}. " +
                "Если вид больше не нужен, поднимите ему цену или переименуйте");
        }

        _db.SpravkaTypes.Remove(type);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<string?> ValidateAsync(SpravkaType type, int id)
    {
        if (string.IsNullOrWhiteSpace(type.Name))
        {
            return "Укажите название вида справки";
        }
        if (type.Name.Length > 100)
        {
            return "Название не должно быть длиннее 100 символов";
        }
        bool nameTaken = await _db.SpravkaTypes.AnyAsync(t => t.Id != id &&
            t.Name.ToLower() == type.Name.Trim().ToLower());
        if (nameTaken)
        {
            return $"Вид справки «{type.Name.Trim()}» уже есть в каталоге";
        }

        if (type.Price <= 0)
        {
            return "Цена должна быть больше 0";
        }
        if (type.Price > 1_000_000)
        {
            return "Цена не может быть больше 1 000 000 тенге. Проверьте, нет ли лишних нулей";
        }

        if (type.ValidDays < 1 || type.ValidDays > 3650)
        {
            return "Срок действия должен быть от 1 до 3650 дней";
        }

        if (type.MinAge < 0 || type.MinAge > 120)
        {
            return "Минимальный возраст должен быть от 0 до 120 лет";
        }
        if (type.MaxAge < type.MinAge || type.MaxAge > 120)
        {
            return $"Максимальный возраст должен быть от {type.MinAge} (минимального) до 120 лет";
        }

        return null;
    }
}
