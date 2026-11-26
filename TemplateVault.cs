using SourceAFIS;

namespace CrepeBiometricBridge;

internal sealed class TemplateVault
{
    private const double MatchThreshold = 20;
    private const string Filename = "templates.dat";

    public void Save(Guid templateId, int employeeId, FingerprintTemplate template)
    {
        var templates = Read();
        templates.RemoveAll(entry => entry.EmployeeId == employeeId);
        templates.Add(new StoredTemplate(templateId, employeeId, Convert.ToBase64String(template.ToByteArray())));
        EncryptedStore.Write(Filename, templates);
    }

    public StoredTemplate? Find(FingerprintTemplate probe)
    {
        var matcher = new FingerprintMatcher(probe);

        return Read()
            .Select(template => new
            {
                Template = template,
                Score = matcher.Match(new FingerprintTemplate(Convert.FromBase64String(template.Template))),
            })
            .Where(result => result.Score >= MatchThreshold)
            .OrderByDescending(result => result.Score)
            .Select(result => result.Template)
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

internal sealed record StoredTemplate(Guid TemplateId, int EmployeeId, string Template);
