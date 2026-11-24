using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Application.Interview;
using InternshipCoach.Contracts.Interview;
using InternshipCoach.Domain.Interview;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternshipCoach.Api.Controllers;

[Route("api/interview")]
[ApiController]
[Authorize]
public sealed class InterviewController : ControllerBase
{
    [HttpGet("{postingId:guid}/questions")]
    public Task<IActionResult> Questions(Guid postingId, [FromServices] GetInterviewHandler handler, CancellationToken ct) =>
        Load(postingId, handler, regenerate: false, ct);

    /// <summary>Replaces the posting's own questions (and the answers to them) with a new set. Costs one or two model calls.</summary>
    [HttpPost("{postingId:guid}/questions/regenerate")]
    [Consumes("application/json")]
    public Task<IActionResult> Regenerate(Guid postingId, [FromServices] GetInterviewHandler handler, CancellationToken ct) =>
        Load(postingId, handler, regenerate: true, ct);

    private async Task<IActionResult> Load(Guid postingId, GetInterviewHandler handler, bool regenerate, CancellationToken ct)
    {
        try
        {
            return await handler.HandleAsync(postingId, ct, regenerate) is { } view
                ? Ok(ToResponse(view))
                : Problem(statusCode: StatusCodes.Status404NotFound, title: "This posting was not found. It may have been deleted.");
        }
        catch (InterviewInputException ex)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: ex.Message);
        }
        catch (InterviewGenerationException)
        {
            return Problem(statusCode: StatusCodes.Status502BadGateway, title: "No usable question came back. Try again.");
        }
        catch (Exception ex) when (TryMapLlm(ex, out var problem))
        {
            return problem!;
        }
    }

    [HttpPost("answers")]
    [Consumes("application/json")]
    public async Task<IActionResult> Answer(SubmitAnswerRequest request, [FromServices] SubmitAnswerHandler handler, CancellationToken ct)
    {
        try
        {
            var result = await handler.HandleAsync(new SubmitAnswerCommand(request.PostingId, request.QuestionId, request.Answer), ct);
            if (result.Error is not null)
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: result.Error);
            }

            return result.Answer is null
                ? Problem(statusCode: StatusCodes.Status404NotFound, title: "This question is gone. Reload the page.")
                : Ok(ToResponse(result.Answer));
        }
        catch (InterviewInputException ex)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: ex.Message);
        }
        catch (Exception ex) when (TryMapLlm(ex, out var problem))
        {
            return problem!;
        }
    }

    private bool TryMapLlm(Exception ex, out IActionResult? problem)
    {
        problem = ex switch
        {
            LlmDailyCapExceededException => Problem(statusCode: StatusCodes.Status429TooManyRequests, title: ex.Message),
            LlmUnavailableException => Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "The AI service is not available right now. Try again later."),
            LlmException => Problem(statusCode: StatusCodes.Status502BadGateway, title: "The AI service did not answer. Try again."),
            _ => null
        };
        return problem is not null;
    }

    private static AnswerResponse ToResponse(InterviewAnswer a) =>
        new(a.Id, a.Answer, a.Score, a.WhatWasGood, a.WhatToAdd, a.ExampleSentence, a.CreatedAt.Value);

    private static InterviewResponse ToResponse(InterviewView view)
    {
        IReadOnlyList<AnswerResponse> AnswersFor(Guid id) => view.Answers.Where(a => a.QuestionId == id).Select(ToResponse).ToList();
        string? GapFor(Guid? id) => view.Posting.Requirements.FirstOrDefault(r => r.Id == id)?.Text;

        var questions = view.Questions.OrderBy(q => q.Position)
            .Select(q => new QuestionResponse(q.Id, q.Source == QuestionSource.Gap ? "gap" : "repo", q.Text, q.Project, GapFor(q.RequirementId), AnswersFor(q.Id)))
            .Concat(view.CompanyQuestions.Select(q => new QuestionResponse(q.Id, "company", q.Text, null, null, AnswersFor(q.Id))))
            .ToList();
        return new InterviewResponse(view.Posting.Id, view.Posting.Company, view.Posting.Title, view.Posting.Language.ToString().ToLowerInvariant(), view.Posting.IsAnalysed, questions);
    }
}
