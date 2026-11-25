using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Users;

namespace InternshipCoach.Application.Auth.GitHubSignIn;

public sealed class GitHubSignInHandler(IUserRepository users, IClock clock)
{
    public async Task<AuthResult> HandleAsync(GitHubSignInCommand command, CancellationToken cancellationToken)
    {
        if (command.GitHubId <= 0 || string.IsNullOrWhiteSpace(command.Login))
        {
            return AuthResult.Invalid(new Dictionary<string, string[]> { ["gitHub"] = ["GitHub profile is not valid."] });
        }

        var existing = await users.FindByGitHubIdAsync(command.GitHubId, cancellationToken);
        if (existing is not null)
        {
            return AuthResult.Ok(existing);
        }

        // Only a verified GitHub email may link to an existing account; otherwise anyone could
        // claim someone else's account by setting their GitHub email to it.
        Email? verified = null;
        if (command.VerifiedEmail is not null && Email.TryCreate(command.VerifiedEmail, out var parsed))
        {
            verified = parsed;
        }

        if (verified is not null)
        {
            var byEmail = await users.FindByEmailAsync(verified, cancellationToken);
            if (byEmail is not null)
            {
                byEmail.LinkGitHub(command.GitHubId, command.Login, command.AvatarUrl);
                await users.UpdateAsync(byEmail, cancellationToken);
                return AuthResult.Ok(byEmail);
            }
        }

        // No verified email: use GitHub's own noreply address so nobody's real email is squatted.
        var email = verified ?? Email.Create($"{command.GitHubId}+{command.Login}@users.noreply.github.com");
        var displayName = string.IsNullOrWhiteSpace(command.Name) ? command.Login : command.Name;
        var user = User.CreateFromGitHub(
            email, displayName, command.GitHubId, command.Login, command.AvatarUrl, new CreatedAt(clock.UtcNow));

        try
        {
            await users.AddAsync(user, cancellationToken);
        }
        catch (EmailAlreadyExistsException)
        {
            // Lost a race with a concurrent request; sign in as whoever won.
            var winner = await users.FindByGitHubIdAsync(command.GitHubId, cancellationToken);
            return winner is null ? AuthResult.EmailTaken() : AuthResult.Ok(winner);
        }

        return AuthResult.Ok(user);
    }
}
