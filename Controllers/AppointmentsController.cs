using MedicalApi.Data;
using MedicalApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MedicalApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AppointmentsController : ControllerBase
{
    private readonly MedicalDbContext _db;

    public AppointmentsController(MedicalDbContext db)
    {
        _db = db;
    }

    // GET /api/appointments?patientId=1&date=2026-11-10
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<List<Appointment>> GetAll(int? patientId, DateOnly? date)
    {
        IQueryable<Appointment> query = _db.Appointments;

        if (patientId != null)
        {
            query = query.Where(a => a.PatientId == patientId);
        }
        if (date != null)
        {
            query = query.Where(a => DateOnly.FromDateTime(a.Time) == date);
        }

        return await query.OrderBy(a => a.Time).ToListAsync();
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Appointment>> GetById(int id)
    {
        Appointment? appointment = await _db.Appointments.FirstOrDefaultAsync(a => a.Id == id);
        if (appointment == null)
        {
            return NotFound();
        }
        return appointment;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<string>(StatusCodes.Status400BadRequest, "text/plain")]
    public async Task<ActionResult<Appointment>> Create(Appointment appointment)
    {
        string? error = await ValidateAsync(appointment, null);
        if (error != null)
        {
            return BadRequest(error);
        }

        appointment.Id = 0;

        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = appointment.Id }, appointment);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<string>(StatusCodes.Status400BadRequest, "text/plain")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Appointment>> Update(int id, Appointment appointment)
    {
        Appointment? existing = await _db.Appointments.FirstOrDefaultAsync(a => a.Id == id);
        if (existing == null)
        {
            return NotFound();
        }

        string? error = await ValidateAsync(appointment, existing);
        if (error != null)
        {
            return BadRequest(error);
        }

        existing.PatientId = appointment.PatientId;
        existing.DoctorName = appointment.DoctorName;
        existing.Specialty = appointment.Specialty;
        existing.Time = appointment.Time;
        existing.Room = appointment.Room;
        existing.Complaint = appointment.Complaint;
        await _db.SaveChangesAsync();
        return Ok(existing);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(int id)
    {
        Appointment? appointment = await _db.Appointments.FirstOrDefaultAsync(a => a.Id == id);
        if (appointment == null)
        {
            return NotFound();
        }

        _db.Appointments.Remove(appointment);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // existing — редактируемая запись, при создании null
    private async Task<string?> ValidateAsync(Appointment appointment, Appointment? existing)
    {
        bool patientExists = await _db.Patients.AnyAsync(p => p.Id == appointment.PatientId);
        if (!patientExists)
        {
            return $"Пациент с Id {appointment.PatientId} не найден. Выберите пациента из картотеки";
        }

        if (string.IsNullOrWhiteSpace(appointment.DoctorName))
        {
            return "Укажите ФИО врача";
        }
        if (appointment.DoctorName.Length > 100)
        {
            return "ФИО врача не должно быть длиннее 100 символов";
        }
        if (string.IsNullOrWhiteSpace(appointment.Specialty))
        {
            return "Укажите специальность врача, например Терапевт";
        }
        if (appointment.Specialty.Length > 50)
        {
            return "Специальность не должна быть длиннее 50 символов";
        }

        // Время сравниваем как местное, даже если клиент прислал его с Z (UTC)
        DateTime time = appointment.Time.Kind == DateTimeKind.Utc ? appointment.Time.ToLocalTime() : appointment.Time;
        appointment.Time = DateTime.SpecifyKind(time, DateTimeKind.Unspecified);

        if (appointment.Time == default)
        {
            return "Укажите время приёма в формате 2026-11-10T09:30";
        }
        // Прошедшее время запрещаем, только если его меняют: старую запись можно поправить, не трогая время
        bool timeChanged = existing == null || existing.Time != appointment.Time;
        if (timeChanged && appointment.Time <= DateTime.Now)
        {
            return "Нельзя записать на прошедшее время";
        }
        if (appointment.Time.DayOfWeek == DayOfWeek.Sunday)
        {
            return "В воскресенье поликлиника не работает";
        }
        if (appointment.Time.Hour < 8 || appointment.Time.Hour >= 20)
        {
            return "Приём идёт с 08:00 до 20:00, последняя запись в 19:45";
        }
        if (appointment.Time.TimeOfDay.Ticks % TimeSpan.FromMinutes(15).Ticks != 0)
        {
            return "Запись идёт каждые 15 минут: выберите время вида 09:00, 09:15, 09:30 или 09:45";
        }

        if (appointment.Room < 1 || appointment.Room > 999)
        {
            return "Номер кабинета должен быть от 1 до 999";
        }

        int id = existing?.Id ?? 0;
        Appointment? doctorBusy = await _db.Appointments.FirstOrDefaultAsync(a => a.Id != id && a.Time == appointment.Time &&
            a.DoctorName.ToLower() == appointment.DoctorName.Trim().ToLower());
        if (doctorBusy != null)
        {
            return $"У врача {doctorBusy.DoctorName} на {appointment.Time:dd.MM.yyyy HH:mm} уже есть запись. Выберите другое время";
        }
        bool patientBusy = await _db.Appointments.AnyAsync(a => a.Id != id && a.Time == appointment.Time &&
            a.PatientId == appointment.PatientId);
        if (patientBusy)
        {
            return $"Пациент уже записан на {appointment.Time:dd.MM.yyyy HH:mm} к другому врачу";
        }

        return null;
    }
}
