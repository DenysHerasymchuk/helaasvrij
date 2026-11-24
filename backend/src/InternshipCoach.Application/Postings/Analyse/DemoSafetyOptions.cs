using InternshipCoach.Domain.Postings;

namespace InternshipCoach.Application.Postings.Analyse;

/// <summary>
/// Demo only (the Api turns it on in Development): when re-analysing a seeded demo posting that already has a stored
/// analysis, a failed or slow model call keeps that stored analysis instead of failing on stage. Never used for
/// non-demo postings, and never invents a result: without a stored analysis the error goes through as usual.
/// </summary>
public sealed record DemoSafetyOptions(bool Enabled = false, int TimeoutSeconds = 20);

/// <summary>FromDemoCache is true when the stored analysis of a demo posting was returned because the model failed or timed out.</summary>
public sealed record AnalysePostingResult(Posting Posting, bool FromDemoCache);
