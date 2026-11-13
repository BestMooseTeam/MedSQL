namespace MedicalApi.Models;

public class Spravka
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public int SpravkaTypeId { get; set; }

    // Заполняются сервером из каталога видов справок, присланные значения игнорируются.
    // В справке хранится цена, за которую её выдали: если завтра каталог подорожает,
    // уже выписанные справки не подорожают
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public DateOnly IssueDate { get; set; }
    public DateOnly ValidUntil { get; set; }
}
