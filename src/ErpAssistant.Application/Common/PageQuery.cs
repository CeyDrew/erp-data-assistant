using FluentValidation;

namespace ErpAssistant.Application.Common;

public abstract record PageQuery
{
    public const int MaxPageSize = 100;

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public abstract class PageQueryValidator<T> : AbstractValidator<T> where T : PageQuery
{
    protected PageQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, PageQuery.MaxPageSize);
    }
}
