using SourceAFIS;

namespace CrepeBiometricBridge;

internal sealed class TemplateVault
{
    private const double MatchThreshold = 18;
    private const string Filename = "templates.dat";

    public void Save(Guid templateId, EmployeeOption employee, IEnumerable<FingerprintTemplate> templatesToStore)
    {
        var templates = Read();
        templates.RemoveAll(entry => entry.EmployeeId == employee.Id);
        templates.AddRange(templatesToStore.Select(template => new StoredTemplate(templateId, employee.Id, employee.Name, Convert.ToBase64String(template.ToByteArray()))));
        EncryptedStore.Write(Filename, templates);
    }

    public StoredMatch? Find(FingerprintTemplate probe)
    {
        var matcher = new FingerprintMatcher(probe);

        return Read()
            .Select(template => new
            {
                Template = template,
                Score = matcher.Match(new FingerprintTemplate(Convert.FromBase64String(template.Template))),
            })
            .OrderByDescending(result => result.Score)
            .Select(result => new StoredMatch(result.Template, result.Score, result.Score >= MatchThreshold))
            .FirstOrDefault();
    }

    public void Delete(Guid templateId)
    {
        var templates = Read();
        templates.RemoveAll(template => template.TemplateId == templateId);
        EncryptedStore.Write(Filename, templates);
    }

    private static List<StoredTemplate> Read()
    {
        return EncryptedStore.Read(Filename, new List<StoredTemplate>());
    }
}

internal sealed record StoredTemplate(Guid TemplateId, int EmployeeId, string? EmployeeName, string Template);

internal sealed record StoredMatch(StoredTemplate Template, double Score, bool IsRecognized);
