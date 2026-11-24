namespace InternshipCoach.Application.Sources.Cv;

public sealed record UploadCvCommand(string FileName, byte[] Content);
