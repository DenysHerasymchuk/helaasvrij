using InternshipCoach.Application.Abstractions;

namespace InternshipCoach.Application.Account.Delete;

public sealed class DeleteAccountHandler(IAccountDeleter deleter, ICurrentUser currentUser)
{
    public const string ConfirmationWord = "DELETE";

    /// <returns>False when the confirmation word is missing or wrong; nothing is deleted then.</returns>
    public async Task<bool> HandleAsync(string? confirm, CancellationToken cancellationToken)
    {
        if (!string.Equals(confirm, ConfirmationWord, StringComparison.Ordinal))
        {
            return false;
        }

        await deleter.DeleteAsync(currentUser.UserId, cancellationToken);
        return true;
    }
}
