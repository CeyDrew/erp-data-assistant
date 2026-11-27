using FluentValidation;

namespace ErpAssistant.Application.Ai;

public sealed class ChatRequestValidator : AbstractValidator<ChatRequest>
{
    public const int MaxHistory = 12;

    public ChatRequestValidator()
    {
        RuleFor(x => x.Message).NotEmpty().MaximumLength(1_000);
        RuleFor(x => x.History).Must(h => h is null || h.Count <= MaxHistory)
            .WithMessage($"Geçmiş en fazla {MaxHistory} mesaj olabilir.");
        RuleForEach(x => x.History).ChildRules(t =>
        {
            t.RuleFor(x => x.Role).Must(r => r is "user" or "assistant");
            t.RuleFor(x => x.Content).NotEmpty().MaximumLength(8_000);
        });
    }
}
