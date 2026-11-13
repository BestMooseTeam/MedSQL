namespace MedicalApi.Models;

public class Patient
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public DateOnly BirthDate { get; set; }
    public string Iin { get; set; } = "";              // ИИН, 12 цифр
    public string Phone { get; set; } = "";
    public string? Address { get; set; }               // необязательное поле
    public string? Email { get; set; }                 // необязательное: у старых пациентов его нет
    public int DistrictNumber { get; set; }            // номер участка
    public bool HasInsurance { get; set; }             // застрахован в ОСМС

    // Метод, а не свойство: в JSON, в Swagger и в базе не попадает
    public int GetAge()
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);
        int age = today.Year - BirthDate.Year;
        if (BirthDate > today.AddYears(-age))
        {
            age--;
        }
        return age;
    }
}
