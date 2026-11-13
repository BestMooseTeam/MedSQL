using MedicalApi.Data;
using MedicalApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MedicalApi.Controllers;

// Статистика для главврача: сколько справок выписано и на какую сумму
[ApiController]
[Route("api/[controller]")]
[Route("api/stats")]
public class StatisticsController : ControllerBase
{
    private readonly MedicalDbContext _db;

    public StatisticsController(MedicalDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<Statistics> Get()
    {
        // Считает база, а не C#: COUNT и SUM уходят в PostgreSQL
        List<SpravkaTypeStatistics> byType = await _db.Spravki
            .GroupBy(s => s.Name)
            .Select(g => new SpravkaTypeStatistics
            {
                Name = g.Key,
                Count = g.Count(),
                Total = g.Sum(s => s.Price),
            })
            .OrderByDescending(t => t.Total)
            .ToListAsync();

        return new Statistics
        {
            PatientsCount = await _db.Patients.CountAsync(),
            SpravkiCount = await _db.Spravki.CountAsync(),
            SpravkiTotal = await _db.Spravki.SumAsync(s => (decimal?)s.Price) ?? 0,
            AppointmentsCount = await _db.Appointments.CountAsync(),
            ByType = byType,
        };
    }
}
