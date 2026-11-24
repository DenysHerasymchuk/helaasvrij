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
    public async Task<IActionResult> Questions(Guid postingId, [FromServices] GetInterviewHandler handler, CancellationToken ct)
    {
        try
        {
            return await handler.HandleAsync(postingId, ct) is { } view ? Ok(ToResponse(view)) : NotFound();
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

            return result.Answer is null ? NotFound() : Ok(ToResponse(result.Answer));
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
        new(a.Id, a.Answer, a.WhatWasGood, a.WhatToAdd, a.ExampleSentence, a.CreatedAt.Value);

    private static InterviewResponse ToResponse(InterviewView view)
    {
        IReadOnlyList<AnswerResponse> AnswersFor(Guid id) => view.Answers.Where(a => a.QuestionId == id).Select(ToResponse).ToList();

        var questions = view.Questions.OrderBy(q => q.Position)
            .Select(q => new QuestionResponse(q.Id, q.Source == QuestionSource.Gap ? "gap" : "repo", q.Text, q.Project, AnswersFor(q.Id)))
            .Concat(view.CompanyQuestions.Select(q => new QuestionResponse(q.Id, "company", q.Text, null, AnswersFor(q.Id))))
            .ToList();
        return new InterviewResponse(view.Posting.Id, view.Posting.Company, view.Posting.Title, view.Posting.Language.ToString().ToLowerInvariant(), questions);
    }
}
